# Source: ElCom electricity price 2026, IWB Basel, H4

| Field | Value |
|---|---|
| File | `iwb-basel-h4-2026.json` (API response wrapped with query and retrieval time) |
| Provider | Swiss Federal Electricity Commission ElCom: https://www.strompreis.elcom.admin.ch |
| Endpoint | `https://www.strompreis.elcom.admin.ch/api/graphql` (the API behind the public website) |
| Query | operator `624` (IWB Industrielle Werke Basel), period `2026`, category `H4`; full query stored in the file |
| Retrieved | 2026-11-20 by `scripts/fetch_data.py` |
| Required by | Case, "Electricity price" ([`docs/case/challenge.md`](../../../docs/case/challenge.md)) |

## Values (Basel, also identical for Riehen and Bettingen)

| Component | Rp./kWh |
|---|---|
| **total** (value used by the case) | **33.25** |
| energy | 10.892 |
| grid usage | 11.414 |
| charges to the municipality/canton (`charge`) | 7.443 |
| federal grid surcharge (`aidfee`) | 2.3 |

The listed components add up to 32.049; the rest of the total (≈ 1.2 Rp.) is in components we did
not query (e.g. metering). Only `total` is used.

Derived for the reference house:
- electricity price: **0.3325 CHF/kWh** (one flat price, as the case says)
- value of used heat: 0.3325 ÷ 2.5 = **0.133 CHF/kWh**

## Caveats

- Category H4 is ElCom's standard household profile (5-room flat, 4,500 kWh/yr, electric stove and
  tumble dryer, no electric water heater).
- VAT treatment: verify on the website whether the published totals include VAT before quoting
  prices in the pitch.
- To check by hand: open the website, search "IWB", choose 2026 and category H4.

---

# Source: ElCom cantonal median electricity price 2026, H4, standard product

| Field | Value |
|---|---|
| File | `canton-median-h4-2026.json` (API response wrapped with query and retrieval time) |
| Endpoint | same GraphQL API, field `cantonMedianObservations` |
| Query | period `2026`, category `H4`, product `standard`; full query stored in the file |
| Retrieved | 2026-11-21 by `scripts/fetch_data.py` (`ELCOM_CANTONS`) |
| Used by | web dashboards (`web/src/params.ts`): electricity price per site canton in the mock network |

- `canton` is the BFS canton number (1 = Zürich … 26 = Jura).
- Without the `product` filter the API returns two rows per canton; the second is a cheaper
  product. We use the standard product. Check: Basel-Stadt median = 33.25 Rp./kWh, identical to
  the IWB value above.
- Same caveats as above (H4 profile, VAT treatment to verify).

## All municipalities, categories H1–H8, 2026 (T-47)

| Field | Value |
|---|---|
| Files | `municipalities-2026/H1.json` … `H8.json` (API response wrapped with query and retrieval time) |
| Query | `observations(period: "2026", category: [Hn])`, fields operator, municipality, canton, `coverageRatio`, `total` (Rp./kWh) |
| Retrieved | 2026-11-21 by `scripts/fetch_elcom_municipalities.py` |
| Derived | `data/derived/elcom/price-per-municipality-2026.csv`: one row per municipality and category; several operators per municipality → coverage-weighted mean (`coverageRatio`), plus min/max; CHF/kWh |
| Coverage | 2,123 municipalities, ≈ 2,970 operator–municipality rows per category |
| Check | Basel (BFS 2701) H4 = 0.3325 CHF/kWh, as in the case |

Categories are ElCom's standard household consumption profiles ("house types"). The only one with a
source so far: **H4 = 4,500 kWh/a** (ElCom FAQ, https://www.elcom.admin.ch/de/faq-stromtarife).
Annual consumption and dwelling description for H1–H3 and H5–H8: TODO(source) (the ElCom website
renders them with JavaScript; LINDAS query timed out on 2026-11-21).

Medians over municipalities 2026, CHF/kWh: H1 0.352, H2 0.310, H3 0.273, H4 0.278, H5 0.258,
H6 0.234, H7 0.250, H8 0.264. Yearly flat tariffs only (no time-of-day prices).
