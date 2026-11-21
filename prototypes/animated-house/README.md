# Animated reference house (T-25)

Open [`index.html`](index.html) directly in a browser. No server, network, Docker or build step is
needed for the two-day replay. Select 15 January or 15 July, press Play, or drag the hour slider.
Arrow colour shows whether an exported heat flow is active; the numeric labels carry the actual
kWh values. Arrow speed and width are illustrative, not a second physical model.

The checked-in HTML contains only 48 hours extracted from the committed
[`reference-house-v1`](../../data/derived/reference-house-v1/) C# result bundle. To regenerate it
from that export, run from the repository root:

```text
python scripts/build_animated_house.py
```

The generator checks bundle schema version, site ID, required columns and all 24 nominal local
hours for each selected day. The HTML has no external scripts, fonts, images or fetch requests.
It displays simulated house heat, not a live sensor measurement. The C# export does not split
useful heat between radiators and hot water, so the drawing does not claim that split.

To show job control, run the existing `DigitalHeat.LiveRun` with a real container and
`--force-pause-step 12`, save its console output as a `.log` or `.txt` file, and use **Import LiveRun
log**. The overlay shows only the imported target and observed CPU use; it never changes the
exported house heat. The log stays in the browser and is not uploaded. Imported text is labelled
as unverified. The selected year-export days do not naturally have a zero target load: winter
runs fully, while summer still needs hot water at a low load.

The layout sketch for team-lead review is
[`docs/pitch/T-25-house-sketch.md`](../../docs/pitch/T-25-house-sketch.md).
The user approved that direction. The polished final pitch video is a separate
[`T-37`](../../docs/tickets/T-37-professional-pitch-video.md) deliverable. The real T-02
transcription logs in PR #15 were checked with this page's importer (24 hours per day, pause at
12:00, resume at 13:00); their source branch is owned by Ennio.
