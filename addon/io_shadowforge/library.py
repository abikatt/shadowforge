"""Catalog refresh: list entities and maps through sforge, build the proxy
asset library in a background Blender (gen_library.py), and register the
library directory with Blender's asset system.
"""
import json
import os
import tempfile

import bpy

from . import bridge
from . import catalog

_LIB_NAME = "ShadowForge"


def library_dir() -> str:
    """bpy.utils.extension_path_user raises ValueError unless the addon was
    loaded through an extension repository (bl_ext.<repo>.io_shadowforge).
    Dev, test and smoke runs import io_shadowforge from sys.path instead and
    use the per-addon user resource dir."""
    try:
        return bpy.utils.extension_path_user(__package__, path="catalog", create=True)
    except ValueError:
        return bpy.utils.user_resource('DATAFILES', path="io_shadowforge/catalog", create=True)


def _gen_script() -> str:
    return os.path.join(os.path.dirname(os.path.abspath(__file__)), "gen_library.py")


def gen_args(dto_path, out_dir, blender=None):
    """Returns (executable, args) for the background library generator."""
    exe = blender or bpy.app.binary_path
    return exe, ["--background", "--factory-startup",
                 "--python", _gen_script(), "--", "--json", dto_path, "--out", out_dir]


def write_generation_input(entity_dto, map_dto):
    """Merges the list DTOs, writes the generator's input JSON to a temp dir,
    and returns (dto_path, summary). A None map_dto means the map listing
    failed, and the summary then counts zero maps."""
    merged, skipped = catalog.merge_specs(entity_dto, map_dto)
    dto_path = os.path.join(tempfile.mkdtemp(prefix="sf_catalog_"), "dto.json")
    with open(dto_path, "w", encoding="utf-8") as fh:
        json.dump(merged, fh)
    maps_total = len((map_dto or {}).get("stages") or [])
    summary = {
        "characters": len((entity_dto or {}).get("entities") or []),
        "maps": (maps_total - len(skipped)) if map_dto else 0,
        "maps_unavailable": skipped,
        "map_list_error": None,
    }
    return dto_path, summary


def refresh(exe, game_root, *, blender=None) -> dict:
    """Blocking catalog refresh for scripted callers. The interactive path is
    SF_OT_refresh_catalog, which runs the same steps as SforgeJobs.

    A failed map listing does not fail the refresh. The catalog is built from
    entities only and the summary's map_list_error holds the error text."""
    entity_dto = bridge.list_entities(exe, game_root=game_root)
    map_list_error = None
    try:
        map_dto = bridge.list_maps(exe, game_root=game_root)
    except bridge.SforgeError as ex:
        map_dto = None
        map_list_error = str(ex)
        print("shadowforge: map list unavailable: " + map_list_error)

    out = library_dir()
    dto_path, summary = write_generation_input(entity_dto, map_dto)
    summary["map_list_error"] = map_list_error
    gen_exe, gen_cmd_args = gen_args(dto_path, out, blender=blender)
    cp = bridge.run(gen_exe, gen_cmd_args)
    if cp.returncode != 0:
        raise RuntimeError("library generation failed (rc=%d): %s" % (cp.returncode, cp.stderr[-500:]))

    register_library(out)
    return summary


def _same_path(a, b):
    return os.path.normcase(os.path.abspath(a)) == os.path.normcase(os.path.abspath(b))


def is_registered(directory: str) -> bool:
    return any(_same_path(lib.path, directory)
               for lib in bpy.context.preferences.filepaths.asset_libraries)


def register_library(directory: str):
    if not is_registered(directory):
        bpy.context.preferences.filepaths.asset_libraries.new(name=_LIB_NAME, directory=directory)
