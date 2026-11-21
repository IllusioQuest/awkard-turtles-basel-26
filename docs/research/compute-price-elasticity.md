# Competitor prices and price elasticity for Type 2 dynamic pricing (T-58)

> Research agent (Claude subagent), 2026-11-21, all accessed 2026-11-21. Feeds
> `docs/model/fleet-simulation.md` §7.1. Rows for `data/raw/market/compute-prices.csv`: `TODO`
> (not yet appended).

## Conversion to CHF per 1 kW server-hour

The method is `config/computing-price-method.json`: 3.74 W per vCPU plus 0.392 W per GB of RAM, and
USD→CHF 0.8402. One CSCS MultiCore node (2× EPYC 7742, 256 threads) counts as ≈ 1.06 kW:
256 × 3.74 W + 256 GB × 0.392 W. The 256 GB of RAM is an assumption, not verified.

## Reference prices

| Source | Quoted | CHF per kW-h | Confidence |
|---|---|---|---|
| CSCS academic subscription (min. CHF 50k/yr, from Jan 2025) | CHF 0.49 per node-hour | 0.46 | high |
| CSCS academic pay-per-use | CHF 1.04 / 0.87 / 0.69 per node-hour (< 10k / 10–50k / > 50k node-h) | 0.65–0.98 | high |
| CSCS industry pay-per-use | CHF 1.77 / 1.48 per node-hour | 1.40–1.67 | high |
| EPFL SCITAS | CHF 0.0056 per core-hour; GPU CHF 0.41 per hour | ≈ 0.55–1.05 | low (snippet) |
| AWS Spot Advisor eu-central-2 | c7i.8xlarge saving 69 %; median saving 72 % over 354 Linux types | spot = 28–38 % of on-demand | high |
| Salad (consumer PCs) | USD 0.005 per vCPU-h + USD 0.001 per GB-h | ≈ 1.30–1.43 | medium |
| ETH Euler, sciCORE | not published | – | – |

Sources:
- CSCS: https://indico.psi.ch/event/17625/contributions/57140/attachments/31855/63544/CSCS_Presentation.pdf
- SCITAS: https://www.epfl.ch/research/facilities/scitas/?p=280
- AWS: https://spot-bid-advisor.s3.amazonaws.com/spot-advisor-data.json
- Salad: https://getdeploying.com/salad and https://docs.salad.com/docs/billing

## Price elasticity of demand

All three values come from one secondary source, Hull & Sattath, arXiv:2611.13131 (2026), p. 2
(https://arxiv.org/pdf/2611.13131). None of the originals was read.

| Study | Value | Context |
|---|---|---|
| Jiang, Lee, Zomaya 2016, IEEE Cloud Computing 3(1), doi:10.1109/MCC.2016.14 | 1.2 | early AWS EC2 demand |
| Jin, Peng, Wang 2026, SSRN 4573326 | 0.629–0.942 | own-price elasticity, VM groups |
| Brand, Castillo, Lohani, Musolff 2025 (working paper) | ≈ 0.5 | spot / on-demand setting |

No elasticity for HPC allocation pricing was found.

## Spot price variation over time

Baughman et al., ScienceCloud'19 (https://par.nsf.gov/servlets/purl/10119475), on AWS after the
2017 pricing change:
- Price SD is 7.9 % of the mean (87.9 % before the change).
- Prices persist about 85× longer than before.
- No daily or weekly pattern remains.

No seasonal data was found.

## Defaults used

- `p_ref` = CHF 0.82 per kW server-hour: CSCS academic pay-per-use, 10–50k node-hours. Range 0.46–1.80.
- Weekly noise SD 7.9 %.
- ε = −0.8, range −0.5 to −1.2, used in constant-elasticity form `Q = Q₀ (p / p_ref)^ε`. The
  functional form is our choice and is not sourced.
- **Caveat:** none of these values measures Swiss research batch compute.
