"""ShadowForge sidebar panel in the Asset Browser."""
import bpy
from bpy_extras.asset_utils import SpaceAssetInfo


class SF_PT_asset_browser(bpy.types.Panel):
    bl_label = "ShadowForge"
    bl_idname = "SF_PT_asset_browser"
    bl_space_type = "FILE_BROWSER"
    bl_region_type = "TOOL_PROPS"
    bl_category = "ShadowForge"

    @classmethod
    def poll(cls, context):
        return SpaceAssetInfo.is_asset_browser_poll(context)

    def draw(self, context):
        props = context.scene.sf
        col = self.layout.column()
        col.operator("shadowforge.refresh_catalog", icon="FILE_REFRESH")
        col.separator()
        asset = getattr(context, "asset", None)
        category = asset.metadata.get("sf_category", "chara") if asset else None
        is_sf = asset is not None and asset.metadata.get("sf_entity_id") is not None
        if category == "map":
            col.prop(props, "include_textures")
            import_op = "shadowforge.import_map"
        else:
            col.prop(props, "include_animations")
            col.prop(props, "include_textures")
            import_op = "shadowforge.import_asset"
        sub = col.column()
        sub.enabled = is_sf
        sub.operator(import_op, icon="IMPORT")

        active = context.active_object
        if active is not None and active.get("sf_id") is not None:
            box = self.layout.box()
            box.label(text="Deploy")
            box.prop(props, "mod_name")
            box.prop(props, "enable_mod")
            box.operator("shadowforge.deploy_asset", icon="EXPORT")


def register():
    bpy.utils.register_class(SF_PT_asset_browser)


def unregister():
    bpy.utils.unregister_class(SF_PT_asset_browser)
