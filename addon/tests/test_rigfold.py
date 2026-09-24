"""Tests for the bpy-free parts of rigfold: name flipping and fold planning.
Edit-bone and action retargeting need Blender and are checked by
smoke/headless_rigfold.py."""
import importlib
import math
import sys
import types

import pytest


@pytest.fixture
def rigfold(monkeypatch, stub_mathutils):
    monkeypatch.setitem(sys.modules, 'bpy', types.ModuleType('bpy'))
    import io_shadowforge.rigfold
    return importlib.reload(io_shadowforge.rigfold)


def test_flip_side_name(rigfold):
    assert rigfold.flip_side_name("leftarm") == "rightarm"
    assert rigfold.flip_side_name("rightforearm") == "leftforearm"
    assert rigfold.flip_side_name("spine1") is None
    assert rigfold.flip_side_name("hips") is None


def test_plan_folds_180_pair(rigfold):
    # np114 shoulders: leftshoulder roll 3.96deg, rightshoulder 176.04deg.
    bones = [
        ("leftshoulder", (0.196, -0.24, 11.538), (1.146, -0.24, 11.472), math.radians(3.96)),
        ("rightshoulder", (-0.196, -0.24, 11.538), (-1.146, -0.24, 11.472), math.radians(176.04)),
    ]
    deltas, warnings = rigfold.plan_folds(bones)
    assert warnings == []
    assert set(deltas) == {"rightshoulder"}
    # -roll_L - roll_R = -3.96 - 176.04 = -180deg, which wraps into (-pi, pi]
    # as +pi. A 180-degree twist either way is the same rotation.
    assert abs(deltas["rightshoulder"]) == pytest.approx(math.pi, abs=1e-9)


def test_plan_folds_small_authored_asymmetry(rigfold):
    # np114 hands are about 10.7deg off mirror-consistent and still get folded.
    bones = [
        ("lefthand", (5.262, -0.233, 11.472), (5.635, -0.268, 11.465), math.radians(6.46)),
        ("righthand", (-5.262, -0.233, 11.472), (-5.635, -0.268, 11.465), math.radians(4.22)),
    ]
    deltas, _ = rigfold.plan_folds(bones)
    assert deltas["righthand"] == pytest.approx(math.radians(-10.68), abs=1e-6)


def test_plan_folds_skips_consistent_pair(rigfold):
    bones = [
        ("leftfoot", (1.0, 0.0, 1.0), (1.0, 0.5, 1.0), math.radians(5.0)),
        ("rightfoot", (-1.0, 0.0, 1.0), (-1.0, 0.5, 1.0), math.radians(-5.0)),
    ]
    deltas, warnings = rigfold.plan_folds(bones)
    assert deltas == {}
    assert warnings == []


def test_plan_folds_warns_on_non_mirrored_placement(rigfold):
    bones = [
        ("leftpouch", (1.0, 0.0, 1.0), (1.0, 0.5, 1.0), 0.0),
        ("rightpouch", (-1.4, 0.2, 1.0), (-1.4, 0.7, 1.0), math.pi),
    ]
    deltas, warnings = rigfold.plan_folds(bones)
    assert deltas == {}
    assert len(warnings) == 1
    assert "leftpouch" in warnings[0] or "rightpouch" in warnings[0]


def test_plan_folds_ignores_unpaired_and_center(rigfold):
    bones = [
        ("leftwisp", (0.5, 0, 0), (0.5, 1, 0), 1.0),
        ("spine", (0, 0, 0), (0, 1, 0), 2.0),
    ]
    deltas, warnings = rigfold.plan_folds(bones)
    assert deltas == {}
    assert warnings == []


def test_wrap_angle(rigfold):
    assert rigfold._wrap_angle(3 * math.pi) == pytest.approx(math.pi)
    assert rigfold._wrap_angle(-3 * math.pi) == pytest.approx(math.pi)
    assert rigfold._wrap_angle(0.5) == pytest.approx(0.5)


def test_bone_of_path(rigfold):
    assert rigfold._bone_of_path('pose.bones["rightarm"].rotation_quaternion') == "rightarm"
    assert rigfold._bone_of_path('pose.bones["a.b"].location') == "a.b"
    assert rigfold._bone_of_path('location') is None
