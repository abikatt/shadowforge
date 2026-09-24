"""Puts io_shadowforge on sys.path twice: its own directory, so bpy-free
modules import standalone (`import bridge`), and the addon directory, so
modules with relative imports load as `io_shadowforge.<name>`."""
import sys
import types
from pathlib import Path
from unittest.mock import MagicMock

import pytest

ADDON = Path(__file__).resolve().parent.parent
PKG = ADDON / "io_shadowforge"
for _p in (PKG, ADDON):
    if str(_p) not in sys.path:
        sys.path.insert(0, str(_p))


@pytest.fixture
def fixture_dir():
    return Path(__file__).resolve().parent / "fixtures"


@pytest.fixture
def stub_mathutils(monkeypatch):
    fake = types.ModuleType('mathutils')
    fake.Matrix = MagicMock()
    fake.Quaternion = MagicMock()
    fake.Vector = MagicMock()
    monkeypatch.setitem(sys.modules, 'mathutils', fake)
    return fake
