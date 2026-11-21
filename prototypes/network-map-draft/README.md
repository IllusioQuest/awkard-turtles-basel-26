# Network map draft (not the team's map)

A first draft from T-12 step 3. The team's map is built separately (Ennio); this folder is kept only for
reusable parts:

- `data/switzerland-cantons.geojson`: 26 Swiss cantons from Natural Earth (public domain), see
  `data/SOURCE.md` and `tools/extract_cantons.py`.
- `app.js`: an equirectangular projection fitted to Switzerland, the loader for the export format
  (`docs/model/network-map-data.md`), daily aggregation, a tooltip and a timeline. Plain ES module, no
  dependencies, works offline.

Run: from the repository root `python -m http.server 8765`, then open
`http://127.0.0.1:8765/prototypes/network-map-draft/?data=../../data/derived/network/switzerland-test/mock-export&day=196`.
