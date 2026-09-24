"""End-to-end open and deploy of one character. Run with:

  blender --background --python addon/smoke/headless_smoke.py -- --id pc01 [--game-root <r>]

Deploys into a mod named smoke_test_mod (not enabled) under the real install's
mods folder and deletes that folder again afterwards.
"""
import argparse
import json
import os
import shutil
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import bpy  # noqa: E402
import io_shadowforge as addon  # noqa: E402
from io_shadowforge import scene  # noqa: E402
from smoke.common import bundled_exe, enable_gltf_io, fail, script_args  # noqa: E402

_MOD_NAME = "smoke_test_mod"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--id", default="pc01")
    ap.add_argument("--game-root", default=None)
    args = ap.parse_args(script_args())

    enable_gltf_io()
    addon.register()
    exe = bundled_exe()
    bpy.ops.wm.read_factory_settings(use_empty=True)

    workdir = tempfile.mkdtemp(prefix="smoke_" + args.id + "_")
    man = scene.open_blocking(args.id, workdir, exe, game_root=args.game_root,
                              anims=True, textures=True)
    if not os.path.exists(os.path.join(workdir, args.id + ".glb")):
        fail("SMOKE", "no exported glb in workdir")
    tagged = [o for o in bpy.data.objects if o.get("sf_id") == args.id]
    if not tagged:
        fail("SMOKE", "no objects tagged with sf_id=" + args.id)
    text_name = "sf_manifest_" + args.id
    man_text = bpy.data.texts.get(text_name)
    if man_text is None:
        fail("SMOKE", "no manifest text datablock named " + text_name)
    for o in tagged:
        if o.get("sf_manifest_text") != text_name:
            fail("SMOKE", "object " + o.name + " sf_manifest_text != " + text_name)
    man_json = json.loads(man_text.as_string())
    if man_json.get("entity", {}).get("id") != args.id:
        fail("SMOKE", "manifest text entity id mismatch: " + str(man_json.get("entity")))
    print("OPEN-OK: imported " + str(len(bpy.data.objects)) + " objects, " +
          str(man.export.clip_count) + " clips, tagged " + str(len(tagged)) +
          ", manifest text=" + text_name)

    result = scene.deploy_blocking(args.id, workdir, _MOD_NAME, exe,
                                   enable=False, game_root=args.game_root)
    mod_dir = result.outputs[0] if (result.ok and result.outputs) else None
    try:
        if not mod_dir:
            fail("SMOKE", "deploy returned no mod dir")
        hits = []
        for root, _dirs, files in os.walk(mod_dir):
            for f in files:
                if f.lower().endswith((".hdb", ".mpk")):
                    hits.append(os.path.join(root, f))
        if not hits:
            fail("SMOKE", "deployed mod dir has no cooked hdb/mpk: " + mod_dir)
        print("DEPLOY-OK: mod_dir=" + mod_dir + " files=" + str(len(hits)))
        for w in result.warnings:
            print("WARN: " + str(w.get("message", w)))
    finally:
        if mod_dir and os.path.basename(mod_dir.rstrip("\\/")) == _MOD_NAME:
            shutil.rmtree(mod_dir, ignore_errors=True)

    addon.unregister()
    print("SMOKE-OK")


if __name__ == "__main__":
    main()
