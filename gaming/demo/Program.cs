using System.Text.Json;
using DigitalHeat.Core;
using DigitalHeat.Gaming;

// Fixed synthetic inputs: reproducible placement example, not measured heat or launched games.
var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
var placement = new SessionPlacement();
GamingSite Site(string id, double demand, double stored, double capacity) =>
    new(id, "CH", new HeatSignal(demand, stored, capacity, 1, 0.8), 0);
var state = new GamingSnapshot(1, now, now.AddHours(1), "Synthetic placement test, all figures illustrative",
    [Site("home", 0.5, 35, 35), Site("hotel", 2, 3, 4), Site("spa", 3, 1, 2)],
    [new("home-a", "home", true, true, 1), new("hotel-a", "hotel", true, true, 1),
     new("spa-a", "spa", true, true, 1), new("spa-b", "spa", true, true, 1)]);
placement.Update(state, now);
SessionRequest Request(string id) => new(id, "home",
    new JobPlacementPolicy("country-allowlist", "Synthetic Swiss local-lab placement", ["CH"]), 2);
var first = placement.TryReserve(Request("player-1"), now);
var second = placement.TryReserve(Request("player-2"), now);
var third = placement.TryReserve(Request("player-3"), now);
// Raising the home's heat demand changes new placement but does not move player-1.
placement.Update(state with { Revision = 2, Sites = [Site("home", 5, 35, 35), state.Sites[1], state.Sites[2]] }, now);
var fourth = placement.TryReserve(Request("player-4"), now);
Console.WriteLine(JsonSerializer.Serialize(new {
    mode = "synthetic_reservations_only_no_game_launch",
    first, second, third, fourth,
    first_after_heat_change = placement.TryReserve(Request("player-1"), now),
    reservations = placement.Reservations()
}, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
