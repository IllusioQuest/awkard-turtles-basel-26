# DigitalHeat case presentation (extracted)

> **Source:** [Dropbox: DigitalHeat.pdf](https://www.dropbox.com/scl/fi/6byhvibtqd73fmtkax6b7/DigitalHeat.pdf?rlkey=inpoz7g3kumwogq624yhx5z6y&dl=0)
> (link from [`references.md`](references.md)); the file is stored unchanged as [`DigitalHeat.pdf`](DigitalHeat.pdf).
> Extracted from DigitalHeat.pdf (6 pages, Keynote export, 16:9). Slide text verbatim where
> possible, figures described. No interpretation added. Where slide text is German, it is quoted
> verbatim and followed by an English translation marked *(translation)*.
> [`challenge.md`](challenge.md) remains the source of truth; see the last section for differences.
>
> Page numbers: the slides carry the numbers 01, 02, 03, 04 and 06. Page 5 has no slide number.

---

## Page 1 (slide 01): Title

**Slide text:**

- SERVER HEAT AND SOVEREIGN AI
- **DigitalHeat**
- Distributed servers for computing, AI and building heat in Switzerland.
- Three key figures:
  - **5–20 kW** — Heat per installed unit
  - **Switzerland** — Local data and operations
  - **50–70 °C** — Heating and hot water
- Footer: DigitalHeat · 01

**Figure:** Photo on the right half of the slide: a black, glass-fronted server rack with
indicator lights stands in a room next to a large, dark grey cylindrical tank (taller than the
rack). Insulated black pipes run from the floor and ceiling along the wall to the tank; a pipe
with a brass fitting connects at the lower side of the tank.

---

## Page 2 (slide 02): Solution

**Slide text:**

- SOLUTION
- **Computing where buildings need heat**
- Servers heat buildings. Heat exchangers connect computing to storage, heating and hot water.
- Diagram boxes:
  - **Building** — Heating, hot water and storage
  - **Server rack** — CPU and GPU work produces heat — **5–20 kW**
  - **Useful heat** — 50–70 °C target, system dependent
  - **Useful IT Power** — Up to 40 servers per rack
- CONTROL — **Flexible workloads follow heat demand.**
- Footer: DigitalHeat · 02

**Figure:** Block diagram in one row. The "Building" box (left) is connected by a line to the
"Server rack" box (centre, dark, with "5–20 kW" highlighted). From the server rack, two lines
lead to two boxes on the right: "Useful heat" (top) and "Useful IT Power" (bottom). Below the
diagram, a full-width bar labelled "CONTROL" contains the sentence "Flexible workloads follow
heat demand."

---

## Page 3 (slide 03): Ideas for flexible computing jobs

**Slide text:**

- **Ideas for flexible computing jobs**
- Which useful jobs can follow the heat demand?

| Category | Examples |
|---|---|
| Local LLMs | Batch inference, document indexing and model evaluation. |
| Images and video | Image tagging, CLIP embeddings, visual search and transcription. |
| Other ideas? | Rendering, simulations, data processing or something completely different. |

- Page number: 03

**Figure:** None (text only).

---

## Page 4 (slide 04): Jobs that can slow down, pause and move

**Slide text:**

- **Jobs that can slow down, pause and move**
- Computing follows heat demand. When a site needs no heat, work moves to another eligible site.

| Mode | Description |
|---|---|
| Slow down | Reduce processing speed or the number of active workers. |
| Pause and resume | Save progress. Continue later without repeating completed work. |
| Distribute the work | Resume saved work or assign the next batch elsewhere. Respect access and deadlines. |

- Page number: 04

**Figure:** None (text only).

---

## Page 5 (no slide number): Decentralised infrastructure (German)

The text on this page is part of the image (not extractable with `pdftotext`); transcribed from
the rendered page.

**Slide text (verbatim, German):**

- Logo: DH — DIGITAL HEAT — WÄRME AUS RECHENLEISTUNG
  *(translation: heat from computing power)*
- DEZENTRALE INFRASTRUKTUR *(translation: decentralised infrastructure)*
- **RECHNEN. HEIZEN.** *(translation: Computing. Heating.)*
- Ein Digital-Heat-Server im Keller. Rechenleistung wird direkt zu nutzbarer Wärme.
  *(translation: A Digital Heat server in the basement. Computing power becomes usable heat directly.)*
- Diagram labels: SERVER · WÄRMETAUSCHER *(translation: heat exchanger)*
- Bottom scale, left to right: HAUS · BASEL · NORDWESTSCHWEIZ · SCHWEIZ
  *(translation: house · Basel · Northwestern Switzerland · Switzerland)*
- Credit, top right: swissBOUNDARIES3D 2026 - swisstopo

**Figure:** Dark-blue illustration of a stylised two-storey house with a pitched roof and six
yellow-lit windows (three per floor). In the basement, a highlighted (yellow outline) box
contains, on the left, a server symbol (five stacked units with yellow lights) labelled
"SERVER" and, on the right, a heat-exchanger symbol labelled "WÄRMETAUSCHER". A yellow line
runs from the server through the heat exchanger and then up the side of the house into the
upper floors. At the bottom, a horizontal line with four points labelled HAUS (highlighted in
yellow, leftmost), BASEL, NORDWESTSCHWEIZ and SCHWEIZ. No numbers are shown on this page.

---

## Page 6 (slide 06): The hackathon

**Slide text:**

- **The hackathon: computing, heat and break-even**
- Build a working scheduler. Test a full year and explore paid services.

| Task | Description |
|---|---|
| Run real jobs | Demonstrate useful jobs that slow down, pause and resume across servers. |
| Follow heat demand | Simulate a year with storage and backup heat. Measure useful computing and heat. |
| Paid services | Who pays, and how much? Compare service income and heat value with total costs. |

- Page number: 06

**Figure:** None (text only).

---

## Differences to challenge.md

Facts, numbers, examples, constraints or ideas in the PDF that are **not** in
[`challenge.md`](challenge.md), or that differ from it. Listed factually, not resolved.

**Numbers and hardware**

1. **5–20 kW heat per installed unit / per server rack** (pages 1 and 2). `challenge.md` gives
   only per-server values (1 kW electricity, about 1 kW heat, 80 % recoverable) and no unit or
   rack size.
2. **Up to 40 servers per rack** ("Useful IT Power", page 2). Not in `challenge.md`. Arithmetic
   note: 40 servers at the `challenge.md` value of 1 kW each would be 40 kW, which is above the
   5–20 kW stated for a rack on the same slide. The PDF does not explain this.
3. **Useful heat at 50–70 °C target, "system dependent"** (pages 1 and 2). `challenge.md` names no
   supply temperature; it only defines the tank by a 30 K temperature spread and says recovered
   heat can be used without a heat pump.
4. **"CPU and GPU work produces heat"** (page 2). `challenge.md` does not state server hardware
   type (it only says a CPU is enough for the team's own laptop workload).
5. **Heat exchangers** connect computing to storage, heating and hot water (pages 2 and 5).
   `challenge.md` only says heat is recovered into a water-based heating system.
6. Photo (page 1) shows a single rack next to one large cylindrical tank. No size given.

**Positioning and constraints**

7. **"Server heat and sovereign AI"** and **"Switzerland — local data and operations"** (page 1);
   "in Switzerland" in the subtitle. Data sovereignty / local operation is not in `challenge.md`.
8. **Geographic scale levels** house → Basel → Northwestern Switzerland → Switzerland (page 5).
   `challenge.md` covers one reference house in Basel plus other sites (hotels, spas,
   agriculture, businesses) without naming regional levels.
9. Map data credit **swissBOUNDARIES3D 2026 (swisstopo)** (page 5). Not in `challenge.md`.
10. Page 5 is in German and uses the brand form "Digital Heat" with the tagline "Wärme aus
    Rechenleistung"; the rest of the PDF and `challenge.md` use English and "DigitalHeat".

**Jobs (examples)**

11. Job examples not in `challenge.md`: **local LLMs** (batch inference, **document indexing**,
    **model evaluation**), **CLIP embeddings**, **visual search**, **rendering**, **simulations**,
    **data processing**, "or something completely different" (page 3). `challenge.md` names
    embeddings, reranking, image recognition and transcription; "image tagging" in the PDF
    corresponds to "image recognition" there.

**Flexibility of jobs**

12. **Slow down** as a mode: "reduce processing speed or the number of active workers" (page 4),
    also in the task "jobs that slow down, pause and resume" (page 6). `challenge.md` only
    mentions distributing, pausing, rescheduling, moving, or stopping at a checkpoint and
    restarting.
13. Pause and resume **"without repeating completed work"** (page 4). `challenge.md` allows
    "stops it at a checkpoint and restarts it" without this condition.
14. **"Eligible" site** and **"respect access"** when moving work (page 4): work moves to another
    *eligible* site, respecting *access* and deadlines. `challenge.md` mentions deadlines but no
    eligibility or access constraint.
15. **"Assign the next batch elsewhere"** (page 4): work organised in batches. Not in
    `challenge.md`.
16. Real jobs should pause and resume **"across servers"** (page 6). `challenge.md` must-have 2
    requires at least one real job paused/resumed by the scheduler on the team's machine, without
    "across servers".

**Economics**

17. **"Who pays, and how much?"** and **"explore paid services"** (page 6). `challenge.md` asks for
    computing income and a break-even computing price per server-hour, not for identifying payers
    or services.
18. Compare service income and heat value with **total costs** (page 6). `challenge.md`'s balance
    uses electricity cost only (break-even = (electricity cost − value of heat used) ÷ server-hours
    sold); hardware appears only as "profitability of the hardware" in the success statement.

**Not in the PDF:** none of the reference-house figures of `challenge.md` (1 kW per server, 80 %
recovery, 10 kW design load, 19.2 kWh per server-day, 12 kWh/day hot water, 35 kWh tank,
ElCom price, heat value = price ÷ 2.5, 206 heating days, ≈ 27,000 kWh), the must-haves or the
jury weights appear in the PDF.
