"""Parses .sfmod.json (entity) and .sfmap.json (map) manifests into dataclasses.

Imports no bpy. Field names are the snake_case forms of the camelCase JSON
keys emitted by ShadowForge.Contracts, except `class`, which becomes `cls`.
`raw` keeps the original dict so it can be stored verbatim in the blend.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional


@dataclass
class EntityInfo:
    id: str
    cls: str
    rig_id: str
    rig_class: str


@dataclass
class InstallInfo:
    game_root: str
    source: str
    mods_root: Optional[str]
    mods_available: bool


@dataclass
class ClipInfo:
    name: str
    hmb: str


def _clips(items):
    return [ClipInfo(c["name"], c["hmb"]) for c in items]


@dataclass
class ObjectOptInfo:
    slot: int
    hdb: str


@dataclass
class ModelInfo:
    path: Optional[str]
    object_hdb: Optional[str]
    object_l0_hdb: Optional[str]
    mot_pack: Optional[str]
    clips: list
    texture_override_csv: Optional[str] = None
    fur_len: Optional[str] = None
    face: Optional[str] = None
    mot_pack_f: Optional[str] = None
    mot_pack_b: Optional[str] = None
    object_opts: list = field(default_factory=list)
    motions: list = field(default_factory=list)


@dataclass
class FileInfo:
    role: str
    vfs_path: str
    loose_rel_path: str
    ipk_name: str
    ipk_inner_path: str
    exists: bool


@dataclass
class TextureInfo:
    file: str
    material: Optional[str]


@dataclass
class ExportInfo:
    glb: str
    up_axis: str
    unit: str
    clip_count: int


@dataclass
class Manifest:
    schema: int
    entity: EntityInfo
    install: InstallInfo
    model: ModelInfo
    files: list
    textures: list
    export: ExportInfo
    raw: dict = field(default_factory=dict)

    @classmethod
    def load(cls, path) -> "Manifest":
        return cls.from_dict(json.loads(Path(path).read_text(encoding="utf-8")))

    @classmethod
    def from_dict(cls, d) -> "Manifest":
        e, ins, m, ex = d["entity"], d["install"], d["model"], d["export"]
        return cls(
            schema=int(d.get("schema", 0)),
            entity=EntityInfo(e["id"], e["class"], e["rigId"], e["rigClass"]),
            install=InstallInfo(ins["gameRoot"], ins["source"],
                                ins.get("modsRoot"), bool(ins["modsAvailable"])),
            model=ModelInfo(m.get("path"), m.get("objectHdb"), m.get("objectL0Hdb"),
                            m.get("motPack"),
                            _clips(m.get("clips", [])),
                            m.get("textureOverrideCsv"), m.get("furLen"), m.get("face"),
                            m.get("motPackF"), m.get("motPackB"),
                            [ObjectOptInfo(int(o["slot"]), o["hdb"]) for o in m.get("objectOpts", [])],
                            _clips(m.get("motions", []))),
            files=[FileInfo(f["role"], f["vfsPath"], f["looseRelPath"],
                            f["ipkName"], f["ipkInnerPath"], bool(f["exists"]))
                   for f in d.get("files", [])],
            textures=[TextureInfo(t["file"], t.get("material")) for t in d.get("textures", [])],
            export=ExportInfo(ex["glb"], ex["upAxis"], ex["unit"], int(ex["clipCount"])),
            raw=d,
        )


@dataclass
class MapModelEntry:
    name: str
    object_hdb: str
    area: int
    pri: float
    exported: bool


@dataclass
class MapSkipEntry:
    kind: str
    path: str
    reason: str


@dataclass
class MapManifest:
    schema: int
    stage_id: str
    category: str
    region_ipk: str
    models: list
    skipped: list
    glb: str
    raw: dict = field(default_factory=dict)

    @classmethod
    def load(cls, path) -> "MapManifest":
        return cls.from_dict(json.loads(Path(path).read_text(encoding="utf-8")))

    @classmethod
    def from_dict(cls, d) -> "MapManifest":
        return cls(
            schema=int(d.get("schema", 0)),
            stage_id=d["stageId"],
            category=d.get("category", ""),
            region_ipk=d.get("regionIpk", ""),
            models=[MapModelEntry(m["name"], m.get("objectHdb", ""), int(m.get("area", 0)),
                                  float(m.get("pri", 0.0)), bool(m.get("exported", False)))
                    for m in d.get("models", [])],
            skipped=[MapSkipEntry(s.get("kind", ""), s.get("path", ""), s.get("reason", ""))
                     for s in d.get("skipped", [])],
            glb=d["export"]["glb"],
            raw=d,
        )
