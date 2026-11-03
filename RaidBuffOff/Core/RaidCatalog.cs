namespace RaidBuffOff.Core;

public sealed record RaidBuffDefinition(string EnglishName, string Job, int JobOrder, int BuffOrder, IReadOnlyList<uint> StatusIds);
public sealed record UltimateDefinition(uint DutyId, uint TerritoryId, string Code, string EnglishName);

public static class RaidCatalog
{
    public static IReadOnlyList<RaidBuffDefinition> Buffs { get; } = Array.AsReadOnly(new[] {
        new RaidBuffDefinition("Divination", "Astrologian / AST", 0, 0, [1878]),
        new RaidBuffDefinition("The Balance", "Astrologian / AST", 0, 1, [3887]),
        new RaidBuffDefinition("The Spear", "Astrologian / AST", 0, 2, [3889]),
        new RaidBuffDefinition("Brotherhood", "Monk / MNK", 1, 0, [1185]),
        new RaidBuffDefinition("Battle Litany", "Dragoon / DRG", 2, 0, [786]),
        new RaidBuffDefinition("Arcane Circle", "Reaper / RPR", 3, 0, [2599]),
        new RaidBuffDefinition("Battle Voice", "Bard / BRD", 4, 0, [141]),
        new RaidBuffDefinition("Radiant Finale", "Bard / BRD", 4, 1, [2964]),
        new RaidBuffDefinition("Technical Finish", "Dancer / DNC", 5, 0, [1822]),
        new RaidBuffDefinition("Devilment", "Dancer / DNC", 5, 1, [1825]),
        new RaidBuffDefinition("Searing Light", "Summoner / SMN", 6, 0, [2703]),
        new RaidBuffDefinition("Embolden", "Red Mage / RDM", 7, 0, [1239, 1297]),
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
    // CanStatusOff alone is not a reliable PvE catalogue filter: identical names have old/PvP entries.
    // Match the current PvE IDs and positive-status category; the game validates every cancellation.
    public static RaidBuffDefinition? FindBuff(uint statusId, string englishName, bool beneficial) =>
        beneficial ? Buffs.FirstOrDefault(b => b.StatusIds.Contains(statusId) &&
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
