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
is CHF 0.226 for every profile (year model, retired 2026-11-22; Fleet: `docs/model/fleet-simulation.md` §16).

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
  the retired year-model results page counted one R570 per 1 kW server (CHF 0.243 per server-hour);
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

## 6. Corrected hybrid CPU/GPU calculations (T-38, 2026-11-21)

Requested follow-up to the [hybrid service review](hybrid-gpu-cpu-review.md). These calculations
check the user-supplied *DigitalHeat Deep Technical Engineering Manual (GPU + CPU)*, pages 1-6.
They do not replace the scored reference configuration, accept a hardware purchase, or establish
measured performance. Sections 1-4 remain exploratory economic normalisations: fractional boxes
or cards per kW are accounting equivalents, not a validated physical build.

### 6.1 Inputs and evidence boundary

| Input | Value | Source / status |
|---|---:|---|
| Reference electrical power | 1 kW per case server | [Case](../case/challenge.md), fixed scored assumption |
| Reference water heat recovery | 0.8 kWh per electrical kWh | Case assumption; not a measured GPU cooling-loop efficiency |
| Reference usable tank capacity | 35 kWh, lossless | Case assumption |
| Domestic hot water | 12 kWh/day, represented as 0.5 kW constant | Case daily total / 24 hours |
| Example server count | 4 | Manual's scenario; a variant, not a new fixed requirement |
| GPU active / idle power | 550-750 W / 15 W | Manual p. 1; TODO(source): hardware measurement |
| CPU active / idle power | 180-250 W / 25 W | Manual p. 1; TODO(source): hardware measurement |
| Other system power | 50-80 W | Manual p. 1; TODO(source): wall-power boundary and idle measurement |
| Illustrative benchmark | 140 W for a 60 s job | Manual p. 6; hypothetical example, not our measurement |

All interval calculations below use hours, kW and kWh. Power is a rate; tank capacity is energy.
For a constant interval, energy_kwh = power_kw * duration_hours. Apply the 80% recovery only
once. Physical limits, idle load and actual recovery require measurements before use in hardware
variants; they must not silently change the scored case model.

### 6.2 Hybrid electrical budget

Adding the manual's subsystem envelopes gives:

- Active low endpoint = 550 + 180 + 50 = **780 W**.
- Active high endpoint = 750 + 250 + 80 = **1,080 W**.
- If the stated other-system range also applies at idle, idle = 15 + 25 + (50 to 80)
  = **90-120 W**, not 40 W for the whole node.
- Applying the case recovery factor illustratively gives **0.624-0.864 kW** recovered at the
  active endpoints, and **0.072-0.096 kW** at those conditional idle endpoints.

These are sums of claimed ranges, not measured or guaranteed operating bounds. CPU, GPU,
PSU losses and chassis measurements must have non-overlapping boundaries before adding them.
A 1 kW budget does not prove that a selected workload fills it. A GPU power cap is an upper
limit, not a demand target, and CPU quota is not a linear wattage control. The existing abstract
1 kW comparison remains useful for accounting, while a real machine needs its own profile.

### 6.3 Four-server heat balance and corrected tank timing

At the reference full load:

- Electrical power = 4 * 1 = **4 kW**.
- Recoverable thermal power = 4 * 0.8 = **3.2 kW**.
- During a DHW-only interval, net charge rate = 3.2 - 0.5 = **2.7 kW**.
- Four hours adds 2.7 * 4 = **10.8 kWh** to the tank (provided that headroom exists).
- Filling an empty 35 kWh tank takes 35 / 2.7 = **12.96 hours**, with that demand and output
  held constant. Four hours fills it only if it already contains at least 35 - 10.8 = **24.2 kWh**.

The ideal daily energy equivalent for DHW is a different calculation:

- Required full-load fleet runtime = 12 / 3.2 = **3.75 hours/day**.
- Fleet duty fraction = 3.75 / 24 = **15.625%**.
- Equivalent continuous reference servers = 12 / (0.8 * 24) = **0.625**.
- Daily electricity = 4 * 3.75 = **15 kWh**, recovering 15 * 0.8 = **12 kWh**.

