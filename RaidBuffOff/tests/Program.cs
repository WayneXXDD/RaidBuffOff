using RaidBuffOff.Core;

static class Tests
{
    private static int count;
    private static readonly HashSet<uint> Areas = [101];
    private static readonly HashSet<uint> Selected = [200, 201, 202, 203, 204, 205];
    private static readonly RunContext Active = new(true, true, true, true, 101, 42);
    private static readonly BuffInstance Buff = new(200, 123);
    private static void Check(bool ok, string reason)
    { if (!ok) throw new Exception(reason); count++; }
    public static void Main(string[] args)
    {
        // Every combination of the six independent activation requirements.
        for (int mask = 0; mask < 64; mask++)
        {
            bool enabled = (mask & 1) != 0, logged = (mask & 2) != 0, ready = (mask & 4) != 0;
            bool combat = (mask & 8) != 0, area = (mask & 16) != 0, buff = (mask & 32) != 0;
            var c = new RunContext(enabled, logged, ready, combat, 101, logged ? 42ul : 0ul);
            var a = area ? Areas : new HashSet<uint>();
            var s = buff ? Selected : new HashSet<uint>();
            Check((RemovalPolicy.Evaluate(c, a, s) == GateReason.Active) ==
                (enabled && logged && ready && combat && area && buff), $"Activation requirements mask {mask}");
        }
        Check(RemovalPolicy.Evaluate(Active with { Enabled = false }, Areas, Selected) == GateReason.Off, "OFF always blocks");
        Check(RemovalPolicy.Evaluate(Active with { CharacterId = 0 }, Areas, Selected) == GateReason.LoggedOut, "Identity required");
        Check(RemovalPolicy.Evaluate(Active with { InCombat = false }, Areas, Selected) == GateReason.OutOfCombat, "No out-of-combat cancellation");
        Check(RemovalPolicy.Evaluate(Active with { TerritoryId = 102 }, Areas, Selected) == GateReason.AreaNotSelected, "No other-zone cancellation");
        Check(RemovalPolicy.Evaluate(Active with { TerritoryId = 0 }, new HashSet<uint> { 0 }, Selected) != GateReason.Active, "Reject invalid zone zero");
        Check(!RemovalPolicy.MayCancel(Buff with { StatusId = 0 }, new HashSet<uint> { 0 }), "Reject empty status slot");
        Check(!RemovalPolicy.MayCancel(Buff with { StatusId = 999 }, Selected), "Never remove unselected buffs");
        Check(!RemovalPolicy.MayCancel(Buff with { Beneficial = false }, Selected), "Never remove debuffs");
        Check(!RemovalPolicy.MayCancel(Buff with { CanStatusOff = false }, Selected), "Never attempt non-cancellable buffs");
        Check(RemovalPolicy.MayCancel(Buff, Selected), "Allow selected cancellable positive status");

        var p = new RemovalPlanner();
        var plan = p.Plan(Active, Areas, Selected, [Buff, Buff, new(201, 456), new(999, 789), new(202, 123, false)], 0);
        Check(plan.Count == 2, "Deduplicate and filter before issuing actions");
        Check(plan[0] == new Cancellation(200, 123) && plan[1] == new Cancellation(201, 456), "Preserve source IDs");
        p.Record(plan[0], true, 0);
        Check(p.Plan(Active, Areas, Selected, [Buff], 0.49).Count == 0, "Throttle successful cancellation until state updates");
        Check(p.Plan(Active, Areas, Selected, [Buff], 0.5).Count == 1, "Allow a re-applied buff after cooldown");
        p.Record(Buff.Key, false, 1);
        Check(p.Plan(Active, Areas, Selected, [Buff], 3.99).Count == 0, "Throttle rejected cancellation");
        Check(p.Plan(Active, Areas, Selected, [Buff], 4).Count == 1, "Rejection does not permanently disable the selection");

        p.Reset();
        plan = p.Plan(Active, Areas, Selected, [new(200, 0)], 0);
        Check(plan.Single().SourceId == 0xE0000000, "Normalize unknown source to the game's wildcard");
        p.Reset();
        plan = p.Plan(Active, Areas, Selected, [Buff, Buff with { SourceId = 124 }], 0);
        Check(plan.Count == 2, "Handle separate instances from separate sources");
        p.Record(plan[0], true, 0);
        Check(p.Plan(Active, Areas, Selected, [Buff with { SourceId = 124 }], 0.1).Count == 1, "Cooldown is scoped to source as well as buff");

        p.Reset();
        var many = Selected.Select(id => new BuffInstance(id, 123)).ToArray();
        plan = p.Plan(Active, Areas, Selected, many, 0);
        Check(plan.Count == 4, "Bound each polling pass to four cancellation attempts");
        foreach (var key in plan) p.Record(key, true, 0);
        Check(p.Plan(Active, Areas, Selected, many, 0.1).Count == 2, "Process remaining buffs on the next pass");

        p.Reset();
        p.Record(Buff.Key, false, 0);
        p.Plan(Active, Areas, Selected, [Buff], 0); // A new owner resets a previous session.
        p.Record(Buff.Key, false, 0);
        Check(p.Plan(Active with { CharacterId = 43 }, Areas, Selected, [Buff], 0.1).Count == 1, "New character has separate cooldowns");
        p.Record(Buff.Key, false, 0.1);
        Check(p.Plan(Active with { CharacterId = 43, TerritoryId = 102 }, new HashSet<uint> { 102 }, Selected, [Buff], 0.2).Count == 1,
            "New territory has separate cooldowns");

        p = new();
        p.Plan(Active, Areas, Selected, [Buff], 0);
        p.Record(Buff.Key, false, 0);
        Check(p.Plan(Active with { Enabled = false }, Areas, Selected, [Buff], 0.1).Count == 0, "Turning OFF blocks pending operations");
        Check(p.Plan(Active, Areas, Selected, [Buff], 0.2).Count == 1, "Explicit re-enable starts with fresh state");
        Check(p.Plan(Active, Areas, new HashSet<uint>(), [Buff], 0.3).Count == 0, "Unchecking all statuses blocks removal");
        Check(p.Plan(Active, new HashSet<uint>(), Selected, [Buff], 0.4).Count == 0, "Unchecking all areas blocks removal");
        Check(p.Plan(Active with { Ready = false }, Areas, Selected, [Buff], 0.5).Count == 0, "Loading, death and cutscenes block removal");
        Check(p.Plan(Active, Areas, Selected, [], 0.6).Count == 0, "Do nothing when the selected buff is absent");
        count += CatalogTests.Run();
        if (args.Length == 2 && args[0] == "--catalog-output")
            File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(new {
                Buffs = RaidCatalog.Buffs, Ultimates = RaidCatalog.Ultimates
            }));
        Console.WriteLine($"PASS: {count} assertions covering activation gates, catalogue scope, selections, source IDs and throttling.");
    }
}
