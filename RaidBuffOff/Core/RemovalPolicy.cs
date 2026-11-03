namespace RaidBuffOff.Core;

public enum GateReason { Off, LoggedOut, Unavailable, OutOfCombat, AreaNotSelected, NoBuffsSelected, Active }
public sealed record RunContext(bool Enabled, bool LoggedIn, bool Ready, bool InCombat, uint TerritoryId, ulong CharacterId, bool TestAllAreas = false);
public readonly record struct BuffInstance(uint StatusId, uint SourceId, bool Beneficial = true, bool CanStatusOff = true)
{
    public Cancellation Key => new(StatusId, SourceId == 0 ? 0xE0000000u : SourceId);
}
public readonly record struct Cancellation(uint StatusId, uint SourceId);

public static class RemovalPolicy
{
    public static GateReason Evaluate(RunContext context, IReadOnlySet<uint> areas, IReadOnlySet<uint> statuses)
    {
        if (!context.Enabled) return GateReason.Off;
        if (!context.LoggedIn || context.CharacterId == 0) return GateReason.LoggedOut;
        if (!context.Ready) return GateReason.Unavailable;
        if (!context.InCombat) return GateReason.OutOfCombat;
        if (context.TerritoryId == 0 || (!context.TestAllAreas && !areas.Contains(context.TerritoryId))) return GateReason.AreaNotSelected;
        if (statuses.Count == 0) return GateReason.NoBuffsSelected;
        return GateReason.Active;
    }
    public static bool MayCancel(BuffInstance buff, IReadOnlySet<uint> selected) =>
        buff.StatusId != 0 && buff.Beneficial && buff.CanStatusOff && selected.Contains(buff.StatusId);
}

/// <summary>Limits status-off attempts and isolates cooldowns by character and territory.</summary>
public sealed class RemovalPlanner
{
    private readonly Dictionary<Cancellation, double> nextAttempt = [];
    private ulong owner;
    private uint territory;
    public IReadOnlyList<Cancellation> Plan(RunContext context, IReadOnlySet<uint> areas,
        IReadOnlySet<uint> selected, IEnumerable<BuffInstance> present, double now)
    {
        if (owner != context.CharacterId || territory != context.TerritoryId)
        { nextAttempt.Clear(); owner = context.CharacterId; territory = context.TerritoryId; }
        if (RemovalPolicy.Evaluate(context, areas, selected) != GateReason.Active)
        { nextAttempt.Clear(); return Array.Empty<Cancellation>(); }
        foreach (var pair in nextAttempt.ToArray())
            if (pair.Value <= now) nextAttempt.Remove(pair.Key);
        return present.Where(b => RemovalPolicy.MayCancel(b, selected)).Select(b => b.Key).Distinct()
            .Where(k => !nextAttempt.ContainsKey(k)).Take(4).ToArray();
    }
    public void Record(Cancellation key, bool success, double now) => nextAttempt[key] = now + (success ? 0.5 : 3);
    public void Reset() { nextAttempt.Clear(); owner = 0; territory = 0; }
}
