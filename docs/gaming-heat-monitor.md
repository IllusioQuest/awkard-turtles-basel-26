> Reviewed update: compact terminal layout, observed CPU/RAM, source details and reproducible date/session controls are documented in [the monitor review](gaming-monitor-review.md). The sections below retain the original model explanation; generation means model reservations, not measured power.

# Gaming heat monitor (T-42): heat need and generation per node

Status: prototype, 2026-11-21, Nikita / Claude, ticket [T-42](tickets/T-42-gaming-heat-monitor.md).
Written for co-builders: what the tool shows, where every number comes from, how to run it and
what it does **not** do. Code: `gaming/monitor/`. Config: `gaming/deploy/heat-monitor.json`.

## 1. What it is for

While someone plays Xonotic in the browser (see [gaming-architecture.md](gaming-architecture.md)),
a second terminal shows, for each live gaming node (session-a/b/c), how much heat the node's
**site needs** this hour and how much heat the game sessions **reserved on that node generate**.
You can reserve and release player sessions with keys and step through hours, and watch where the
existing heat-based placement (`SessionPlacement`, ADR 0022) puts each player and how the site's
tank fills.

It is a viewer on top of existing code. It adds **no** heat model, scheduler or placement rule:

| Shown value | Computed by (existing code) |
|---|---|
| Heat need per site and hour | `NetworkScenarioLoader` (T-12, `config/network/switzerland-test.json`); live: Core `HeatDemand.Build` (case formula) |
| Free capacity | Core `HeatCapacity.RemainingKwh` (same numerator as `GreedyHeatScheduler`) |
| Which node gets a new player | gaming `SessionPlacement.TryReserve` (largest free heat capacity, ready node, free slot) |
| Tank, direct use, dumped, backup | Core `HeatBalance.Step` (the one hourly balance of the model) |
| Node ready | read-only `docker ps` by exact compose labels + runtime profile capabilities (T-40) |

It never launches, stops, pauses or moves a game. A reservation is a claim in memory, exactly as
in the existing placement demo.

## 2. Node-to-site mapping

The three nodes run on one laptop. For the monitor each is placed at a site of the Swiss test
dataset (agreed with Nikita, 2026-11-21):

| Node | Site | Servers | Tank | Why |
|---|---|---|---|---|
| session-a | `basel-reference-house-case-1` | 4 | 35 kWh | the case's reference house; only site whose demand is the case formula, so it can follow live weather |
| session-b | `basel-mfh-1` (apartment block) | 11 | 96.2 kWh | middle-sized winter site |
| session-c | `basel-hotel-spa-1` | 28 | 245 kWh | summer heat sink (hot water all year) |

Server counts and tanks are the dataset's values (synthetic and marked per field there, except
the reference house, which is from the case). Change the mapping in `heat-monitor.json`; any site
id of the scenario works.

Today only session-c declares the `xonotic-installed` capability in
`gaming/deploy/runtime-profiles.json`, so with the capability check on, every player lands on
session-c, and there is one session slot per node (one desktop lock per desktop, T-40). Press
**C** for a labelled what-if that ignores capabilities, to see how heat would distribute players
over the three sites.

## 3. Where every number comes from

| Number | Value | Source |
|---|---|---|
| Heat per game session | 0.8 kWh/h | case: 1 kW server × 80 % recoverable. Assumption **A-018**: one session occupies one full case server. Not a Xonotic power measurement. |
| Site demand (replay) | hourly, 2025 | `data/derived/network/switzerland-test/*-demand.csv`; reference house: Open-Meteo Basel 2025 + case formula |
| Site demand (live, reference house) | today | Open-Meteo forecast API at the site's coordinates, `temperature_2m`, Europe/Zurich, then case formula `max(0, 10 kW × (20 − T) ÷ 25.6)` on heating days + 12 kWh/day hot water |
| Site demand (live, other sites) | 2025 same date and hour | assumption **A-019**: their profile shapes are built offline in `scripts/build_network_dataset.py`; we do not re-implement them live |
| Electricity price (replay) | 0.3325 CHF/kWh | scenario, from committed ElCom 2026 IWB Basel H4 snapshot |
| Electricity price (live) | current year | ElCom GraphQL API, category H4, filtered by each site's municipality (BFS number in config); if several operators serve it, the lowest operator number is used and the count is shown (A-019) |
| Heat value | price ÷ 2.5 | case (heat-pump efficiency) |
| Dataset UTC offset | +1 h | `data/raw/open-meteo/SOURCE.md` (fixed offset, 24 values per day) |

