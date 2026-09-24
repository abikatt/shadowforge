"""Imports many distinct characters with animations and textures off and
checks that nothing unrequested accumulates. Datablock counts stand in for
memory use, since Blender's Python cannot measure RSS portably. Usage:

  blender -b --factory-startup --python headless_many.py -- --entities pc01,pc02,em001 --rounds 2
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
    ap.add_argument("--entities", default="pc01,pc02,em001")
    ap.add_argument("--rounds", type=int, default=2)
    args = ap.parse_args(script_args())
    entities = [e for e in args.entities.split(",") if e]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    enable_gltf_io()
    exe = bundled_exe()

    actions0 = len(bpy.data.actions)
    images0 = len(bpy.data.images)
    imported_total = 0
    n = 0
    for _ in range(args.rounds):
        for entity in entities:
            n += 1
            before = len(bpy.data.objects)
            workdir = scene.fresh_workdir(entity)
            res = bridge.export_entity(exe, entity, workdir, anims=False, textures=False)
            scene.import_entity(workdir, entity, res)
            imported_total += len(bpy.data.objects) - before
            print("MANY-OK import %d (%s) objects=%d" % (n, entity, len(bpy.data.objects)))

    assert len(bpy.data.actions) == actions0, "actions leaked: %d" % len(bpy.data.actions)
    assert len(bpy.data.images) == images0, "images leaked: %d" % len(bpy.data.images)
    assert len(bpy.data.objects) == imported_total, (
        "object count %d != imported total %d" % (len(bpy.data.objects), imported_total))
    manifest_texts = [t for t in bpy.data.texts if t.name.startswith("sf_manifest_")]
    assert len(manifest_texts) == len(set(entities)), (
        "expected %d manifest texts, got %d" % (len(set(entities)), len(manifest_texts)))
    print("MANY-OK final")


if __name__ == "__main__":
    main()
