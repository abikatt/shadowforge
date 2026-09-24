import importlib
import sys
import types
from unittest.mock import MagicMock

import pytest


@pytest.fixture
def mock_bpy(monkeypatch, stub_mathutils):
    fake_bpy = types.ModuleType('bpy')
    fake_bpy.ops = MagicMock()
    fake_bpy.data = MagicMock()
    fake_bpy.types = MagicMock()
    fake_bpy.utils = MagicMock()
    monkeypatch.setitem(sys.modules, 'bpy', fake_bpy)
    return fake_bpy


@pytest.fixture
def scene_module(mock_bpy):
    """Reloads scene and the modules it calls so all of them bind this test's bpy."""
    import io_shadowforge.materials
    import io_shadowforge.rigfold
    import io_shadowforge.scene
    for module in (io_shadowforge.materials, io_shadowforge.rigfold, io_shadowforge.scene):
        importlib.reload(module)
    return io_shadowforge.scene


def test_export_scene_glb_requests_extras(scene_module, mock_bpy, tmp_path):
    mock_bpy.data.materials = []
    mock_bpy.data.objects = []

    scene_module.export_scene_glb(str(tmp_path), "test_entity")

    export_call = mock_bpy.ops.export_scene.gltf.call_args
    assert export_call is not None, "export_scene.gltf was not called"
    assert export_call[1].get("export_extras") is True, export_call[1]


def test_import_entity_imports_unused_materials(scene_module, mock_bpy, tmp_path, fixture_dir):
    """A face texture bound only under eye stages ships as a material no
    primitive uses, and staged-eye layering needs its image in bpy.data.images."""
    fixture = fixture_dir / "pc01.sfmod.json"
    man_path = tmp_path / "pc01.sfmod.json"
    man_path.write_text(fixture.read_text())
    glb_path = tmp_path / "pc01.glb"
    glb_path.write_bytes(b"")
    result = types.SimpleNamespace(outputs=[str(glb_path), str(man_path)])
    mock_bpy.data.objects = []
    mock_bpy.data.actions = []

    with pytest.raises(RuntimeError):
        scene_module.import_entity(str(tmp_path), "pc01", result)

    import_call = mock_bpy.ops.import_scene.gltf.call_args
    assert import_call is not None, "import_scene.gltf was not called"
    assert import_call[1].get("import_unused_materials") is True, import_call[1]
