# Deep-dive notes, structured (interpretation)

> This is the team's **interpretation** of [`notes-verbatim.md`](notes-verbatim.md). Where it and
> the verbatim notes disagree, the verbatim notes win. Where it and the case
> ([`../case/challenge.md`](../case/challenge.md)) disagree, the case wins for anything scored.
> Items marked *(interpretation)* go beyond what was literally said. Items marked *(to verify)* need a
> source before they are used in numbers or the pitch.

## 1. Physical and building constraints

- **Legionella.** The case owner flagged Legionella bacteria as a concern for hot water.
  *(interpretation)* Domestic hot water storage usually has to reach high temperatures regularly
  (often cited: around 60 °C) to prevent Legionella growth. Server waste-heat temperatures may not
  reach that on their own. *(to verify: Swiss guidance, e.g. SIA / BAG, and realistic outlet
  temperatures of liquid-cooled servers.)* This puts pressure on the case's simplification
  "recovered heat can be used for space heating and hot water without a heat pump". It can be a
  stated limit of the model or an extension, but the scored reference-house figures still follow the
  case simplification.
- **Several heat sources can be combined** ("Y-connector"). Server heat and a backup heater can feed
  the same system. This matches the case's backup heater that covers the rest.
- **Space usage matters.** Servers, tank and hydraulics take up room in the basement. Relevant for
  how many servers per house are realistic.

## 2. Locality: keep heat and data close

- **Do not transport heat.** Keep the heat in the building, at most the direct neighbourhood.
- **Do not send jobs very far either**, because of the delay (latency).
  *(interpretation)* The case's "jobs move to sites that need heat in summer" means moving **jobs**,
  not heat, and preferably to nearby sites.
- **Data sovereignty.** Do not send data to other countries where that is not allowed.

## 3. Operating principle

- **Servers can go to standby.**
- **"Only compute if you need it"**, meaning when heat is needed. *(interpretation)* The scheduler is
  heat-led: heat demand (plus tank state) decides how much computing runs, not the other way round.

## 4. Customer segments and heat profiles

Different sites need heat at different times of the year:

| Segment | Heat need | Extra angle from the notes |
|---|---|---|
| Family houses | mostly winter | the reference house of the case |
| Farming | e.g. drying corn/grain *(notes: "dring corn")* | seasonal heat outside winter *(interpretation)* |
| Hotel with spa | all year | summer sink for heat |
| Law office | building heat | sensitive client data, so run locally or fully anonymised |
| Grocery shop | heating | CCTV: needs heat **and** video monitoring at the same time; a data leak would be a serious problem |

## 5. Selling point: local, private compute

- The difference to "just using ChatGPT or Claude": some computations need **local data** that must
  not leave the building or the country.
- Example workloads where local processing is an advantage: CCTV analysis in shops, documents at a
  law office.
- *(interpretation)* Data privacy is a reason to buy local compute on its own, separate from the heat.
  Heat plus privacy together is a stronger story than heat alone.

## 6. Business model and competitiveness

- **The team decides on the hardware** (CPU vs GPU, exact models).
- **Research market prices:** e.g. price of image tagging services, price of a GPU-hour on Amazon
  (AWS) and other clouds. *(to verify, with sources and dates.)*
- **Build a business model for DigitalHeat** with a clear reason why customers would buy.
- **Competitiveness:** compare heat + compute against simply using AWS. The case owner thinks the idea
  is sound because most data centres just "destroy" their heat instead of using it.

## 7. The story the case owner could imagine: three layers

This was offered as one possible direction, not a requirement.

1. **Financial layer.** Calculate all money flows and simulate them over time with changing
   parameters (e.g. price over time). Use sliders to show how much room there is before the model
   stops paying off.
2. **Marketing layer.** Which priced services exist online today where we can say "we can do it
   better"? We probably cannot compete with LLMs, but maybe with image tagging. Another idea:
   surveillance / body-cam analysis as a licensed model with US companies. What would it cost per
   year?
3. **Physical layer.** A physical simulation of how the whole system works (heat, storage, servers).

**Alternative:** a completely new product idea, AI services people would buy *(notes: "AU stuff",
read as "AI stuff")*, e.g. "Find your relatives through images".

## 8. Open questions this raises (for the team)

- Which workloads are both locally valuable (privacy) and interruptible (heat-led scheduling)?
  CCTV analysis, for example, may be continuous and not easy to pause.
- Which nearby sites could absorb jobs or heat in summer (hotel/spa, farming), and how close is close
  enough?
- How do we handle Legionella in the model: stated limit, a backup heater top-up, or out of scope?
- Which public prices (cloud GPU/CPU hour, image tagging API) do we use as the computing-income
  benchmark, and from which date?
