# SS13 DMM map import helpers

- `ss13_type_map.yaml` — prefix table used by the Play Mode importer (`SS3D/Map Import/Import DMM…`) and this CLI.
- `dmm_gap_report.py` — list frequent unmapped type paths without Unity:

```bash
python3 Tools/map_import/dmm_gap_report.py /path/to/map.dmm
```

Do not commit full tgstation maps or converted Box/Meta station templates. Keep fixtures tiny under EditMode tests.
