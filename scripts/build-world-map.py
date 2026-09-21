#!/usr/bin/env python3
"""Build the bundled map from Natural Earth v5.1.2's 50m countries GeoJSON.

Download https://raw.githubusercontent.com/nvkelso/natural-earth-vector/v5.1.2/geojson/ne_50m_admin_0_countries.geojson
then run: python3 scripts/build-world-map.py /path/to/ne_50m_admin_0_countries.geojson
"""
import json
import pathlib
import sys

source = json.loads(pathlib.Path(sys.argv[1]).read_text())
features = []

def coordinates(value):
    return [coordinates(item) if isinstance(item, list) else round(item, 4) for item in value]

for feature in source['features']:
    props = feature['properties']
    if props['ISO_A2'] == 'AQ':
        continue
    # Only use fallback codes for countries whose ISO fields are missing. Do not
    # assign Australia's code to the separately drawn Australian territories.
    iso2 = props['ISO_A2']
    iso3 = props['ISO_A3']
    if props['ADM0_A3'] in ('FRA', 'NOR', 'KOS'):
        iso2 = props['ISO_A2_EH']
        iso3 = props['ADM0_A3']
    aliases = sorted({v for v in [props['NAME'], props['ADMIN'], iso2, iso3] if v and v != '-99'})
    features.append({
        'type': 'Feature',
        'properties': {'name': props['NAME_EN'], 'aliases': aliases},
        'geometry': {'type': feature['geometry']['type'], 'coordinates': coordinates(feature['geometry']['coordinates'])}
    })
output = pathlib.Path(__file__).resolve().parents[1] / 'src/BlazorBlueprint.Components/wwwroot/maps/world.json'
output.write_text(json.dumps({'type': 'FeatureCollection', 'features': features}, ensure_ascii=False, separators=(',', ':')) + '\n')
print(f'{len(features)} regions, {output.stat().st_size:,} bytes')
