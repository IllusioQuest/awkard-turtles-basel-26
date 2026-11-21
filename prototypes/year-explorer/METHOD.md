# Year explorer method

This is an **exploration prototype**. Its JavaScript simulation reimplements the case's hourly
heat rule in the browser; it is not the production C# model and should not replace its results.
The data and system fonts are embedded in `index.html`, so the page can be opened directly as a
local file without network access.

## Inputs

`build_data.py` reads the committed Basel 2025 hourly temperatures from
`data/raw/open-meteo/basel-2025-hourly.csv` and the 2026 IWB Basel H4 price from
`data/raw/elcom/iwb-basel-h4-2026.json`. It writes a compact `explorer-data.json` alongside the page
and embeds the same payload in `index.html`. Rebuild with:

```powershell
python prototypes/year-explorer/build_data.py
```

The page's initial scenario has four 1 kW servers, 0% minimum load, 35 kWh of lossless storage,
CHF 0.3325/kWh electricity and CHF 0.66/server-hour example computing income. Hardware cost is
initially CHF 0 per server-year. The case does not specify purchase cost, so that is a scenario
input, not a sourced estimate. The default surplus is therefore labelled **at example price,
without hardware**.

## Hourly rule

For each local day, space heating is active only when its mean outdoor temperature is strictly
below 12 °C. During an active day, hourly demand is
`max(0, 10 kW × (20 °C − outdoor temperature) ÷ 25.6 °C)`. Hot water adds 12 kWh/day ÷ 24 to each
hour. Each 1 kW reference server makes 0.8 kWh of recoverable heat per hour at full load. The
greedy controller runs up to the amount required by current demand plus empty tank capacity,
bounded by the selected server count and minimum-load floor. Storage is lossless, starts empty, and
cannot exceed its selected capacity. Unmet demand uses backup heat; unused heat after filling the
tank is dumped.

Electricity cost is server-hours × 1 kW × the selected electricity price. Used heat is valued at
the selected electricity price ÷ the case heat-pump COP of 2.5. Computing income is server-hours ×
the selected example computing price. Annual hardware cost is selected CHF/server-year × server
count. The **case break-even** excludes hardware; the **full-cost break-even** adds the selected
annual hardware cost and divides by sold server-hours. The verdict uses the full-cost value.

## Verification

`verify.py` independently reimplements the same rule for the default case and checks hourly energy
conservation, tank bounds, and agreement with the committed C# `YearRun` reference-house output.
It is a cross-check, not proof that both implementations share one code path. Run:

```powershell
python prototypes/year-explorer/verify.py
```
