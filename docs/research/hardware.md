# Hardware profiles: CPU vs GPU (T-15)

**Status: exploration, first version (2026-11-21).** Not reviewed by the team. The scored
reference-house figures keep the case's abstract 1 kW server (`config/reference-house.json`);
the profiles below are **variants** for the economics (T-14) and the pitch.

Profiles: [`config/hardware/`](../../config/hardware/) (schema `hardware-profile.schema.json`,
loader `DigitalHeat.Core.HardwareProfile`, tests `HardwareProfileTests`). New raw values:
[`data/raw/market/hardware.csv`](../../data/raw/market/hardware.csv). Table reproduced by
`python3 scripts/hardware_comparison.py`.

## 1. Method

Every profile is **normalised to one case server = 1 kW of electric load**, so one server-hour is
1 kWh of electricity and 0.8 kWh of recoverable heat for every profile. Hardware only changes
what that kWh costs and what it can sell. The box count per kW follows the same method as
[`computing-price.md`](computing-price.md) §2 (count how many units draw 1 kW at full load).

| Profile | Composition of 1 kW | Unit power basis |
|---|---|---|
| `cpu-server` | 6.15 × Dell R570 (Xeon 6507P, 8 cores, 32 GB), CHF 8,248 each | CPU TDP 150 W + 32 GB × 0.392 W = 162.5 W (estimate, no fans/PSU) |
| `gpu-inference-l4` | 1 host (R570 class) + 11.6 × NVIDIA L4 24 GB, CHF 2,235 per card | L4 TDP 72 W; host 162.5 W |
| `gpu-consumer-rtx4090` | 2 × complete RTX 4090 desktop, CHF 8,150 each | 2 × 450 W GPU TGP + about 100 W rest (A5 assumption) |

Formulas (all per 1 kW server; `H` = sold server-hours per server-year = 22,661 ÷ 4 = 5,665 from
`data/derived/reference-house/explorer-default.json`, the 4-server heat-led draft scenario):

- depreciation per sold server-hour = purchase price ÷ lifetime (6 years) ÷ `H` (straight line,
  no residual value; lifetime = accounting life, `costs.csv`)
- operations per sold server-hour = managed monitoring + patching CHF 59 per physical box and
  month (`cost-ops-smartsolution-server-basic`) × boxes × 12 ÷ `H`
- electricity = 1 kWh × 0.3325 CHF/kWh; heat value if used = 0.8 kWh × 0.3325 ÷ 2.5 = CHF 0.106
- **full-cost break-even** = electricity + depreciation + operations − heat value
- heat per CHF = 0.8 kWh ÷ cost per sold server-hour
- market price per server-hour = units per kW × price per unit-hour (spot, home-host marketplace,
  on-demand; rows of `compute-prices.csv`)

**Worked example (`gpu-inference-l4`).** Cards per kW = (1 − 0.1625) ÷ 0.072 = 11.63. Purchase =
11.63 × 2,235 + 8,248 = CHF 34,244. Depreciation = 34,244 ÷ 6 ÷ 5,665 = CHF 1.007 per sold
server-hour. Operations = 59 × 12 ÷ 5,665 = 0.125. Cost = 0.3325 + 1.007 + 0.125 = CHF 1.465;
minus heat 0.106 → break-even **CHF 1.36**. Income at AWS g6.xlarge spot (1 L4 each,
CHF 0.1914/h) = 11.63 × 0.1914 = **CHF 2.23**.

## 2. Comparison

Output of `python3 scripts/hardware_comparison.py` (2026-11-21):

| Per 1 kW server | `cpu-server` | `gpu-consumer-rtx4090` | `gpu-inference-l4` |
|---|---:|---:|---:|
| Purchase price per 1 kW server (CHF) | 50,743 | 16,300 | 34,244 |
| Depreciation per sold server-hour (CHF) | 1.493 | 0.480 | 1.007 |
| Operations per sold server-hour (CHF) | 0.769 | 0.250 | 0.125 |
| Electricity per server-hour (CHF) | 0.333 | 0.333 | 0.333 |
| **Cost per sold server-hour (CHF)** | 2.594 | 1.062 | 1.465 |
| Heat value if used (CHF per server-hour) | 0.106 | 0.106 | 0.106 |
| **Full-cost break-even, heat used (CHF per server-hour)** | 2.488 | 0.956 | 1.359 |
| Recoverable heat per CHF of cost (kWh/CHF) | 0.31 | 0.75 | 0.55 |
| Transcription throughput (audio h per kWh) | 436 | 92 | 470 |
| Income at spot (CHF per server-hour) | 1.80 | TODO | 2.23 |
| Income at home-host marketplace price (CHF per server-hour) | 0.68 | 0.57 | 4.30 |
| Income at on-demand price (CHF per server-hour) | 7.10 | 1.50 | 10.60 |
| Idle power (kW per server) | 0.205 | TODO | TODO |

How to read it: each column is the same 1 kW heater built from different hardware. "Cost per sold
server-hour" spreads the yearly fixed cost over the 5,665 hours the house actually buys heat. If
the income row is above the break-even row, that hardware pays its full cost in the heat-led
draft scenario (installation and water cooling still excluded). Case break-even without hardware
is CHF 0.226 for every profile (`docs/results/reference-house.md`).

