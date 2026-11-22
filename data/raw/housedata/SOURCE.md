# Real house data (hackathon organisers)

| | |
|---|---|
| Provider | Basel GreenTech 2026 hackathon organisers (DigitalHeat case), handed to the team as `Housedata.zip` |
| Received | 2026-11-21 (zip file date); stored in the repo on 2026-11-22 |
| Original archive SHA-256 | `95d503363322e5fa28f5f089a7ead12840ae227602ee4df9bf08e0f9c9c7e695` (`Housedata.zip`, deleted after extraction) |
| Local copy | `data/external/housedata/` (**git-ignored, not committed**: measured data and floor plans of a real household, anonymised by the organisers). File checksums: `data/external/housedata/SHA256SUMS` |
| Use permission | provided by the organisers for the hackathon |

## Contents

| File | Content |
|---|---|
| `Grund_und_Aufriss_anonymisiert.pdf`, `Dachstock_anonymisiert.pdf` | floor plans and elevations, attic (anonymised) |
| `energy/Data_Gas_2021-2026_anonymisiert.csv` | monthly gas readings per meter, kWh and CHF, `;`-separated, German headers. Main meter `MESSPUNKT_004` (heating): 66,317 / 45,268 / 26,191 / 44,464 kWh for 2021–2024; 2025 only January–September (October–December missing); two small meters of about 200 kWh/year |
| `energy/Data_Strom_<year>_anonymisiert.csv` | household electricity in 15-minute steps, 2021–2025 (1,600–2,100 kWh/year); not heating |

Customer, contract, meter and address fields are replaced by placeholders (`KUNDE_001`, `OBJEKT_001`, ...).
Do not copy rows of these files into committed files; derived files keep monthly totals at most.

## Use

`scripts/calibrate_real_house.py` fits the case formula's two house parameters to the 2025 gas
months and writes `config/fleet/houses/real-house.json` (Fleet variant, T-82).

Caveats: July and August 2025 read exactly the same (1,487 kWh), which may be estimated readings.
Gas is not heat: the boiler efficiency is a sourced minimum for new boilers, the real boiler is unknown.
