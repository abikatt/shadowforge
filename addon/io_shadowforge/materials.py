"""Viewport preview for staged (eye) materials.

sforge exports a staged material in contract layout: baseColor is the stage-1
iris image on TEXCOORD_1, and the stage-0 face and stage-2 eyelid images are
named in material extras. Rendered as-is, the iris texture's transparent
padding paints the whole eye region dark.

layer_staged_materials() puts the stage-0 face (TEXCOORD_0) under the iris,
mixed by iris alpha. The stage-2 eyelid is a runtime blink animation and stays
out of the rest-pose preview. contract_layout_for_export() rewires back to
contract layout around a scene export, because the GLTF exporter cannot trace
baseColorTexture through a Mix node.
"""
import bpy

from contextlib import contextmanager

# Node names are unique per node tree, so MIX_NODE also marks a layered material.
MIX_NODE = "SF Stage Mix"
STAGE0_TEX_NODE = "SF Stage0 Texture"
STAGE0_UV_NODE = "SF Stage0 UV"

STAGE0_PROP = "sfStage0Texture"


def _socket(sockets, identifier):
    """Looks a socket up by identifier. The Mix node has several sockets
    named "A", "B" and "Result" (one per data type), so names are ambiguous."""
    for s in sockets:
        if s.identifier == identifier:
            return s
    raise KeyError(identifier)


def _find_node(nt, node_type):
    for n in nt.nodes:
        if n.type == node_type:
            return n
    return None


def _link_into(nt, socket):
    """bpy returns a fresh wrapper per RNA access, so sockets compare with ==
    (pointer equality), never `is`."""
    for l in nt.links:
        if l.to_socket == socket:
            return l
    return None


def _staged_materials(objs):
    """Yields (material, mesh) for each distinct staged material on objs."""
    seen = set()
    for obj in objs:
        if obj.type != 'MESH':
            continue
        for slot in obj.material_slots:
            mat = slot.material
            if mat is None or mat.name in seen:
                continue
            if mat.get(STAGE0_PROP) is None or not mat.use_nodes:
                continue
            seen.add(mat.name)
            yield mat, obj.data


def layer_staged_materials(objs):
    """Layers every staged material used by objs. Returns
    (updated_material_names, warnings). A material that cannot be layered
    stays in contract layout and produces a warning."""
    updated = []
    warnings = []
    for mat, mesh in _staged_materials(objs):
        nt = mat.node_tree
        if nt.nodes.get(MIX_NODE) is not None:
            continue
        bsdf = _find_node(nt, 'BSDF_PRINCIPLED')
        if bsdf is None:
            warnings.append("staged material '%s': no Principled BSDF node" % mat.name)
            continue
        base_in = bsdf.inputs["Base Color"]
        iris_link = _link_into(nt, base_in)
        if iris_link is None or iris_link.from_node.type != 'TEX_IMAGE':
            warnings.append("staged material '%s': Base Color is not an image "
                            "texture, left as imported" % mat.name)
            continue
        iris = iris_link.from_node
        stage0_name = mat.get(STAGE0_PROP)
        stage0_img = bpy.data.images.get(stage0_name) if stage0_name else None
        if stage0_img is None:
            warnings.append("staged material '%s': stage-0 image '%s' not found "
                            "in this blend. The eye region will show the iris "
                            "texture only" % (mat.name, stage0_name))
            continue
        if not mesh.uv_layers:
            warnings.append("staged material '%s': mesh has no UV layers" % mat.name)
            continue

        uv0 = nt.nodes.new('ShaderNodeUVMap')
        uv0.name = STAGE0_UV_NODE
        uv0.uv_map = mesh.uv_layers[0].name
        uv0.location = (iris.location[0] - 250, iris.location[1] + 300)

        tex0 = nt.nodes.new('ShaderNodeTexImage')
        tex0.name = STAGE0_TEX_NODE
        tex0.label = "stage 0 (face)"
        tex0.image = stage0_img
        tex0.location = (iris.location[0], iris.location[1] + 300)

        mix = nt.nodes.new('ShaderNodeMix')
        mix.data_type = 'RGBA'
        mix.name = MIX_NODE
        mix.label = "face + iris decal"
        mix.location = (iris.location[0] + 300, iris.location[1] + 150)

        nt.links.new(_socket(uv0.outputs, "UV"), tex0.inputs["Vector"])
        nt.links.new(_socket(tex0.outputs, "Color"), _socket(mix.inputs, "A_Color"))
        nt.links.new(_socket(iris.outputs, "Color"), _socket(mix.inputs, "B_Color"))
        nt.links.new(_socket(iris.outputs, "Alpha"), _socket(mix.inputs, "Factor_Float"))
        nt.links.new(_socket(mix.outputs, "Result_Color"), base_in)
        updated.append(mat.name)
    return updated, warnings


@contextmanager
def contract_layout_for_export():
    """Links the iris image straight into Base Color on every layered
    material for the duration of an export, then restores the Mix link.
    The C# importer reads stage 1 from the exported baseColorTexture.
    Materials whose Base Color no longer comes from the Mix node were rewired
    by the user and are left alone."""
    restore = []
    for mat in bpy.data.materials:
        if not getattr(mat, "use_nodes", False) or mat.node_tree is None:
            continue
        nt = mat.node_tree
        mix = nt.nodes.get(MIX_NODE)
        if mix is None:
            continue
        bsdf = _find_node(nt, 'BSDF_PRINCIPLED')
        if bsdf is None:
            continue
        base_in = bsdf.inputs["Base Color"]
        link = _link_into(nt, base_in)
        if link is None or link.from_node != mix:
            continue
        iris_link = _link_into(nt, _socket(mix.inputs, "B_Color"))
        if iris_link is None or iris_link.from_node.type != 'TEX_IMAGE':
            continue
        nt.links.new(_socket(iris_link.from_node.outputs, "Color"), base_in)
        restore.append((nt, _socket(mix.outputs, "Result_Color"), base_in))
    try:
        yield
    finally:
        for nt, out_sock, in_sock in restore:
            nt.links.new(out_sock, in_sock)
