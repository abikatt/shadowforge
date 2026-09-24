"""Builds the proxy asset library inside a separate background Blender:

  blender --background --factory-startup --python gen_library.py -- --json <dto.json> --out <libdir>

Writes one asset-marked Empty per entity to <libdir>/sf_assets.blend and the
catalog tree to <libdir>/blender_assets.cats.txt, then prints "GEN-OK <n>".
Running in the user's session would replace their open file.
"""
import argparse
import json
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import catalog  # noqa: E402

LIBRARY_BLEND = "sf_assets.blend"
# Old library file name, deleted so Blender does not list its assets alongside
# LIBRARY_BLEND.
_OLD_LIBRARY_BLEND = "characters.blend"


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args(argv)

    with open(args.json, "r", encoding="utf-8") as fh:
        dto = json.load(fh)
    specs = catalog.proxy_specs(dto)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.context.scene.collection
    for s in specs:
        obj = bpy.data.objects.new(s["name"], None)
        coll.objects.link(obj)
        obj.asset_mark()
        obj.asset_data.catalog_id = s["catalog_uuid"]
        obj.asset_data["sf_entity_id"] = s["id"]
        obj.asset_data["sf_category"] = s["category"]

    os.makedirs(args.out, exist_ok=True)
    with open(os.path.join(args.out, "blender_assets.cats.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(catalog.cats_txt(specs))

    old = os.path.join(args.out, _OLD_LIBRARY_BLEND)
    if os.path.exists(old):
        os.remove(old)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(args.out, LIBRARY_BLEND))
    print("GEN-OK " + str(len(specs)))


if __name__ == "__main__":
    main()
