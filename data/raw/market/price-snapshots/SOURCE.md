# Azure VM price snapshots (Switzerland North)

Dated snapshots for the self-updating computing price (ticket T-28). Each file is the raw response
of one fetch, written by a script and **never overwritten** (a second fetch on the same day gets a
`-HHMMSSZ` suffix).

| | |
|---|---|
| **Source** | Azure Retail Prices API, `https://prices.azure.com/api/retail/prices` (public, no login). Docs: <https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices> |
| **Filter** | `serviceName eq 'Virtual Machines' and armRegionName eq 'switzerlandnorth' and priceType eq 'Consumption' and (armSkuName eq 'Standard_D32as_v5' or armSkuName eq 'Standard_D32s_v5')`, `currencyCode=USD`; built from `config/computing-price-method.json`. Pagination via `NextPageLink` is followed. |
| **Command** | `python3 scripts/fetch_data.py --prices` (or `python3 scripts/fetch_prices.py`) |
| **First snapshot** | `azure-switzerlandnorth-2026-11-21.json`, retrieved 2026-11-21 01:30 UTC, 14 items, 1 page |
| **Licence / terms** | Microsoft's public price API; Microsoft's terms of use apply. We store factual price points for analysis; no redistribution licence is claimed. |

## File format

`source`, `request_url`, `filter`, `currency`, `retrieved_at_utc`, `pages`, and `items`: the API's
items unchanged, sorted by SKU, product and meter for stable diffs. Relevant item fields:
`armSkuName` (VM size), `productName` (`… Windows` = Windows licence included; `… Cloud Services` =
a different product), `meterName` (`… Spot`, `… Low Priority`, or plain = pay-as-you-go),
`retailPrice` (USD per `unitOfMeasure`, here `1 Hour`), `effectiveStartDate`.

## Caveats

- **Retail list prices**, without discounts, reservations or savings plans; VAT not included.
- **Spot prices change over time** and can be evicted; a snapshot is one point in time. The
  2026-11-21 Linux values equal the WP1 values of 2026-11-20 in `../compute-prices.csv`.
- The API returns USD by default; CHF conversion uses the WP1 rate (0.8402 CHF/USD,
  `docs/research/ai-services.md`). Azure's own CHF billing rate is about 4 % lower (same note).
- The snapshot holds Windows, Low Priority and Cloud Services meters too (raw stays raw); the
  derivation selects the Linux Virtual Machines on-demand and Spot meters only.
- vCPU and RAM per size are not in the API; they come from `config/computing-price-method.json`.

## Regional comparison snapshot (T-37, 2026-11-21)

`azure-region-comparison-2026-11-21.json`: the same API, one query per region (`switzerlandnorth`,
`germanywestcentral`, `westeurope`, `northeurope`, `francecentral`), filter
`priceType eq 'Consumption' and serviceName eq 'Virtual Machines'` and the SKUs D32as v5, D32s v5,
F32s v2, NC24ads A100 v4 and NV36ads A10 v5, with `currencyCode=CHF` (Azure's billing conversion).
Items are unchanged and grouped by region. It is used for the Swiss-location premium rows
`cp-region-azure-*` in `../compute-prices.csv`. SKUs with more than one Linux meter per region were
skipped as ambiguous. Fetched ad hoc with Python `urllib` (command in `docs/research/profitability-inputs.md` §8).
