"""Checks gen_library.py. Run with:

  blender --background --factory-startup --python addon/smoke/headless_gen.py

Builds a library from a two-entity DTO and checks that sf_assets.blend holds
asset-marked objects carrying sf_entity_id, and that cats.txt exists.
"""
import json
import os
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import bpy  # noqa: E402
from smoke.common import PKG_DIR, fail  # noqa: E402


def main():
    out = tempfile.mkdtemp(prefix="sf_gencheck_")
    dto = {"entities": [
        {"id": "pc01", "category": "chara", "class": "ply", "displayName": "pc01",
         "modelDefPath": "x"},
        {"id": "em001", "category": "chara", "class": "ene", "displayName": "em001",
         "modelDefPath": "x"}]}
    dto_path = os.path.join(out, "dto.json")
    with open(dto_path, "w", encoding="utf-8") as fh:
        json.dump(dto, fh)

    rc = subprocess.call([bpy.app.binary_path, "--background", "--factory-startup",
                          "--python", os.path.join(PKG_DIR, "gen_library.py"), "--",
                          "--json", dto_path, "--out", out])
    if rc != 0:
        fail("GENCHECK", "gen_library subprocess rc=" + str(rc))

    blend = os.path.join(out, "sf_assets.blend")
    if not os.path.exists(blend):
        fail("GENCHECK", "no sf_assets.blend")
    if not os.path.exists(os.path.join(out, "blender_assets.cats.txt")):
        fail("GENCHECK", "no cats.txt")

    ids = set()
    with bpy.data.libraries.load(blend, assets_only=True) as (src, dst):
        dst.objects = list(src.objects)
    for obj in dst.objects:
        if obj is not None and obj.asset_data is not None:
            ids.add(obj.asset_data.get("sf_entity_id"))
    if not {"pc01", "em001"}.issubset(ids):
        fail("GENCHECK", "expected sf_entity_id pc01+em001 in assets, got " + str(ids))
    print("GENCHECK-OK ids=" + str(sorted(i for i in ids if i)))


if __name__ == "__main__":
    main()
