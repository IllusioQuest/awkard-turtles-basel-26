# Refurbished hardware: cost and wear (T-35)

**Status: exploration, first version (2026-11-21).** Not reviewed by the team. Scored
reference-house figures keep the case's abstract 1 kW server; everything here is a **variant**.
The team chose second-life servers and GPUs (lever D). This note checks what that lever does to the
cost per server-hour once shorter life, failures and lower work per kWh are counted.

**How the sources were read.** Most hosts were blocked in this session (e.g. `servermall.com`
does not resolve). All external figures come from **search-result snippets** of the named pages,
accessed **2026-11-21**, unless the row says otherwise. Confidence: **high** = primary page or
several sources agree; **medium** = one credible source, or a snippet quoting the primary page;
**low** = a single listing, a secondary source, or sources disagree. **[A]** marks our own
assumption. USD → CHF 0.8402 and EUR → CHF 0.9233, as in [`ai-services.md`](ai-services.md).

Files: profiles [`config/hardware/*-refurbished.json`](../../config/hardware/) (T-15 format,
loaded and checked by `HardwareProfile`); wear parameters
[`config/hardware/wear/refurbished-wear.json`](../../config/hardware/wear/refurbished-wear.json);
new raw rows `hw-used-*`, `hw-refurb-*` in [`data/raw/market/hardware.csv`](../../data/raw/market/hardware.csv).
Every table in §5 and §6 is printed by `python3 scripts/refurbished_comparison.py`.

## 1. Short answer

- **Per 1 kW heater, refurbished is cheaper.** Full-cost break-even with heat used, including
  expected failures: refurbished T4 server **CHF 1.12**, refurbished CPU **CHF 1.19** per sold
  server-hour. The new profiles are L4 **CHF 1.83** and CPU **CHF 2.67**. The L4 figure here is
  higher than the 1.36 in [`hardware.md`](hardware.md) because wear is now counted.
- **Per unit of work, refurbished is not cheaper.** Older chips do less work per kWh: T4 about 0.4
  of an L4, a 2019/2020 Xeon about 0.3 of a 2024 Xeon. Per new-equivalent unit of work, the
  break-even is T4 **2.81** against new L4 **1.83**, and old CPU **4.06** against new CPU **2.67**.
  Refurbished wins only where buyers pay per GPU-hour or vCPU-hour rented, not per job done.
- **A used L4 saves almost nothing (1.80 against 1.83).** The L4 is the current generation. The
  one used sale we found was 53 % of the new price, but the remaining life is also about half, and
  used asking prices are above the new price.
- **Thermal cycling is not a problem if the scheduler limits starts.** In the reference run a
  server would start about 10 times a year, which uses 0.08 % of its solder fatigue life. Hourly
  on/off would use about 73 % per year. A scheduler rule (at most about 1 start per day, throttle
  instead of switching off) makes this risk go away.
- **Hot water (50–65 °C) is probably not worse than an air-cooled datacentre** (similar die
  temperatures, steadier). This is not proven; at sensitivity ×2 the break-even rises by
  CHF 0.07–0.40.
- **Biggest uncertainty:** the GPU failure rate. Our 9 %/year comes from 700 W H100s training at
  full load, which is an upper bound. No GPU failure rate by age is published. With 3 %/year, every
  GPU break-even falls by CHF 0.12–0.30.

## 2. Purchase prices of used and refurbished hardware

