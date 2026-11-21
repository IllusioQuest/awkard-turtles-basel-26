#!/usr/bin/env python3
"""Extract the 26 Swiss cantons from Natural Earth admin-1 (10m) into a small GeoJSON for the map.

Usage (from the repository root, after downloading the Natural Earth file, see data/SOURCE.md):
  python prototypes/network-map/tools/extract_cantons.py \
      data/external/naturalearth/ne_10m_admin_1.geojson prototypes/network-map/data/switzerland-cantons.geojson
Coordinates are rounded to 4 decimals (about 10 m); nothing else is changed.
"""
import json
import sys


def main(src: str, dst: str) -> None:
    data = json.load(open(src, encoding="utf-8"))

    def r(c):
        return [round(c[0], 4), round(c[1], 4)]

    features = []
    for f in data["features"]:
        p = f["properties"]
        if p.get("iso_a2") != "CH":
            continue
        g = f["geometry"]
        if g["type"] == "Polygon":
            coords = [[r(c) for c in ring] for ring in g["coordinates"]]
        else:
            coords = [[[r(c) for c in ring] for ring in poly] for poly in g["coordinates"]]
        code = (p.get("iso_3166_2") or "")[-2:] or p.get("postal")
        features.append({"type": "Feature", "properties": {"name": p.get("name"), "code": code},
                         "geometry": {"type": g["type"], "coordinates": coords}})
    with open(dst, "w", encoding="utf-8") as out:
        json.dump({"type": "FeatureCollection", "features": features}, out, separators=(",", ":"))
    print(len(features), sorted(f["properties"]["code"] for f in features))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
