using Dalamud.Configuration;

namespace RaidBuffOff;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;
    public bool Enabled { get; set; }
    public bool TestAllAreas { get; set; }
    public HashSet<uint> SelectedStatusIds { get; set; } = [];
    public HashSet<uint> SelectedTerritoryIds { get; set; } = [];
}
