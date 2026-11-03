using RaidBuffOff.Core;

static class CatalogTests
{
    private static int count;
    private static void Check(bool value, string reason)
    { if (!value) throw new Exception(reason); count++; }
    public static int Run()
    {
        Check(RaidCatalog.Buffs.Count == 13, "Exactly the screenshot raid buffs, including both attack cards");
        Check(RaidCatalog.Buffs.Select(b => b.EnglishName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 13,
            "No duplicate named checkboxes");
        foreach (var b in RaidCatalog.Buffs)
        {
            Check(RaidCatalog.FindBuff(b.EnglishName, true, true) == b, "Eligible buff appears under its job");
            Check(RaidCatalog.FindBuff(b.EnglishName, false, true) == null, "Debuff names are not accepted");
            Check(RaidCatalog.FindBuff(b.EnglishName, true, false) == null, "Cannot display a non-cancellable status");
        }
        Check(RaidCatalog.FindBuff("  the balance  ", true, true)?.Job.EndsWith("AST") == true, "Balance attack card");
        Check(RaidCatalog.FindBuff("the Spear", true, true)?.Job.EndsWith("AST") == true, "Spear attack card");
        Check(RaidCatalog.FindBuff("Starry Muse", true, true)?.Job.EndsWith("PCT") == true, "Use the real Starry Muse name");
        Check(RaidCatalog.FindBuff("Starry Mues", true, true) == null, "Do not turn the macro typo into a catalogue name");
        foreach (var name in new[] { "Standard Finish", "Meditative Brotherhood", "The Arrow", "The Bole",
            "The Ewer", "The Spire", "The Wanderer's Minuet", "Mage's Ballad", "Army's Paeon", "Well Fed", "Sprint" })
            Check(RaidCatalog.FindBuff(name, true, true) == null, "No non-requested or general-purpose buff: " + name);

        Check(RaidCatalog.Ultimates.Count == 7, "Exactly seven Ultimate choices");
        Check(RaidCatalog.Ultimates.Select(d => d.TerritoryId).Distinct().Count() == 7, "No duplicate territory IDs");
        foreach (var d in RaidCatalog.Ultimates)
        {
            Check(RaidCatalog.FindUltimate(d.DutyId, d.TerritoryId, 28) == d, "Accept verified Ultimate mapping");
            Check(RaidCatalog.FindUltimate(d.DutyId, d.TerritoryId, 5) == null, "Reject non-Ultimate content type");
            Check(RaidCatalog.FindUltimate(d.DutyId + 1, d.TerritoryId, 28) == null, "Reject mismatched duty ID");
            Check(RaidCatalog.FindUltimate(d.DutyId, d.TerritoryId + 1, 28) == null, "Reject mismatched territory");
        }
        Check(RaidCatalog.FindUltimate(0, 0, 28) == null, "Never accept a fallback territory");
        var savedStatuses = new HashSet<uint> { 200, 201 };
        var savedAreas = new HashSet<uint> { 733, 101 };
        Check(RaidCatalog.RestrictSelections(savedStatuses, savedAreas,
            new HashSet<uint> { 200 }, new HashSet<uint> { 733 }), "Migrate old broad selections");
        Check(savedStatuses.SetEquals(new uint[] { 200 }), "Old non-raid status selections are pruned");
        Check(savedAreas.SetEquals(new uint[] { 733 }), "Old non-Ultimate territory selections are pruned");
        Check(RemovalPolicy.Evaluate(new(true, true, true, true, 101, 42), savedAreas, savedStatuses) == GateReason.AreaNotSelected,
            "Previously enabled normal duties cannot keep removing buffs after migration");
        Check(!RaidCatalog.RestrictSelections(savedStatuses, savedAreas,
            new HashSet<uint> { 200 }, new HashSet<uint> { 733 }), "Migration is idempotent");
        Console.WriteLine($"PASS: {count} catalogue and migration assertions.");
        return count;
    }
}
