import manifest as mf


def test_load_full_manifest(fixture_dir):
    m = mf.Manifest.load(fixture_dir / "pc01.sfmod.json")
    assert m.schema == 1
    assert m.entity.id == "pc01" and m.entity.cls == "ply" and m.entity.rig_id == "pc01"
    assert m.install.source == "Install" and m.install.mods_available is True
    assert m.model.object_hdb == "pc01_obj.hdb" and m.model.mot_pack == "pc01_mot.mpk"
    assert [c.name for c in m.model.clips] == ["WAIT", "RUN"]
    assert m.files[0].ipk_name == "pc01.ipk" and m.files[0].exists is True
    assert m.textures[0].file == "pc01_01.dds" and m.textures[0].material == "pc01_body"
    assert m.textures[1].material is None
    assert m.export.glb == "pc01.glb" and m.export.clip_count == 165
    assert m.raw["entity"]["id"] == "pc01"


def test_map_manifest_loads(fixture_dir):
    man = mf.MapManifest.load(fixture_dir / "bg01_01.sfmap.json")
    assert man.stage_id == "bg01_01"
    assert man.category == "town"
    assert len(man.models) == 2
    assert man.models[0].exported is True
    assert man.models[1].pri == -8.0
    assert man.skipped[0].reason == "sidecar-not-imported-v1"
    assert man.glb == "bg01_01.glb"


def test_entity_manifest_defaults_optional_model_fields(fixture_dir):
    man = mf.Manifest.load(fixture_dir / "pc01.sfmod.json")
    assert man.model.texture_override_csv is None
    assert man.model.fur_len is None
    assert man.model.object_opts == []
    assert man.model.motions == []


def test_entity_manifest_parses_object_opts_and_motions():
    man = mf.Manifest.from_dict({
        "schema": 1,
        "entity": {"id": "em028", "class": "npc", "rigId": "em028", "rigClass": "npc"},
        "install": {"gameRoot": "C:\\g", "source": "Loose", "modsRoot": None, "modsAvailable": False},
        "model": {
            "path": None, "objectHdb": None, "objectL0Hdb": None, "motPack": None, "clips": [],
            "objectOpts": [{"slot": 1, "hdb": "em028_efc_eye_obj.hdb"}],
            "motions": [{"name": "IDLE", "hmb": "em028_idle.hmb"}],
        },
        "files": [],
        "textures": [],
        "export": {"glb": "em028.glb", "upAxis": "Y", "unit": "meter", "clipCount": 0},
    })
    assert man.model.object_opts == [mf.ObjectOptInfo(1, "em028_efc_eye_obj.hdb")]
    assert man.model.motions == [mf.ClipInfo("IDLE", "em028_idle.hmb")]


def test_from_dict_tolerates_missing_optional_lists():
    m = mf.Manifest.from_dict({
        "schema": 1,
        "entity": {"id": "em001", "class": "npc", "rigId": "em001", "rigClass": "npc"},
        "install": {"gameRoot": "C:\\g", "source": "Loose", "modsRoot": None, "modsAvailable": False},
        "model": {"path": None, "objectHdb": None, "objectL0Hdb": None, "motPack": None, "clips": []},
        "files": [],
        "textures": [],
        "export": {"glb": "em001.glb", "upAxis": "Y", "unit": "meter", "clipCount": 0},
    })
    assert m.install.mods_available is False and m.install.mods_root is None
    assert m.model.clips == [] and m.model.path is None
