# Market research sources

## WP2 costs and energy — first version

Collected by Codex (GPT-6 Astra) on 2026-11-20. `costs.csv` and `energy-prices.csv`
are manual research snapshots, using the market-research exception described in
[`data/README.md`](../../README.md) and the exact schema in
[`docs/research/README.md`](../../../docs/research/README.md).
Do not treat source guide ranges, components or minimum charges as complete quotations.

Each row identifies its original source, retrieval date, original unit, offer/profile,
geography, confidence and conversion. Money is CHF except the hourly spot-price example.
Non-monetary observations (power, years, efficiency) have an empty currency. `high`
means a directly published primary price/measurement; `medium` indicates a primary
source with an indicative price or unresolved comparability. It does not express
confidence in the profitability of a DigitalHeat installation.

`value` preserves the source's number. Decimal punctuation is standardised to a dot.
`normalised_value` converts only units/currency, never estimates a missing price.
Rappen are CHF cents, so divide by 100; prices per hundred litres divide by 100.
Do not sum rows indiscriminately: endpoints, alternatives, component specifications,
annual guide expenses and charges already included in totals coexist in these files.

### ElCom retail prices

Sources: [ElCom public website](https://www.strompreis.elcom.admin.ch/),
[GraphQL endpoint](https://www.strompreis.elcom.admin.ch/api/graphql), and
[LINDAS query endpoint](https://lindas.admin.ch/query).
CSV source URLs identify the individual LINDAS observations. Values use raw published
precision; the website/GraphQL response rounds them to two decimal places.

Submit the following SPARQL query to LINDAS with
`Accept: application/sparql-results+json` to retrieve the complete Basel series:

```sparql
PREFIX d: <https://energy.ld.admin.ch/elcom/electricityprice/dimension/>
SELECT ?s ?period ?total ?name WHERE {
  ?s d:operator <https://energy.ld.admin.ch/elcom/electricityprice/operator/624>;
     d:municipality <https://ld.admin.ch/municipality/2701>;
     d:period ?period;
     d:category <https://energy.ld.admin.ch/elcom/electricityprice/category/H4>;
     d:product <https://energy.ld.admin.ch/elcom/electricityprice/product/standard>;
     d:total ?total .
  OPTIONAL { ?s d:energyname ?name }
} ORDER BY ?period
```

There is no year filter. At retrieval the returned coverage is 2011–2027; earlier
years are absent, not zero. The final year is a published future tariff. The scored
case continues to use 2026. Basel's standard product is cross-checked against the
existing `data/raw/elcom/iwb-basel-h4-2026.json` snapshot.

For comparison, replace the operator/municipality with the pairs below, add
`PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>` and constrain
`?period` to `"2026"^^xsd:gYear`. Keep the standard-product constraint:

| Operator | Municipality | Provider / place |
|---|---|---|
| 486 | 243 | EKZ / Dietikon |
| 519 | 351 | Energie Wasser Bern / Bern |
| 565 | 261 | ewz / Zürich |
| 672 | 3203 | St.Galler Stadtwerke / St. Gallen |

Omitting the product filter can return both standard and cheapest offers. ElCom
LINDAS metadata labels `dimension/total` **Total excl. VAT**; check it with:

```sparql
SELECT ?p ?o WHERE {
  ?s <http://www.w3.org/ns/shacl#path>
     <https://energy.ld.admin.ch/elcom/electricityprice/dimension/total> .
  ?s ?p ?o
}
```

[EnergieSchweiz's guide, p. 16](https://pubdb.bfe.admin.ch/de/publication/download/9329)
also identifies ElCom calculator tariffs as excluding VAT. No VAT uplift is applied
to the case anchor. H4 is a standard household profile; a server installation needs
its actual supply contract and fixed/variable tariff breakdown. Metadata alone does
not establish how every historical fixed or metering charge is allocated.

### Spot electricity and exchange rate

Annual SwissIX means are directly published rounded CHF values in
[ElCom's report, p. 6](https://www.elcom.admin.ch/dam/de/sd-web/EVR8p5NjOTvA/Regelleistung%20und%20Regelenergie%202025.pdf).
They are not recomputed or converted with today's FX.

The hourly example uses the exact request
<https://api.energy-charts.info/price?bzn=CH&start=2025-01-15&end=2025-01-15>.
The API returns parallel `unix_seconds` and `price` arrays in EUR/MWh; timestamps
are written as Europe/Zurich local times with explicit offsets in `tier` and original
Unix timestamps in `notes`. The example is a winter day, not a representative annual
load-weighted price. Original data: Bundesnetzagentur / SMARD.de via Fraunhofer ISE
Energy-Charts, **CC BY 4.0**, as stated by the API response and
[API specification](https://api.energy-charts.info/openapi.json).

Hourly normalisation is EUR/MWh × **0.9232 CHF/EUR** ÷ 1000, using the
[SNB rate dated 2026-11-20](https://www.snb.ch/en/srv/current_interest_exchange_rates).
The rate/date/source are repeated in the affected CSV rows. This is a common snapshot
conversion for comparison, not historical settlement FX. Wholesale spot excludes
retail network charges, taxes, levies, metering and supplier margins. No runtime
fetching or scheduling model is introduced.

### Other costs, fuel and export compensation

Exact pages and offer conditions live in the CSV rows; the category tables and gaps
are in [`costs-and-energy.md`](../../../docs/research/costs-and-energy.md).
Prices are factual extracts, not republications of source pages. Retailer/manufacturer
and government source terms remain applicable; no blanket redistribution licence is
claimed for those pages. No full commercial price list or large dataset is copied.
For ElCom, follow the licensing metadata of the underlying dataset when redistributing
beyond these attributed factual observations.

Physical equipment and fuel rows describe the offer location, not a data-processing
location. For internet/monitoring services, processing geography is recorded only
where the provider states it; Swiss retail availability alone is not evidence of
Swiss-only processing. Obtain installation quotations and contract terms before
using these observations as a project budget.