This assumes zero energy outside active periods, sufficient useful queued work, no losses,
compatible timing, and the same tank energy at the start and end of the comparison day. It is
not proof that every day follows a 3.75-hour schedule. During that active block the tank gains
2.7 * 3.75 = 10.125 kWh; the other 20.25 hours consume 0.5 * 20.25 = 10.125 kWh. Idle power,
live sessions, startup overhead and a different initial tank state change the result. Stopping
containers does not power off the physical machine.

### 6.4 Temperature examples checked against the case

On a heating day, demand_kw = max(0, 10 * (20 - temperature_c) / 25.6) + 0.5.
Heating-day eligibility uses the mean of the day's 24 hourly temperatures, not the current hour
alone. The following rows are constant-temperature worked examples on eligible heating days.
Backup values assume the tank supplies no energy during the hour.

| Temperature | Space heat (kW) | Total including DHW (kW) | Four-server output (kW) | Deficit before storage discharge (kW) |
|---|---:|---:|---:|---:|
| -5.6 C | 10.00000 | 10.50000 | 3.2 | 7.30000 |
| -4 C | 9.37500 | 9.87500 | 3.2 | 6.67500 |
| +6 C | 5.46875 | 5.96875 | 3.2 | 2.76875 |
| +10 C | 3.90625 | 4.40625 | 3.2 | 1.20625 |
| Non-heating day | 0 | 0.50000 | 3.2 | 0; 2.7 kW available to charge storage |

Thus the manual's +10 C charging diagram cannot represent a constant +10 C heating day:
4.40625 kW demand exceeds 3.2 kW supply. A warm hour on a non-heating day must be identified
explicitly instead. The table's peak-total-to-summer ratio is 10.5 / 0.5 = **21**, not 18;
space heating alone divided by DHW would be 10 / 0.5 = 20. Neither ratio is an annual result.

### 6.5 Benchmark energy and explicitly idealised scaling

For the manual's hypothetical 140 W, 60-second job:

- Electrical energy = 140 * 60 = 8,400 joules = 8,400 / 3,600 = **2.333 Wh/job**
  = **0.002333 kWh/job**.
- Recoverable heat under the case assumption = 0.8 * 2.333 = **1.867 Wh/job**.
- Measured-example serial rate = 3,600 / 60 = **60 jobs/hour**.
- Ideal power-normalisation factor = 1,000 / 140 = **7.143**.
- Ideal aggregate rate = 60 * 7.143 = **428.571 jobs/hour per kW**.
- Cross-check: 428.571 * 2.333 Wh is approximately **1,000 Wh** electrical per hour;
  recoverable heat is **800 Wh** per hour.

This preserves energy per job by assumption. It can represent ideal parallel replication of
identical efficiency; it does not establish that a single job finishes in 8.4 seconds on a
new rack server. CPU/GPU contention, memory capacity, model, batching and platform overhead
can all change efficiency. Report real throughput and energy separately for each hardware and
workload combination. GPU-board plus CPU-package counters are not whole-system wall energy.
For sampled watts, energy_kwh = sum(power_w * interval_seconds) / 3,600,000. For cumulative
energy counters, account for wraparound and missing samples before deriving power.

### 6.6 CPU quota interpretation and limits of the new results

CPU quota / period = allocated CPU equivalents. Therefore 50,000 / 100,000 = **0.5 CPU**,
not 50% of every core. For example, half of an explicitly allocated 8-CPU allowance requires
4 CPU equivalents: quota 400,000 with period 100,000, subject to the actual node constraints.
This allocates CPU time, not 50% electrical power.

No new gaming/rendering profitability figure is justified yet. Required inputs remain
TODO(source): wall power and idle consumption, cooling-loop capture, render time and energy
per frame/scene, gaming concurrency and latency, customer demand, realised price, licensing,
and a physically buildable hardware bill of materials. The existing profiles' market prices
cannot be reused as demonstrated income for this new service.

Technical references (checked 2026-11-21):
- [Linux cgroup v2 CPU quota](https://docs.kernel.org/admin-guide/cgroup-v2.html).
- [NVIDIA power-limit semantics](https://docs.nvidia.com/deploy/nvidia-smi/).
- [NVIDIA WSL limitations](https://docs.nvidia.com/cuda/wsl-user-guide/index.html).

Verification: independently evaluated all arithmetic in PowerShell; checked dimensional units
and the reference formula against docs/case/challenge.md. This is a documentation correction,
not a change to config/hardware, the shared scheduler, scored figures or economic calculations.
