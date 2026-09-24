"""Addon preferences: optional overrides for the bundled exe and game root."""
import bpy


class SF_Prefs(bpy.types.AddonPreferences):
    bl_idname = __package__

    sforge_path: bpy.props.StringProperty(
        name="sforge.exe override",
        description="Leave blank to use the bundled bin/sforge.exe",
        subtype="FILE_PATH", default="")
    game_root: bpy.props.StringProperty(
        name="Game root override",
        description="Leave blank to auto-locate (registry / SHADOWFORGE_GAME_ROOT)",
        subtype="DIR_PATH", default="")
    default_author: bpy.props.StringProperty(
        name="Default mod author", default="")

    def draw(self, context):
        col = self.layout.column()
        col.prop(self, "sforge_path")
        col.prop(self, "game_root")
        col.prop(self, "default_author")


def register():
    bpy.utils.register_class(SF_Prefs)


def unregister():
    bpy.utils.unregister_class(SF_Prefs)