| Item (power) | Used / refurbished price | CHF | CHF per kW of card | New for comparison | Conf. |
|---|---|---:|---:|---|---|
| NVIDIA **T4** 16 GB (70 W) | USD 510–580 realised over 12 months to Aug 2026, flat ([AMCompute](https://www.amcompute.com/appraisals/tesla-t4)); eBay asks USD 430–700 ([eBay](https://www.ebay.com/shop/nvidia-t4)) | **458** (mid) | 6,542 | legacy part | medium |
| NVIDIA **L4** 24 GB (72 W) | one completed eBay.de sale USD 1,400, scratched ([eBay.de](https://www.ebay.de/itm/157127945920)); used ask USD 2,898 ([eBay](https://www.ebay.com/itm/236238919558)) | **1,176** | 16,337 | CHF 2,235 Bechtle (`hw-cost-bechtle-pny-l4`, low) | low |
| NVIDIA **A2** 16 GB (60 W `TODO(source)`) | USD 439 used; USD 799–1,040 refurbished, 1-year warranty ([eBay](https://www.ebay.com/itm/176993050489), [Newegg](https://www.newegg.com/nvidia-tesla-a2-graphics-card/p/1WK-00TY-00082)) | 369–874 | 6,150–14,560 | – | low |
| NVIDIA **A10** 24 GB (150 W `TODO(source)`) | USD 1,795–1,900 used asks; USD 3,238 refurbished ([eBay](https://www.ebay.com/itm/156770832547), [Newegg](https://www.newegg.com/p/1WK-00TY-00040)) | 1,554 | 10,360 | – | low |
| NVIDIA **V100** PCIe 32 GB (250 W `TODO(source)`) | USD 760–830, −40 % in a year ([AMCompute](https://www.amcompute.com/appraisals/tesla-v100-pcie-32gb)) | 668 | 2,670 | – | medium |
| NVIDIA **A100** PCIe 40 GB (250 W `TODO(source)`) | typical realised sale USD 3,207; appraisal USD 4,200–5,000 (−30 %/yr); asks USD 6,000–6,500 ([AMCompute](https://www.amcompute.com/appraisals/a100-pcie-40gb), [aliteq](https://aliteq.com/a100-price)) | 2,695 | 10,780 | – | low |
| **RTX 3090** 24 GB (350 W `TODO(source)`) | ≈ USD 1,010 eBay average, range 820–1,900 ([analysis](https://dev.to/jovan_chan_9500711396d4e6/used-rtx-3090-in-2026-still-the-ai-value-king-or-time-to-move-on-32ie)); EU 12-month low EUR 900, unconfirmed ([gpuprix](https://gpuprix.com/eu/gpus/geforce-rtx-3090)) | 849 | 2,425 | – | low |
| **RTX 4090** 24 GB (450 W) | USD 2,100–3,000 eBay sold, rising Aug→Oct 2026 ([tech-insider](https://tech-insider.org/rtx-4090-vs-rtx-5080-used-market-2026/), trackers) | 2,135 | 4,744 | used is above the launch price | low |
| **Dell R740**, 2 × Gold 6246R, 256 GB (≈ 0.51 kW) | USD 6,770 refurbished ([Newegg](https://www.newegg.com/dell-poweredge-rack-mount/p/2NS-0008-71SB5)) | **5,688** | 11,146 | R570 CHF 8,248 for 0.16 kW | low |
| Dell T640, 2 × Gold 6248, 256 GB (≈ 0.40 kW) | EUR 2,910 used ([eBay.de](https://www.ebay.de/itm/187727590188)) | 2,687 | 6,711 | – | low |
| Dell R750, refurbished | from EUR 2,621 excl. VAT, base configuration unknown (Servermall snippet) | 2,420 | ? | new R750 from CHF 4,369 excl. VAT ([Dell CH](https://www.dell.com/de-ch/shop/enterpriselösungen/poweredge-r750-rack-server/spd/poweredge-r750/per7501a)) | low |

**Warranty terms of refurbishers.** Servermall advertises a 5-year EU warranty with on-site repair
([servermall.com/about](https://servermall.com/about/)). Bargain Hardware gives 3 years
return-to-base. Used GPU cards from marketplaces mostly have none, or 30 days to 1 year (ServerMonkey
A10: 30 days; refurbished A2: 1 year). ITRenew (now Iron Mountain) sells decommissioned hyperscale
racks "recertified, warrantied" at about 50 % of the original price and claims about 50 % lower TCO
([Datamation](https://www.datamation.com/data-center/it-renewal-and-implementing-a-data-center-circular-economy)),
vendor claims, medium. All of these come from snippets; read the terms before buying.

**Pattern.** Prices fall fast for generations that have a successor (V100 −40 %, A100 −30 % per
year). They do not fall for the current one (L4) or for consumer cards that AI hobbyists want
(RTX 4090 above its launch price). **Second-life value therefore sits in the previous generation**
(T4, V100, A100, Cascade Lake servers), which is also the generation with the worst work per kWh.

## 3. Wear and degradation: what the evidence says

| Topic | Finding | Source | Conf. |
|---|---|---|---|
| GPU failure rate | Llama 3 405B: 16,384 H100s over 54 days, 419 unexpected interruptions; 148 GPU + 72 HBM3. Annualised **≈ 9 %/yr** (TrendForce); own check 220 ÷ 16,384 × 365 ÷ 54 = 9.1 %. These are interruptions, not all replacements, on 700 W cards in training | [TrendForce](https://www.trendforce.com/news/2024/10/31/news-datacenter-gpus-may-have-an-astonishingly-short-lifespan-of-only-1-to-3-years), [Tom's Hardware](https://www.tomshardware.com/tech-industry/artificial-intelligence/faulty-nvidia-h100-gpus-and-hbm3-memory-caused-half-of-the-failures-during-llama-3-training-one-failure-every-three-hours-for-metas-16384-gpu-training-cluster) | medium |
| GPU availability | NCSA Delta, 2.5 years, 11.7 M GPU-hours: per-node availability 99.4 % (A100), 99.3 % (H100); hardware errors dominate on A100 | [arXiv 2503.11901](https://arxiv.org/html/2503.11901v4) | medium |
| GPU life "1–3 years" | anonymous Alphabet architect, unverified, at 60–70 % utilisation; disputed (Google runs 8-year-old TPUs; > 95 % of GPUs alive after 3 years in one cluster). It is an **economic** life rather than a physical one | [Tom's Hardware](https://www.tomshardware.com/pc-components/gpus/datacenter-gpu-service-life-can-be-surprisingly-short-only-one-to-three-years-is-expected-according-to-unnamed-google-architect), [Goedecke](https://seangoedecke.com/ai-gpus-live-longer-than-three-years) | low |
| Server failure rate | Microsoft fleet: 8 % of servers had a hardware repair, those averaged 2 repairs; 70–78 % disks, ≈ 5 % RAID, 3–5 % memory; a server that failed once is likely to fail again | [Vishwanath & Nagappan, SoCC 2010](https://www.microsoft.com/en-us/research/publication/characterizing-cloud-computing-hardware-reliability/) | medium |
| Ageing curve | Drives: flat failure rate for most of the life, then a wear-out rise (bathtub); 2025 fleet AFR 1.36 % | [Backblaze 2025](https://www.backblaze.com/blog/backblaze-drive-stats-for-2025/) | medium |
| Performance loss with age | none found as a published throughput loss; air-cooled cards lose cooling (fans, paste), which liquid cooling avoids **[A]** | `TODO(source)` | – |
| Thermal cycling | the main thermomechanical failure driver (solder fatigue); Coffin–Manson / Norris–Landzberg, exponent n = 1.9 (SnPb) or 2.65 (SAC); example package: 4,535 cycles to 50 % failure at 0/100 °C | [Wikipedia: solder fatigue](https://en.wikipedia.org/wiki/Solder_fatigue), [TI E2E](https://e2e.ti.com/support/data-converters-group/data-converters/f/data-converters-forum/730473/solder-joint-reliability-acceleration-model), [govdocs PDF](https://corpora.tika.apache.org/base/docs/govdocs1/549/549854.pdf) | low–medium |
| Running hot (warm water) | SuperMUC (LRZ, inlet up to 50 °C, outlet ≈ 60 °C): board failures reported at about **half** the rate of air-cooled, attributed to steady temperatures; ≈ 5 % more CPU power at the warm-water point | [DCD](https://www.datacenterdynamics.com/en/analysis/getting-into-hot-water/), [HPCwire](https://www.hpcwire.com/?p=47689), [IEEE Spectrum](https://spectrum.ieee.org/new-tech-keeps-data-centers-cool-in-warm-climates) | low (claim unverified) |
| Efficiency old vs new GPU | L4 ≈ 2.5 × T4 generative-AI performance at the same 70–72 W (NVIDIA); ResNet FP16 ≈ 2.3 × (arXiv 2604.14552); ≈ 2.9 × TFLOPS/W (Clarifai) | [nvidia.com/l4](https://www.nvidia.com/en-us/data-center/l4/), [arXiv 2604.14552](https://arxiv.org/html/2604.14552), [Clarifai](https://www.clarifai.com/blog/t4-vs-l4) | medium |
| Efficiency old vs new CPU | SPECpower_ssj2008 overall ssj_ops/W: 6,006 (2 × Gold 6226R, Cascade Lake Refresh) vs 20,408 (2 × Platinum 8592+, 2024) → 0.29. Configurations differ | [spec.org 2022q2](https://spec.org/power_ssj2008/results/res2022q2), [2024q2](https://spec.org/power_ssj2008/results/res2024q2) | low |
| Idle power | T4 36 W idle, 74 W full (one card, ServeTheHome); L4 idle not found | [STH](https://servethehome.com/nvidia-tesla-t4-ai-inferencing-gpu-review/5) | medium |

**What the efficiency loss means for us.** A server-hour always gives 1 kWh of electricity and
0.8 kWh of heat, whatever the chip. An older chip does less work in that kWh. **Heat per unit of
work goes up** (T4: 2.5 × the heat per transcribed hour), and **income per kWh goes down**, if the
buyer pays per job. For a heat-led system the heat is welcome. The loss of income is what hurts.

## 4. Wear model

Per 1 kW server and calendar year (parameters in `refurbished-wear.json`):

```
k            = k_age (refurbished parts only) × k_temp × k_cycle
W [CHF/yr]   = n_GPU  × AFR_GPU × k × (card price + 1 h × CHF 160)
             + n_box  × R_server × k × (1.5 h × CHF 160 + 0.10 × box price)
C [CHF/h]    = electricity + purchase ÷ life ÷ H + ops ÷ H + W ÷ H          (H = 5,665 sold h/yr)
B            = C − heat value 0.106                                         (full-cost break-even)
B_work       = B ÷ E                                                         (E = work per kWh vs new)
```

| Parameter | Value | Basis |
|---|---:|---|
| AFR_GPU | 0.09 per GPU-year | Llama 3 / TrendForce, upper bound (sensitivity 0.03) |
| R_server | 0.16 repairs per server-year | Vishwanath 2010: 8 % × 2 repairs; window taken as 1 year `TODO(source)` |
| Labour | CHF 160/h | `cost-ops-brentford-labour` |
| Repair hours, part share | 1 h per GPU, 1.5 h per server, part = 10 % of box | **[A]** `TODO(source)` |
| k_age (refurbished) | 1.5 (sensitivity 1–3) | **[A]**: bathtub wear-out; no GPU data by age `TODO(source)` |
| k_temp (hot water) | 1 (sensitivity 2) | **[A]**: die temperature near air-cooled DC; ×2 = Arrhenius for +10 K at ≈ 75 °C with Ea 0.7 eV `TODO(source)` |
| k_cycle | 1 | derived below, **valid only with a start limit** |
| Remaining life | 3 years used parts, 6 years new host | **[A]**: refurbisher warranty 3–5 years; 2018–2020 hardware is then 8–10 years old |
| Residual value | 0 | as in `hardware.md` |

Parts are always paid by us (conservative). A refurbisher warranty would remove part cost for
servers during its term. A lost hour of heat during a repair is covered by the backup heater and is
not costed.

**Thermal cycling with heat-led operation.** Cycles to failure scale as
`N_f(ΔT) = N_ref × (ΔT_ref ÷ ΔT)^n`. With N_ref = 4,535 cycles at ΔT = 100 K and n = 1.9, a full
on/off cycle of ΔT ≈ 60 K **[A]** (die ≈ 80 °C on, room temperature off) gives
N_f ≈ 11,970 cycles. The reference run (`data/derived/reference-house-v1`, 4 servers, minimum load
0.156 per server) throttles and never switches off. Even if its load were realised by switching
whole servers, that would be **9.75 starts per server-year → 0.08 % of fatigue life per year**.
One start per day uses 3 %, and hourly on/off uses 73 % per year. **Rule for the scheduler (T-12):**
throttle, keep a minimum load, and allow at most about one cold start per day. With that rule
k_cycle = 1. The same rule is needed anyway for idle power: a T4 server draws 0.46 kW idle, so
in non-heating hours it must be switched off for long periods, not toggled.

## 5. Comparison per 1 kW server

Output of `python3 scripts/refurbished_comparison.py` (2026-11-21). `cpu-server` and
`gpu-inference-l4` are the T-15 profiles, now with wear added. Both refurbished GPU profiles keep a
**new** R570 host, so that only the GPU effect is measured.

| Per 1 kW server | `cpu-server` | `cpu-server-refurbished` | `gpu-inference-l4` | `gpu-inference-l4-refurbished` | `gpu-inference-t4-refurbished` |
|---|---:|---:|---:|---:|---:|
| Purchase price per 1 kW server (CHF) | 50,743 | 11,146 | 34,244 | 21,930 | 13,727 |
| Life used for depreciation (years) | 6.00 | 3.00 | 6.00 | 3.69 | 4.29 |
| Depreciation per sold server-hour (CHF) | 1.493 | 0.656 | 1.007 | 1.048 | 0.565 |
| Operations per sold server-hour (CHF) | 0.769 | 0.245 | 0.125 | 0.125 | 0.125 |
| Expected wear (failures) per sold server-hour (CHF) | 0.185 | 0.067 | 0.473 | 0.400 | 0.206 |
| Electricity per server-hour (CHF) | 0.333 | 0.333 | 0.333 | 0.333 | 0.333 |
| **Cost per sold server-hour incl. wear (CHF)** | 2.779 | 1.300 | 1.938 | 1.906 | 1.229 |
| **Full-cost break-even, heat used (CHF per server-hour)** | 2.673 | 1.194 | 1.831 | 1.799 | 1.122 |
| Work per kWh relative to new (E) | 1.00 | 0.29 | 1.00 | 1.00 | 0.40 |
| **Break-even per new-equivalent unit of work (B / E)** | 2.673 | 4.061 | 1.831 | 1.799 | 2.806 |
| Rental income at spot, per GPU/vCPU-hour (CHF per server-hour) | 1.80 | 1.20 | 2.23 | 2.23 | 3.19 |
| Work-equivalent income at spot (E x new spot) | 1.80 | 0.53 | 2.23 | 2.23 | 0.89 |
| Work-equivalent income at home-host price (E x new home-host) | 0.68 | 0.20 | 4.30 | 4.30 | 1.72 |

Sensitivity of the break-even (CHF per sold server-hour, heat used):

| Case | `cpu-server` | `cpu-server-refurbished` | `gpu-inference-l4` | `gpu-inference-l4-refurbished` | `gpu-inference-t4-refurbished` |
|---|---:|---:|---:|---:|---:|
| central | 2.673 | 1.194 | 1.831 | 1.799 | 1.122 |
| k_age = 3 | 2.673 | 1.261 | 1.831 | 2.170 | 1.299 |
| k_temp = 2 | 2.858 | 1.261 | 2.304 | 2.200 | 1.329 |
| k_age = 3 and k_temp = 2 | 2.858 | 1.395 | 2.304 | 2.940 | 1.681 |
| refurbished life 2 years | 2.673 | 1.522 | 1.831 | 2.202 | 1.283 |
| GPU AFR 0.03 instead of 0.09 | 2.673 | 1.194 | 1.536 | 1.552 | 1.005 |

**How to read it.** Each column is the same 1 kW heater. "Break-even" is the price per server-hour
that covers electricity, depreciation over the remaining life, operations and expected repairs,
minus the heat value. If a buyer pays per GPU-hour or vCPU-hour, compare the break-even with the
"rental income" row. If the buyer pays per job (transcription, a filler batch), compare
"break-even per unit of work" with the new profile's income, or the break-even with the
"work-equivalent income" rows.

- `gpu-inference-t4-refurbished` is the cheapest heater (1.12). At today's T4 **rental** prices
  (AWS Frankfurt spot, 3.19) it pays. Per unit of work it earns only 0.89 at spot (below 1.12) and
  1.72 at the home-host price (above). The T4 rental price is a legacy price and is likely to fall
  (the T4 is a "legacy part" per one weak source). The rental row is the optimistic case.
- `cpu-server-refurbished` halves the CPU cost per heater-hour (2.67 → 1.19), mostly because the
  T-15 CPU profile is built from a small 8-core box. With 0.29 of the work per kWh it is the worst
  per unit of work (4.06). It is only attractive when sold per vCPU (1.20 spot ≈ 1.19 break-even).
- `gpu-inference-l4-refurbished` ≈ new L4. Half the card price is cancelled by half the remaining
  life. It is worse than new as soon as the age factor exceeds 1.5.
- **Wear matters for GPUs.** At 9 %/yr, expected card replacement adds CHF 0.21–0.47 per sold
  server-hour; for the new L4 that is more than its electricity. Cheap used cards make each failure cheap, which is the
  strongest argument for second-life GPUs.

**Worked example (`gpu-inference-t4-refurbished`).** Cards = (1 − 0.1625) ÷ 0.070 = 11.964.
Purchase = 11.964 × 458 + 8,248 = CHF 13,727. Depreciation = 5,479 ÷ 3 + 8,248 ÷ 6 = 3,201 per
year ÷ 5,665 = 0.565. Ops = 708 ÷ 5,665 = 0.125. Wear = 11.964 × 0.09 × 1.5 × (458 + 160) +
0.16 × 1 × (240 + 825) = 998 + 170 = CHF 1,168 per year ÷ 5,665 = 0.206. Cost = 0.3325 + 0.565 +
0.125 + 0.206 = **1.229**; minus heat 0.106 → **1.122**; per L4-equivalent work ÷ 0.4 = **2.81**.

## 6. Who already does it

- **Qarnot** moved its QBx digital boilers to **recertified OCP servers from ITRenew** in 2022;
  founder: "we favor recertified servers since they are very performant (and available)"
  ([DCD](https://datacenterdynamics.com/en/news/qarnot-upgrades-its-digital-boiler-with-itrenews-refurbished-ocp-hardware)).
  QBx: 12 OCP servers, about 95 % heat as water up to 65 °C; moving to new servers meant
  redesigning the cold plate ([CORDIS](https://cordis.europa.eu/project/id/190117429/reporting)).
  Later MiTAC supplied 132 (new) OCP AMD servers for a 100 kW Qarnot site
  ([MiTAC](https://www.mitaccomputing.com/en/successcase/Rethinking_Sustainable_HPC_Clusters_in_Europe)).
  No prices published. Qarnot went into receivership in 2026 ([`competitors.md`](competitors.md)).
- **ITRenew / Sesame** (now Iron Mountain): decommissioned hyperscale racks resold at about 50 % of
  the original price, used "for several more years"; also installed at **Hydro66** (Sweden) and
  through Circle B in Amsterdam ([DCD](https://datacenterdynamics.com/en/news/hydro66-installs-used-hyperscale-hardware-itrenew),
  [DCK](https://www.datacenterknowledge.com/hyperscalers/hyperscalers-used-gear-gets-a-second-life-in-smaller-data-centers)).
  Sims Lifecycle sells remanufactured OCP racks ([Sims](https://www.simslifecycle.com/blog/2024/rack-renew-ocp)).
- Lenovo Value Recovery sells refurbished servers ([Lenovo](https://lenovo.com/us/en/servers-storage/solutions/value-recovery)).
  Secondary-market server resale values rose in 2026 with AI demand
  ([Resource Recycling](https://resource-recycling.com/e-scrap/2026/01/22/server-resale-values-surge-in-ai-driven-secondary-it-markets/)),
  so cheap supply is not guaranteed.

## 7. Check of `creative-ideas.md` idea #5 ("GPU retirement home")

The idea assumes used GPUs at **50 % of the new price [A]** and computes an L4 break-even of
≈ CHF 0.86 (from 1.36).

- **The price assumption roughly holds for an L4, but only on thin data.** The one used sale is
  53 % of the new Bechtle price; used asks are above the new price. For previous generations the
  discount is much larger: a T4 costs about 20 % of a new L4 per card, and a V100 about 2,670 CHF
  per kW of card.
- **The conclusion does not hold.** The 0.86 keeps the 6-year life and ignores wear. A second-life
  card has about 3 years left **[A]**, so depreciation per year barely changes (1.05 used against
  1.01 new). Counting failures, a used L4 breaks even at **1.80 against 1.83 new**, not 0.86.
- **The mechanism of the idea does hold for old generations.** "Low-utilisation sites get
  low-capital hardware" is right for the *heater* cost: a T4 server is 1.12 against 1.83. Older
  GPUs earn less per kWh, though (0.4 ×), so the idea pays only with buyers who rent per GPU-hour,
  or with work whose value does not depend on speed (deadline-flexible batch).

## 8. Recommendation (for the team to decide)

1. **Pitch line:** "second-life hardware makes the heater cheap; the scheduler makes it last".
   Show the T4 column next to the new L4: 1.12 against 1.83 per heater-hour. Say honestly that per
   unit of work the new L4 is still cheaper.
2. **Buy the previous generation, not used current-generation cards.** Used L4s save nothing.
   T4/A2-class 70 W cards (or a mix with the host CPU) give the cheapest heat per CHF. Used RTX 4090s
   sell above their launch price.
3. **Scheduler rule (T-12 / T-05):** at most about one cold start per server per day, throttle
   instead of switching. This keeps thermal-cycling wear negligible. Enforce it in the shared
   scheduler, not only in the demo.
4. **Economics (T-14):** add expected wear (`W` above) to every profile, new ones included. It is
   of the same size as electricity for GPU servers.
5. Keep a refurbished host as the next variant. A used dual-socket box at CHF 1,100–2,700 instead of
   the new R570 (CHF 8,248) would cut the T4 break-even further (`TODO(source)`: a sourced quote for a
   GPU-capable refurbished host).

## 9. Open `TODO(source)`s and limits

- GPU failure rate **by age**, and for 70 W inference cards (we use H100 training data).
- Exact observation window of Vishwanath 2010 (repairs per server-year).
- Repair hours and part share; refurbisher warranty terms (full T&C, Swiss coverage).
- More than one used-L4 sale; a Swiss refurbisher quote in CHF (none found, servermall.com blocked).
- TDP of A2, A10, V100, A100, RTX 3090 (only used in §2 for CHF per kW); the T4 datasheet.
- Measured T4 throughput for our transcription job (T4 value = L4 × 0.4); matched SPECpower
  configurations for the CPU efficiency factor.
- Die temperature of liquid-cooled GPUs at 50–65 °C water; activation energy for the temperature
  factor; the LRZ "half the failures" claim.
- Retrofitting cold plates on air-cooled refurbished servers and cards (cost and the 80 % recovery).
- Idle power of refurbished CPU servers (we reuse the new-server ratio).
