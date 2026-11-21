# T-67 visual direction v2 — real Basel street, selective reveal

Status: proposed after the first procedural dollhouse frame was rejected by the team lead. This is a production brief, not a rendered final style frame.

## What the first shot must feel like

An ordinary, believable residential street in Basel: neighbouring houses, pavement, street furniture, bicycles, trees or parked cars where appropriate, varied facade materials, visible weather and natural depth. The hero house belongs to this block. It is not an isolated object on a black stage, a toy model, a permanent dollhouse or a futuristic data centre.

The case reference house has **no specified address or geometry**. The team lead approved using the private plan only for room relationships: a separate cellar next to the gas heating room, with the heat integration surveyed rather than assumed. Create a representative Basel facade and freely chosen dimensions; do not reproduce the private dwelling's identifiable exterior or show raw plan pages in the public film. The plan is a spatial reference, while all scored heat and money figures remain from the fixed case model.

## Camera and reveal

1. Street-level human view of the Basel block. Find one house through camera movement, light and sound rather than an arrow or a label.
2. The camera approaches its facade. Only the wall segment needed to explain the water loop becomes translucent. The surrounding street remains visible through windows and in reflections.
3. The view enters the basement plant room; servers, a water-side exchanger, a pipe route and a tank are physically connected. The backup heater fills the winter gap. Keep all figures as separate, restrained overlays.
4. The facade closes again as the camera withdraws. Roofs and streets become the real Basel map context; the map takes over without cutting to a generic floating network.

Useful server heat uses a warm amber trace inside the water loop, backup heat a distinct neutral/warm trace, and moving **jobs** a thin cool soft line above the Basel map. No heat line jumps between buildings. Flow brightness follows a validated transfer quantity and hour; no decorative movement when the data says zero.

## Source and implementation route

- [Basel-Stadt MapBS 3D](https://www.bs.ch/bvd/grundbuch-und-vermessungsamt/geo/hilfe/mapbs-3d) offers real city geometry, terrain, aerial imagery and an in-view camera tool. The [official geoservices page](https://www.bs.ch/bvd/grundbuch-und-vermessungsamt/geo/geodaten/geodienste) describes free public STAC downloads and 3D Tiles; its [building tileset](https://3d.geo.bs.ch/cesium/tiles/av-gebaeude-basel-stadt/tileset.json) is a candidate for the city pull-out. Check the final asset's terms and give a clear source credit. Do not suggest the canton endorses DigitalHeat; the [OGD terms](https://data.bs.ch/terms/terms-and-conditions/) prohibit that implication.
- Build or license one high-quality residential house exterior for the close-up, matched to Basel architecture but not presented as a real case address. Blender can assemble and render this with PBR materials, street details, atmosphere and a camera path; a hand-built geometric blockout is **not** a final image.
- The current `web/` MapLibre map remains the live data view. Film may camera-match into it; a true continuous 3D camera is optional if its data and geometry can be aligned. A visual match cut is acceptable only if it looks continuous at presentation speed.
- The Basel year time-lapse requires a Basel-only network export. The existing documented Swiss test network and mock flows cannot be relabelled as Basel results (`docs/model/network-map-data.md`).

## Acceptance check for the next frame

At presentation size, a viewer should first see a **realistic Basel house in a street**. The basement reveal should be a purposeful visual effect that lasts only for the explanation. Materials, shadows, scale cues and neighbouring buildings must make the scene feel inhabited. If it still reads as a clean miniature, reject it before producing summer and map shots.

## Blockout review, 21 November 2026

The second Blender blockout moved the house into a row, but the frontal camera cut off the roofs and pavement, and repeated windows and roof forms still read as a synthetic set. It is **rejected as final imagery**. The preview and its procedural source remain local, outside this PR. A fresh-context visual review agreed with this assessment.

For the next shot test, use an eye-height oblique view along the street with a visible sidewalk, roadway and depth. Neighbours need different eaves, materials, window axes, entrances and wear. Establish true-to-life scale with a bicycle, tree, person or parked vehicle. A licensed real Basel street photograph can serve as a photographic plate; a separate, freely designed representative hero house can be camera-matched into it. The plate cannot be presented as the fixed case house. The [Wikimedia Commons winter Basel candidate](https://commons.wikimedia.org/wiki/File:A_Cold_Day_in_Basel_2.jpg) is CC BY 4.0, but its framing must be checked before selection and attribution must accompany any use. The public [MapBS 3D data](https://www.bs.ch/bvd/grundbuch-und-vermessungsamt/geo/hilfe/mapbs-3d) can guide wider street geometry and the city pull-out; it does not supply detailed, photorealistic facades by itself.
