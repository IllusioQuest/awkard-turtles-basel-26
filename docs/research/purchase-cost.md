# R2: Purchase and lifetime cost of new hardware (T-37)

**Status: exploration, 2026-11-21. Not reviewed.** Raw values: [`data/raw/market/purchase-cost.csv`](../../data/raw/market/purchase-cost.csv)
(ids `pc-*`). Existing rows are referenced, not copied: `cost-server-dell-r570`,
`cost-gpu-pccom-rtx4090-system`, `power-gpu-nvidia-rtx4090-tgp` (`costs.csv`), `hw-power-nvidia-l4-tdp`,
`hw-cost-bechtle-pny-l4` (`hardware.csv`). Currency: USD to CHF 0.8402, EUR to CHF 0.9233
(`ai-services.md`). Used hardware is T-35 and not covered.

**Honest summary:** most Swiss shop pages did not load or showed no price, so many rows are search
snippets (confidence low, marked in notes). No complete, dense 0.5-1 kW server or a complete
GPU-host price with a wall-power measurement was found. The CHF per kW values below are therefore
indicative, with their gaps named.

## 1. Method

- CHF per kW = purchase price (CHF, excl. VAT where stated) ÷ electric power (kW).
- Power = component TDP/TGP only (the repo's convention, `hardware.md` §1). This **underestimates** draw
  (RAM, fans, PSU losses), so CHF per kW is an **upper bound** on cost per kW. Measured wall power: TODO(source).
- Depreciation per sold server-hour = CHF per kW ÷ lifetime years ÷ H, with H = 5,665 sold
  server-hours per server-year (`hardware.md` §1; 1 kW server, so 1 server-hour = 1 kWh).

## 2. Profiles

| Profile | Price (CHF) | Power (kW) | CHF per kW | CSV ids | Gap |
|---|---|---|---|---|---|
| Dense CPU: Dell R6615, 1x EPYC 9354P 32 cores | 7,188 | 0.28 (CPU TDP) | **25,671** | `pc-cpu-brack-r6615-9354p`, `pc-cpu-dell-epyc9354p-tdp` | both low (snippets); RAM and storage unknown; wall power TODO(source) |
| Existing cpu-server (six R570 boxes) | 50,743 | 1 | 50,743 | `cost-server-dell-r570` | for comparison |
| L4 host: 4 x L4 + R6615 host | 19,544 | 0.568 | **34,408** | `pc-gpu-bechtle-l4`, `pc-cpu-brack-r6615-9354p`, `hw-power-nvidia-l4-tdp` | whether 4 L4 fit the R6615 is TODO(source); 8-card chassis price TODO(source) |
| Existing gpu-inference-l4 | 34,244 | 1 | 34,244 | `hw-cost-bechtle-pny-l4` | used the older CHF 2,235 card price |
| RTX 4090 desktops (existing) | 16,300 | 1 | 16,300 | `cost-gpu-pccom-rtx4090-system` | existing |
| RTX 5090, card only | 3,187 | 0.575 | 5,543 | `pc-gpu-digitec-5090-gaming-oc`, `pc-gpu-nvidia-5090-tgp` | card only, no host; both rows low; no complete workstation price found |
| RTX PRO 6000 Blackwell, card only | 13,539 | 0.6 | 22,565 | `pc-gpu-bechtle-rtxpro6000`, `pc-gpu-nvidia-rtxpro6000-power` | card only; power not verified |

Worked examples:

- R6615: 7,188 ÷ 0.28 = 25,671 CHF per kW. Depreciation per sold server-hour, 6 years:
  25,671 ÷ 6 ÷ 5,665 = **CHF 0.755**; at 5 years 0.906. The existing cpu-server gives
  50,743 ÷ 6 ÷ 5,665 = 1.493, so a dense box halves the capital cost per kWh.
- L4 host: 4 × 3,089 + 7,188 = 19,544; power 4 × 0.072 + 0.28 = 0.568 kW; 19,544 ÷ 0.568 = 34,408 CHF per kW;
  ÷ 6 ÷ 5,665 = CHF 1.012. Note the L4 card now costs CHF 3,089 (opened Bechtle page), not 2,235 as in
  `hardware.csv`, so the repo's L4 profile is likely too cheap; its card price row is an older snippet.
- RTX 5090: 3,187 ÷ 0.575 = 5,543; a complete system adds host price (TODO(source)). Compare the 4090
  desktop at 16,300 per kW: a 5090 build is likely well below it, but this is not yet shown by a source.
- Consumer GPU power per CHF is the cheapest, but needs continuous-duty suitability (not verified).

Other dense CPU prices (not converted to CHF per kW because no TDP source): R7625 dual-socket EPYC 9334
CHF 14,576 (`pc-cpu-brack-r7625-9334`); Thomas-Krenn 1U EPYC 9124 EUR 3,985 net = CHF 3,679
(`pc-cpu-tk-ra1104-smepn`, entry 16 GB); Dell R7625 base CHF 6,989 excl. VAT (`pc-cpu-dell-ch-r7625-base`).

## 3. Cooling and heat-recovery integration

| Item | Value | Id |
|---|---|---|
| GPU water block, RTX 4090 FE (Alphacool) | EUR 77.90 incl. VAT = CHF 71.93 | `pc-cool-alphacool-4090-block` |
| Alternative block (EK-Quantum Vector2 4090) | EUR 159 incl. VAT = CHF 146.8 | in notes of the same row |
| Complete heater product, Qarnot QC-1 (2018) | EUR 2,900 = CHF 2,678, power unknown | `pc-cool-qarnot-qc1` |
| Heata unit | no price published | `pc-cool-heata-price` |
| Plate heat exchanger, pump, tubing | TODO(source) | `pc-cool-plate-hx-3kw` |
| Swiss plumber hourly rate | TODO(source) | `pc-cool-swiss-plumber-rate` |

Cloud&Heat, Deep Green and Windcloud were not researched to a published price within this session:
TODO(source). A GPU water block is cheap (about CHF 70-150 per card) relative to the card, so the
unknown part of the integration cost is plumbing labour and the heat-exchange loop, not the cold plates.

## 4. Lifetime and resale

- **Lifetime:** hyperscalers depreciate servers and GPUs over 5-6 years (`pc-life-hyperscaler-years`,
  Fortune, opened page, medium). Economic life of AI GPUs may be about 3 years (`pc-life-economic-years`, opinion,
  low). The repo's 6 years sits at the upper end of the accounting range; 5 years is a fair sensitivity
  (0.906 vs 0.755 CHF per sold server-hour for the R6615 above).
- **Resale:** only low-confidence US data: a used RTX 4090 sold at about 131 % of MSRP in Aug 2026 and
  69 % in April 2026 (`pc-resale-4090-used-median`), dealer trade-in about 50 % of the going rate
  (`pc-resale-4090-tradein`). These reflect GPU scarcity, not normal depreciation. Recommendation: keep
  residual value at 0 in the base case, and treat resale as a sensitivity owned by T-35.

## 5. Cheapest credible 1 kW heater (today's evidence)

Candidate: consumer GPU (RTX 5090 class) with water blocks. Sourced pieces: card CHF 5,543 per kW
(card only), water block about CHF 72-147 per card. Missing: host system, heat exchanger, labour. Of the *fully
priced* systems, the existing **RTX 4090 desktop profile (CHF 16,300 per kW)** is the cheapest; the dense
single-socket CPU server (about CHF 25,700 per kW, upper bound) is the cheapest fully priced CPU option.

## 6. Gaps

1. Wall-power measurement for all boxes (TDP-only power is the weak point).
2. Complete 5090 workstation price; L4 host slot count and 8-card chassis price.
3. Plate heat exchanger, pump, installation labour, Swiss hourly rate.
4. Published heater-server prices (Heata, Cloud&Heat, Deep Green, Windcloud, Qarnot current).
5. Digitec/Galaxus pages timed out; confirm snippet rows (`low`) on the live shop pages.
6. VAT treatment differs per row (Bechtle excl., Thomas-Krenn net, Digitec unclear).