Transcription throughput uses **different models** (CPU: `whisper.cpp base`, our own measurement;
L4: `whisper-large-v3-turbo`; RTX 4090: `faster-whisper large-v2` measured on a slower RTX 3070 Ti
as proxy). Read it as "audio hours per kWh at the quality each platform runs well": the L4 does
about the same audio per kWh as our CPU, but with a far larger, more accurate model. Our
CPU estimate for the large model is about 62 audio h per kWh (`computing-price.md` B), so for
quality transcription a GPU is several times more energy-efficient (470 ÷ 62 ≈ 7.6 on these
numbers; turbo is lighter than large, low confidence).

**What drives the result.** Electricity is CHF 0.33 of every server-hour; hardware is
CHF 0.48–1.49. Hardware cost per kW, not efficiency, decides the full-cost case.

- `cpu-server` is the worst per kW because the only sourced rack server is an 8-core entry box
  (≈ 0.16 kW), so a 1 kW CPU heater needs six of them. A denser CPU server would be much cheaper
  per kW (`TODO(source)`: quote). **Caveat for T-06:** the depreciation sensitivity in
  `docs/results/reference-house.md` counts one R570 per 1 kW server (CHF 0.243 per server-hour);
  by this power estimate it is about 6× that.
- `gpu-inference-l4` has the highest value per kWh: datacentre spot capacity for 11.6 L4s
  (CHF 2.23) is above its full-cost break-even (CHF 1.36). It is the only profile where a market
  reference covers the full cost.
- `gpu-consumer-rtx4090` is cheapest to buy and gives the most heat per CHF, but its market
  (home-host GPU marketplaces, CHF 0.57–0.84) is below its break-even (CHF 0.96).

## 3. What it means for heat-led operation

- **Idle hours.** In the draft scenario each server runs 5,665 of 8,760 hours (64.7 %); 3,095
  hours per server-year are not bought by the heat demand, mostly in summer (heating season 206
  days, `docs/case/key-figures.md`). Fixed cost runs through all of them, which is why
  depreciation per *sold* hour is 1.5× the per-calendar-hour value.
- **Idle power.** A powered-on idle CPU server still draws ≈ 0.205 kW (SPECpower ratio, not
  measured): 3,095 h × 0.205 kWh × 0.3325 = CHF 211 per server-year of heat nobody needs. Hard
  power-off (or suspend) during non-heating hours avoids it; GPU idle draw is `TODO(source)`.
- **Summer filler sold as spot capacity (under consideration).** In a non-heating hour the
  hardware cost is sunk, so the marginal cost of running is only electricity (CHF 0.3325; heat
  is dumped, no heat value). Filler pays if its price is above that:

  | Profile | Filler price used | Margin per filler hour | × 3,095 idle h per server-year |
  |---|---|---:|---:|
  | `cpu-server` | home-host 0.68 | 0.35 | CHF 1,075 |
  | `gpu-consumer-rtx4090` | home-host 0.57 | 0.24 | CHF 735 |
  | `gpu-inference-l4` | AWS spot 2.23 | 1.90 | CHF 5,872 |

  Upper bounds: they assume a buyer for every idle hour (`TODO(source)`: demand for interruptible
  home-hosted capacity), and the heat must be dumped somewhere (the case's reference house has
  no dump path; dumped heat also weakens the sustainability story). The scored figures do not
  include filler.
- **Interruptibility fits GPUs well.** GPU batch inference (transcription, embeddings) works in
  short, restartable chunks, the same property the T-02 job uses for pause/resume.

## 4. Recommendation for the pitch (for the team to decide)

1. Keep the **abstract 1 kW server** for every scored figure, as the case requires.
2. Show **one hardware slide**: "same 1 kWh of heat, three kinds of hardware". The message: the
   electricity is cheap relative to the hardware, so DigitalHeat needs **high-value compute per
   kWh**. That points to **GPU inference servers (L4 class)**: they turn the same kWh into
   high-quality transcription (or other inference) and are the only sourced profile whose market
   price covers the full cost in the heat-led scenario.
3. Present summer **filler sold as spot capacity** as an option with an explicit caveat (demand
   unproven, heat dumped), not as part of the scored result.
4. Be honest about confidence: the L4 card price and L4 throughput come from search snippets
   (sites blocked in our session) and must be re-checked before the final pitch.

## 5. Limits and open `TODO(source)`s

- Measured wall power of any real server under our workloads (CPU box power is a TDP estimate;
  GPU power uses board limits); idle power of L4 and RTX 4090 systems.
- A quote for a ~1 kW CPU server and for a GPU host that holds ~11 L4 cards (slot count; GPU-dense
  chassis start at CHF 28,720, `cost-server-brentford-s240-from`, which would raise the L4 full-cost
  break-even from CHF 1.36 to CHF 1.96 per sold server-hour by the same formula).
- Water cooling for passively cooled L4 cards; all profiles assume the case's 80 % recovery.
- L4 price (`hw-cost-bechtle-pny-l4`) and L4 Whisper throughput (`hw-perf-e2e-l4-whisper-turbo-b4`)
  are `low` confidence; RTX 4090 Whisper throughput is a proxy; the GCP L4 spot price
  (CHF 0.062/h, `low`) was not used.
- RTX 4090 datacentre spot price; consumer-GPU lifetime (6 years is a datacentre accounting life).
- Webhosting throughput per kW (all profiles).
- Fleet-scale operations cost (CHF 59 per box-month is a retail managed-service price and
  dominates the CPU column).
- Demand for interruptible home-hosted capacity at these prices.
