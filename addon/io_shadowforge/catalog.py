"""Turns `entity list` and `map list` DTOs into asset-catalog proxy specs and
blender_assets.cats.txt text.

Imports no bpy, so gen_library.py can load it inside a background Blender and
unit tests can load it under plain CPython.
"""
from __future__ import annotations

import uuid

# Blender keys catalog assignments by UUID, so a catalog path must map to the
# same UUID on every regeneration or assets fall out of their catalogs.
_NS = uuid.uuid5(uuid.NAMESPACE_URL, "shadowforge:asset-catalog")


def catalog_path(category: str, cls: str) -> str:
    return category + "/" + cls


def catalog_uuid(category: str, cls: str) -> str:
    return str(uuid.uuid5(_NS, catalog_path(category, cls)))


def proxy_specs(dto: dict) -> list:
    specs = []
    for e in dto.get("entities") or []:
        category = e.get("category", "chara")
        cls = e.get("class", "")
        specs.append({
            "id": e["id"],
            "name": e.get("displayName") or e["id"],
            "category": category,
            "cls": cls,
            "catalog_path": catalog_path(category, cls),
            "catalog_uuid": catalog_uuid(category, cls),
        })
    return specs


def merge_specs(entity_dto: dict, map_dto: dict):
    """Appends the map stages to the entity list. Returns (merged_dto,
    skipped_stage_ids), where skipped stages are those whose region pack is
    unavailable and so stay out of the catalog."""
    entities = list((entity_dto or {}).get("entities") or [])
    skipped = []
    for s in (map_dto or {}).get("stages") or []:
        if not s.get("available", False):
            skipped.append(s["id"])
            continue
        entities.append({
            "id": s["id"],
            "category": "map",
            "class": s.get("class", ""),
            "displayName": s.get("displayName") or s["id"],
        })
    return {"entities": entities}, skipped


def cats_txt(specs: list) -> str:
    """Lines are UUID:catalog/path:simple name, one per distinct catalog."""
    lines = ["VERSION 1", ""]
    seen = set()
    for s in specs:
        key = s["catalog_path"]
        if key in seen:
            continue
        seen.add(key)
        lines.append(s["catalog_uuid"] + ":" + s["catalog_path"] + ":" + s["cls"])
    return "\n".join(lines) + "\n"
