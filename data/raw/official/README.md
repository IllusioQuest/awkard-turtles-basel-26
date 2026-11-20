# Official reference profile (from the organisers)

The case says: "The ready hourly profile (weather, heating demand, hot water) is provided as a CSV
with the case, so every team starts from the same numbers."

**Status: not in the repository yet.** Whoever has the file: put it here **unchanged** (keep the
original file name) and add a short `SOURCE.md` (where it came from, date received, column
meanings).

Then:
1. Run `python3 scripts/fetch_data.py --check` (it reports the file).
2. Compare it with our own computation from `../open-meteo/` (heating days, yearly kWh, single
   hours). A comparison is not automated yet because the file format is unknown.
3. Once confirmed, **this file is the basis for all scored reference-house figures.**