Live calls have a 10 s timeout. Any failure falls back to the replay value **for that site only**
and writes a warning to the log panel, so the stage demo still works offline.

## 4. How to read the screen

**Per node table.** *Docker* is the container state; *Xonotic* is whether the game process runs
inside it (`pgrep`), or why the node is not capable. *Ready* = container running and capable;
only ready nodes receive new players. *Generation* = sum of reservations on the node (sessions ×
0.8 kWh/h). *Site heat need* = space heating + hot water of the node's site this hour.
*Gen / need* is the share of the site's need covered by this node's sessions (red above 100 %).
*Site free capacity* = need + free tank − all reservations at the site. *Useful heat CHF/h* = the
node's share of heat actually used (directly or into the tank) × heat value.

**Per site table.** A preview of the current hour with `HeatBalance.Step`: what the held sessions
would cover directly, charge into or draw from the tank, dump, and what the backup heater must
supply. *Run total* sums closed hours since the last start/jump.

**Worked example (replay start, 2025-01-15 18:00, tanks empty).** Basel is at −0.6 °C, and the
day's mean is −1.4 °C (< 12 °C, so it is a heating day). Reference house need = 10 × (20 + 0.6) ÷
25.6 = 8.05 kWh space + 12 ÷ 24 = 0.50 kWh hot water = **8.55 kWh**. Free capacity = 8.55 + 35 −
0 = 43.55 kWh. The hotel/spa needs 34.68 + 6.79 = **41.46 kWh**, so its free capacity is 41.46 +
245 = 286.46 kWh. Press R: the player goes to the site with the most free capacity that has a
ready node with a free slot, i.e. session-c at the hotel/spa, and generates 0.8 kWh/h, which
covers 0.8 ÷ 41.46 = 1.9 % of its need. The backup heater supplies the remaining 40.66 kWh. In
July (press U) the reference house needs only 0.50 kWh/h (hot water), the hotel/spa 7.29 kWh/h.

## 5. Keys

| Key | Action |
|---|---|
| R | reserve a new player session (placement decides the node) |
| X | release the most recent session |
| → or N / F | close the current hour (tank moves) and go 1 h / 24 h forward (replay only) |
| ← or B | go back one hour: tank restored, players placed again in their original order |
| J / U | jump to the January / July hour from the config, tanks empty, players placed again |
| L | toggle live mode (clock time, Open-Meteo + ElCom) and replay |
| D | probe Docker now (otherwise every 5 s) |
| C | what-if: ignore declared capabilities |
| Q or Esc | quit |

Why "placed again": `SessionPlacement` refuses to go back in time (a guard from ADR 0022). For
back steps and jumps the monitor starts a fresh placement at the target hour and requests the same
players in order; players that no longer fit are listed in the log and released. Going forward,
players stay pinned to their node, as in the real system.

## 6. Run it

From the repository root (Windows PowerShell or any shell), with Docker Desktop running:

```powershell
dotnet run --project gaming/monitor                  # replay, reads Docker
dotnet run --project gaming/monitor -- --live        # start in live mode (needs internet)
dotnet run --project gaming/monitor -- --no-docker   # stage fallback: assume containers run
dotnet run --project gaming/monitor -- --once        # print one frame and exit
dotnet test gaming/tests                             # includes HeatMonitorTests
```

The first build downloads the `Spectre.Console` NuGet package (terminal tables), so it needs
internet once. The normal view fits an 80-column terminal; use S or --details for source and calculation panels. A taller terminal avoids clipping in interactive mode.

## 7. Limits (say these if a juror asks)

- Generation is **reserved**, not measured: no Xonotic power or heat has been measured yet. Swap
  A-018 for a measured value (e.g. `scripts/measure_job_windows_battery.ps1`) when available.
- Only game sessions count as generation; other servers at a site (batch jobs) are not shown.
- One laptop hosts all three "sites"; physical heat goes into one room, not three buildings.
- Live demand for non-reference sites is the 2025 same-hour value, not today's.
- Network eligibility is not probed (all nodes are local), and the session slot count is a
  declaration.
- Replay hours are dataset labels with a fixed UTC+1 (no summer time, as in the case data), so
  "18:00" in July replay is 19:00 CEST; live mode uses real Europe/Zurich time.
- *Run total useful* counts heat as useful when it enters the tank; heat still in the tank at the
  end of a run is included.
- In live mode, if the laptop sleeps across several hours, only one hour is closed and the log
  says how many were skipped.
- Verified in a separate Release build while the original Debug monitor remained open; see the review guide for checks.
