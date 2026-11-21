# T-25 example-house visual sketch

Approved as a sensible direction by the user on 2026-11-21; team-lead pitch feedback is pending.
One landscape screen, sized for a projected pitch and usable from a local browser file. This is
a wireframe, not a simulated result. T-37 tracks the requested professional final video.

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ INSIDE THE EXAMPLE HOUSE          [15 January | 15 July] [▶]  hour ━●━━━━ │
├─────────────────────────────────────────────────────────────────────────────┤
│       ┌────────────── HOUSE CUTAWAY ──────────────┐  HOURLY FACTS          │
│       │   radiator ♨       hot water 🚿           │  Demand     … kWh      │
│       │          ↑ combined useful heat           │  Useful heat … kWh      │
│       │          │                                  │  Backup      … kWh      │
│       │    server rack ───→ heat tank ──────→ house│  Tank      … / 35 kWh  │
│       │    [load bars]       [level]               │  Servers      … %       │
│       │          ↓               ↑                 │                      │
│       │       backup heater   stored heat          │  Status: year replay  │
│       └────────────────────────────────────────────┘  or LiveRun log         │
├─────────────────────────────────────────────────────────────────────────────┤
│ A plain-language sentence explains this hour. Source: C# result export.     │
└─────────────────────────────────────────────────────────────────────────────┘
```

Animated arrows encode the exported `produced_heat_kwh`, `direct_used_kwh`,
`tank_charge_kwh`, `tank_discharge_kwh` and `backup_heat_kwh`; labels show their exact values.
Space-heating and hot-water demand are displayed separately, but their supply is not split
because the C# result only exports combined useful heat. The playback includes a visible
`simulated year` badge. The two selected days are sourced from the committed T-13 v1 export.

The selected days do **not** naturally contain a zero target load: 15 January is full load all
day; on 15 July the tank is full but hot-water demand still calls for a positive load. A separate
`LiveRun log` import will show an actual forced pause/resume recorded from the shared scheduler
and Docker executor. Without such a log, the visual must not claim that a real job stopped.

Implementation after approval: a self-contained offline HTML view generated from the committed
T-13 hourly CSV; keyboard and buttons for day, playback, pause and hour seeking; optional local
LiveRun text-log import. No client-side heat or economic formulas.
