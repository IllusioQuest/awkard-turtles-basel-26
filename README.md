# Awkward Turtles · Basel GreenTech 2026

**Case: Useful heat from computing (DigitalHeat).**
Can computing power heat a home while doing useful work? We build a demonstrator that schedules
interruptible computing jobs on distributed servers so that their heat arrives where and when homes
need it, over a full year, with transparent and reproducible numbers.

> Status: concept phase. Tech stack and architecture are not decided yet.

## Start here

- [Case description (verbatim)](docs/case/challenge.md)
- [Key figures and must-have checklist](docs/case/key-figures.md)
- [External references and data sources](docs/case/references.md)
- [Deep-dive notes with the case owner](docs/deep-dive/notes-structured.md) ([raw](docs/deep-dive/notes-verbatim.md))
- [Data and how to fetch it](data/README.md) · [Assumptions](docs/assumptions.md) · [Decisions](docs/decisions/README.md)
- [Instructions for AI agents](AGENTS.md)

## Quick checks

```bash
python3 scripts/fetch_data.py --check   # validate the data against the case's expected values
scripts/deadline-reminder.sh            # time left until our deadlines
```
