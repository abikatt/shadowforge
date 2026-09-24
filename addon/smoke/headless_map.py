"""Exports and imports one stage and checks that the collections match the
map manifest. Usage:

  blender -b --factory-startup --python headless_map.py -- --stage bg01_01
"""
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import bpy  # noqa: E402
from io_shadowforge import bridge, scene  # noqa: E402
from smoke.common import bundled_exe, enable_gltf_io, script_args  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--stage", default="bg01_01")
    args = ap.parse_args(script_args())

    bpy.ops.wm.read_factory_settings(use_empty=True)
    enable_gltf_io()
    exe = bundled_exe()

    workdir = scene.fresh_workdir("map_" + args.stage)
    res = bridge.export_map(exe, args.stage, workdir)
    man = scene.import_map(bpy.context, workdir, args.stage, res)

    root = bpy.data.collections.get("SF_" + args.stage)
    assert root is not None, "root collection missing"
    exported = [m for m in man.models if m.exported]
    assert exported, "no models exported"

    want_areas = {"AREA_%d" % m.area for m in exported}
    have_areas = {scene.strip_dedup(c.name) for c in root.children}
    assert want_areas == have_areas, "areas %r != manifest %r" % (have_areas, want_areas)
    obj_names = [o.name for o in bpy.data.objects]
    for m in exported:
        stem = os.path.splitext(m.name)[0]
        assert any(n.startswith(stem) for n in obj_names), "no object for model " + stem
    print("MAP-OK %s models=%d missing=%d sidecars=%d areas=%d images=%d"
          % (args.stage, len(exported), len(man.models) - len(exported),
             len(man.skipped), len(root.children), len(bpy.data.images)))


if __name__ == "__main__":
    main()
