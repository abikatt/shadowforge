import importlib
import sys
import types
from unittest.mock import MagicMock

import pytest


@pytest.fixture
def ops_module(monkeypatch, stub_mathutils):
    fake_bpy = types.ModuleType('bpy')
    fake_bpy.ops = MagicMock()
    fake_bpy.data = MagicMock()
    fake_bpy.types = MagicMock()
    fake_bpy.types.Operator = object
    fake_bpy.utils = MagicMock()
    monkeypatch.setitem(sys.modules, 'bpy', fake_bpy)
    import io_shadowforge.ops
    return importlib.reload(io_shadowforge.ops)


def test_operators_have_unique_idnames(ops_module):
    idnames = [cls.bl_idname for cls in ops_module._CLASSES]
    assert len(set(idnames)) == len(idnames)
    assert all(i.startswith("shadowforge.") for i in idnames)


def test_status_line_shows_fraction_as_percent(ops_module):
    assert ops_module._status_line("baking model 3/24: x") == "ShadowForge: [ 12%] baking model 3/24: x"
    assert ops_module._status_line("done") == "ShadowForge: done"
