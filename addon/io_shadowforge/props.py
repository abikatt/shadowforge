"""Per-scene state for the ShadowForge panel."""
import bpy


class SF_Props(bpy.types.PropertyGroup):
    include_animations: bpy.props.BoolProperty(
        name="Include Animations", default=True,
        description="Request the character's animation clips from sforge on export")
    include_textures: bpy.props.BoolProperty(
        name="Include Textures", default=True,
        description="Request the character's textures/materials from sforge on export")
    mod_name: bpy.props.StringProperty(
        name="Mod Name", default="my-mod",
        description="Folder name under mods\\ to deploy the edited character into")
    enable_mod: bpy.props.BoolProperty(
        name="Enable Mod", default=False,
        description="Add the deployed mod to mod_order.txt so it loads in-game")


def register():
    bpy.utils.register_class(SF_Props)
    bpy.types.Scene.sf = bpy.props.PointerProperty(type=SF_Props)


def unregister():
    del bpy.types.Scene.sf
    bpy.utils.unregister_class(SF_Props)
