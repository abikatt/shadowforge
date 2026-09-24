"""Runs a catalog refresh against the real install. Run with:

  blender --background --factory-startup --python addon/smoke/headless_refresh.py -- [--game-root <r>]

Checks that the library blend was generated and its directory registered.
"""
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import io_shadowforge as addon  # noqa: E402
from io_shadowforge import library  # noqa: E402
from smoke.common import bundled_exe, fail, script_args  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--game-root", default=None)
    args = ap.parse_args(script_args())

    addon.register()
    res = library.refresh(bundled_exe(), args.game_root)
    if res["characters"] <= 0:
        fail("REFRESH", "refresh returned no entities")
    blend = os.path.join(library.library_dir(), "sf_assets.blend")
    if not os.path.exists(blend):
        fail("REFRESH", "no sf_assets.blend generated")
    if not library.is_registered(library.library_dir()):
        fail("REFRESH", "library dir not registered")
    addon.unregister()
    if res.get("map_list_error"):
        print("REFRESH-WARN map_list_error=" + res["map_list_error"])
    print("REFRESH-OK entities=" + str(res["characters"]) +
          " maps=" + str(res["maps"]) +
          " maps_unavailable=" + str(len(res["maps_unavailable"])) +
          " blend=" + blend)


if __name__ == "__main__":
    main()
