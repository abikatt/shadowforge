"""Helpers shared by the headless Blender smoke scripts. Each script puts the
addon directory on sys.path, then imports this module as smoke.common."""
import os
import sys

import bpy

ADDON_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PKG_DIR = os.path.join(ADDON_DIR, "io_shadowforge")


def script_args():
    """Arguments after "--" on the Blender command line."""
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def enable_gltf_io():
    """Blender's GLTF I/O is a bundled addon that --factory-startup may leave
    disabled. A failure to enable it is printed and the script continues."""
    try:
        bpy.ops.preferences.addon_enable(module="io_scene_gltf2")
    except Exception as ex:
        print("note: addon_enable io_scene_gltf2: " + str(ex))


def bundled_exe():
    from io_shadowforge import bridge
    return bridge.resolve_exe(None, package_dir=PKG_DIR)


def fail(tag, msg):
    print(tag + "-FAIL: " + msg)
    sys.exit(1)
