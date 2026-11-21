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
