"""ShadowForge Blender extension: edit Blue Dragon characters and maps through
the sforge CLI.

register() and unregister() import the bpy submodules lazily, so
`import io_shadowforge` and its bpy-free modules load under plain CPython.
"""

_MODULES = ("prefs", "props", "ops", "panels")


def register():
    import importlib
    for name in _MODULES:
        importlib.import_module("." + name, __package__).register()


def unregister():
    import importlib
    for name in reversed(_MODULES):
        importlib.import_module("." + name, __package__).unregister()
