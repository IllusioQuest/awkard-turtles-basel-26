# T-37: Profitability research: power, purchase cost, capacity, demand, trends

- **Tier:** 2 Pitch  ·  **Area:** Research / economics inputs
- **Depends on:** T-15 (hardware profiles), T-26 (job demand), decision 0018 (two job types)
- **Suggested owner:** Ennio  ·  **Owner:** Ennio / Claude  ·  **Status:** in progress

## Goal

Sourced inputs, with the calculations shown, for the question "is this profitable, per house and
as a network?". Basel-Stadt is the main case, Switzerland the scale-up, and the horizon is 2030.
Feeds T-14 (economics) and T-34 (business model v2); no second economic model.

## Packages (one document + one CSV each, schema of `docs/research/README.md`)

- [ ] R1 Power and efficiency per job: server wall power (load, idle, paused), GPU energy per audio
      minute and per 1M tokens, pumps and water cooling → `power-efficiency.md`, `data/raw/market/power-efficiency.csv`
- [ ] R2 Purchase and lifetime cost: dense CPU and GPU servers (new), water cooling, installation,
      lifetime, resale → `purchase-cost.md`, `purchase-cost.csv` (refurbished hardware stays in T-35)
- [ ] R3 Capacity from real households: building stock of Basel-Stadt and CH (GWR), heating
      systems, heat and hot-water demand per type → network capacity in server-hours per year → `household-capacity.md`, `household-capacity.csv`
- [ ] R4 Demand for compute today and to 2030: Type 1 (transcription, LLM tokens), Type 2
      (research HPC, finance Monte Carlo, batch/spot market), seasonality → `compute-demand.md`, `compute-demand.csv`
- [ ] R5 Trends to 2030: performance per W, hardware prices, price per token, electricity prices → `trends-2030.md`, `trends-2030.csv`
- [ ] Synthesis `profitability-inputs.md`: parameter table and two worked calculations (profit per
      house and year; network capacity vs. demand)

## Done when

- Every number has a CSV row with URL, access date and confidence, and every derived number has
  a written calculation with a worked example.
