namespace RaidBuffOff.Core;

public sealed record RaidBuffDefinition(string EnglishName, string Job, int JobOrder, int BuffOrder);
public sealed record UltimateDefinition(uint DutyId, uint TerritoryId, string Code, string EnglishName);

public static class RaidCatalog
{
    public static IReadOnlyList<RaidBuffDefinition> Buffs { get; } = Array.AsReadOnly(new[] {
        new RaidBuffDefinition("Divination", "占星術士 / AST", 0, 0),
        new RaidBuffDefinition("The Balance", "占星術士 / AST", 0, 1),
        new RaidBuffDefinition("The Spear", "占星術士 / AST", 0, 2),
        new RaidBuffDefinition("Brotherhood", "武僧 / MNK", 1, 0),
        new RaidBuffDefinition("Battle Litany", "龍騎士 / DRG", 2, 0),
        new RaidBuffDefinition("Arcane Circle", "Reaper / RPR", 3, 0),
        new RaidBuffDefinition("Battle Voice", "吟遊詩人 / BRD", 4, 0),
        new RaidBuffDefinition("Radiant Finale", "吟遊詩人 / BRD", 4, 1),
        new RaidBuffDefinition("Technical Finish", "舞者 / DNC", 5, 0),
        new RaidBuffDefinition("Devilment", "舞者 / DNC", 5, 1),
        new RaidBuffDefinition("Searing Light", "召喚師 / SMN", 6, 0),
        new RaidBuffDefinition("Embolden", "赤魔法師 / RDM", 7, 0),
        new RaidBuffDefinition("Starry Muse", "Pictomancer / PCT", 8, 0)
    });
    // Verified against Global ContentFinderCondition data. No general territory or future-duty fallback.
    public static IReadOnlyList<UltimateDefinition> Ultimates { get; } = Array.AsReadOnly(new[] {
        new UltimateDefinition(280, 733, "UCOB", "the Unending Coil of Bahamut (Ultimate)"),
        new UltimateDefinition(539, 777, "UWU", "the Weapon's Refrain (Ultimate)"),
        new UltimateDefinition(694, 887, "TEA", "the Epic of Alexander (Ultimate)"),
        new UltimateDefinition(788, 968, "DSR", "Dragonsong's Reprise (Ultimate)"),
        new UltimateDefinition(908, 1122, "TOP", "the Omega Protocol (Ultimate)"),
        new UltimateDefinition(1006, 1238, "FRU", "Futures Rewritten (Ultimate)"),
        new UltimateDefinition(1094, 1363, "UMAD", "Dancing Mad (Ultimate)")
    });
    public static RaidBuffDefinition? FindBuff(string englishName, bool beneficial, bool canStatusOff) =>
        beneficial && canStatusOff ? Buffs.FirstOrDefault(b =>
            string.Equals(b.EnglishName, englishName.Trim(), StringComparison.OrdinalIgnoreCase)) : null;
    public static UltimateDefinition? FindUltimate(uint dutyId, uint territoryId, uint contentType) =>
        contentType == 28 ? Ultimates.FirstOrDefault(d => d.DutyId == dutyId && d.TerritoryId == territoryId) : null;

    // Prune old broad selections when migrating from the original all-buff/all-zone catalogue.
    public static bool RestrictSelections(HashSet<uint> statuses, HashSet<uint> territories,
        IReadOnlySet<uint> eligibleStatuses, IReadOnlySet<uint> eligibleTerritories)
    {
        int before = statuses.Count + territories.Count;
        statuses.IntersectWith(eligibleStatuses);
        territories.IntersectWith(eligibleTerritories);
        return before != statuses.Count + territories.Count;
    }
}
