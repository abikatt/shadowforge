import catalog


DTO = {"entities": [
    {"id": "pc01", "category": "chara", "class": "ply",
     "modelDefPath": "database\\model\\chara\\ply\\model_pc01.mdl", "displayName": "pc01"},
    {"id": "em001", "category": "chara", "class": "ene",
     "modelDefPath": "database\\model\\chara\\ene\\model_em001.mdl", "displayName": "em001"},
    {"id": "em002", "category": "chara", "class": "ene",
     "modelDefPath": "database\\model\\chara\\ene\\model_em002.mdl", "displayName": "em002"},
]}


def test_catalog_uuid_is_deterministic():
    a = catalog.catalog_uuid("chara", "ply")
    b = catalog.catalog_uuid("chara", "ply")
    assert a == b and len(a) == 36 and a != catalog.catalog_uuid("chara", "ene")


def test_proxy_specs_fields():
    specs = catalog.proxy_specs(DTO)
    assert len(specs) == 3
    pc = next(s for s in specs if s["id"] == "pc01")
    assert pc["catalog_path"] == "chara/ply"
    assert pc["catalog_uuid"] == catalog.catalog_uuid("chara", "ply")
    assert pc["name"] == "pc01"


def test_cats_txt_has_version_and_one_line_per_unique_catalog():
    specs = catalog.proxy_specs(DTO)
    text = catalog.cats_txt(specs)
    lines = [ln for ln in text.splitlines() if ln and not ln.startswith("#")]
    assert lines[0] == "VERSION 1"
    catalog_lines = [ln for ln in lines[1:] if ":" in ln]
    assert len(catalog_lines) == 2
    assert any(ln.endswith(":ply") and "chara/ply" in ln for ln in catalog_lines)
    assert any(ln.endswith(":ene") and "chara/ene" in ln for ln in catalog_lines)


def test_merge_specs_appends_available_maps_and_reports_skipped():
    edto = {"entities": [{"id": "pc01", "category": "chara", "class": "ply", "displayName": "pc01"}]}
    mdto = {"stages": [
        {"id": "bg01_01", "category": "map", "class": "town", "displayName": "bg01_01",
         "regionIpk": "bg01_00.ipk", "available": True, "modelCount": 24},
        {"id": "bg99_01", "category": "map", "class": "town", "displayName": "bg99_01",
         "regionIpk": "bg99_00.ipk", "available": False, "modelCount": 0},
    ]}
    merged, skipped = catalog.merge_specs(edto, mdto)
    ids = [e["id"] for e in merged["entities"]]
    assert ids == ["pc01", "bg01_01"]
    assert merged["entities"][1]["category"] == "map"
    assert skipped == ["bg99_01"]


def test_merge_specs_tolerates_missing_map_dto():
    edto = {"entities": []}
    merged, skipped = catalog.merge_specs(edto, None)
    assert merged == {"entities": []} and skipped == []
