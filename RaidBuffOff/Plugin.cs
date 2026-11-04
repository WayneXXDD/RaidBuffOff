using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using RaidBuffOff.Core;
using NativeStatusManager = FFXIVClientStructs.FFXIV.Client.Game.StatusManager;
using StatusRow = Lumina.Excel.Sheets.Status;
using Lumina.Excel.Sheets;

namespace RaidBuffOff;

public sealed class Plugin : IDalamudPlugin
{
    private sealed record BuffOption(uint Id, string Name, string English, string Description, RaidBuffDefinition Definition);
    private sealed record BuffGroup(string Name, string English, string Description, uint[] Ids, RaidBuffDefinition Definition);
    private sealed record AreaOption(uint Id, string Name, int Order);
    private sealed record View(uint Territory, string Area, uint[] Present, GateReason Reason,
        int Removed, string LastAction);
    private readonly IDalamudPluginInterface pi;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly IObjectTable objects;
    private readonly ICondition condition;
    private readonly ICommandManager commands;
    private readonly IPluginLog log;
    private readonly IDtrBarEntry entry;
    private readonly Configuration config;
    private readonly object sync = new();
    private readonly RemovalPlanner planner = new();
    private readonly Dictionary<uint, BuffOption> buffs = [];
    private readonly List<BuffGroup> groups = [];
    private readonly List<AreaOption> areas = [];
    private readonly Dictionary<uint, string> areaNames = [];
    private volatile View view = new(0, "", [], GateReason.Off, 0, "");
    private bool showSettings = true;
    private volatile bool disposed;
    private string buffSearch = "", lastAction = "";
    private string lastDtrText = "", lastDtrTooltip = "";
    private int pendingToggles, pendingSwitch, removed;
    private double nextTick;
    private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    public Plugin(IDalamudPluginInterface pi, IFramework framework, IClientState client, IPlayerState player,
        IObjectTable objects, ICondition condition, ICommandManager commands, IPluginLog log, IDataManager data, IDtrBar dtr)
    {
        this.pi = pi; this.framework = framework; this.client = client; this.player = player;
        this.objects = objects; this.condition = condition; this.commands = commands; this.log = log;
        config = pi.GetPluginConfig() as Configuration ?? new();
        config.SelectedStatusIds ??= []; config.SelectedTerritoryIds ??= [];
        var english = data.GetExcelSheet<StatusRow>(ClientLanguage.English);
        foreach (var row in data.GetExcelSheet<StatusRow>())
        {
            if (row.RowId == 0 || row.StatusCategory != 1) continue;
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var en = english.TryGetRow(row.RowId, out var eng) ? eng.Name.ToString() : name;
            var definition = RaidCatalog.FindBuff(row.RowId, en, row.StatusCategory == 1);
            if (definition == null) continue;
            buffs.Add(row.RowId, new(row.RowId, name, en, row.Description.ToString(), definition));
        }
        foreach (var group in buffs.Values.GroupBy(b => (b.Name, b.English)))
            groups.Add(new(group.Key.Name, group.Key.English, group.First().Description,
                group.Select(b => b.Id).Order().ToArray(), group.First().Definition));
        groups.Sort((a, b) => a.Definition.JobOrder != b.Definition.JobOrder
            ? a.Definition.JobOrder.CompareTo(b.Definition.JobOrder)
            : a.Definition.BuffOrder.CompareTo(b.Definition.BuffOrder));
        var duties = data.GetExcelSheet<ContentFinderCondition>();
        foreach (var definition in RaidCatalog.Ultimates)
        {
            if (!duties.TryGetRow(definition.DutyId, out var duty) ||
                RaidCatalog.FindUltimate(duty.RowId, duty.TerritoryType.RowId, duty.ContentType.RowId) == null)
                continue;
            var name = $"{definition.Code} — {duty.Name}";
            areas.Add(new(definition.TerritoryId, name, areas.Count));
            areaNames[definition.TerritoryId] = name;
        }
        var eligibleStatuses = buffs.Keys.ToHashSet();
        var eligibleTerritories = areas.Select(a => a.Id).ToHashSet();
        bool pruned = RaidCatalog.RestrictSelections(config.SelectedStatusIds, config.SelectedTerritoryIds,
            eligibleStatuses, eligibleTerritories);
        if (config.Version < 3 || pruned) { config.Version = 3; Save(); }
        entry = dtr.Get("Raid Buff Off", Text("raidbuff: OFF"));
        entry.OnClick = OnDtrClick;
        entry.Shown = true;
        commands.AddHandler("/rbo", new CommandInfo(OnCommand) {
            HelpMessage = "團隊增益取消設定；/rbo on、off、toggle 可切換總開關。"
        });
        framework.Update += Tick;
        pi.UiBuilder.Draw += Draw;
        pi.UiBuilder.OpenConfigUi += Open;
        pi.UiBuilder.OpenMainUi += Open;
        UpdateDtr(GateReason.Off);
    }

    private static SeString Text(string value) => new SeStringBuilder().AddText(value).Build();
    private void Open() => showSettings = true;
    private void OnDtrClick(DtrInteractionEvent ev)
    {
        if (disposed || ev.ScrollDirection != MouseScrollDirection.None) return;
        if (ev.ClickType == MouseClickType.Right) Open();
        else if (ev.ClickType == MouseClickType.Left) Interlocked.Increment(ref pendingToggles);
    }
    private void OnCommand(string _, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "on": Interlocked.Exchange(ref pendingSwitch, 1); break;
            case "off": Interlocked.Exchange(ref pendingSwitch, 2); break;
            case "toggle": Interlocked.Increment(ref pendingToggles); break;
            default: Open(); break;
        }
    }
    private void Save() => pi.SavePluginConfig(config); // Caller holds sync.
    private RunContext Context()
    {
        var local = objects.LocalPlayer;
        bool ready = player.IsLoaded && local != null && local.CurrentHp > 0 &&
            !condition[ConditionFlag.BetweenAreas] && !condition[ConditionFlag.BetweenAreas51] &&
            !condition[ConditionFlag.Unconscious] && !condition[ConditionFlag.OccupiedInCutSceneEvent] &&
            !condition[ConditionFlag.WatchingCutscene] && !condition[ConditionFlag.WatchingCutscene78];
        return new(config.Enabled, client.IsLoggedIn, ready, condition[ConditionFlag.InCombat],
            client.TerritoryType, player.IsLoaded ? player.ContentId : 0, config.TestAllAreas);
    }
    private void Tick(IFramework _)
    {
        if (disposed) return;
        try
        {
            lock (sync)
            {
                int toggles = Interlocked.Exchange(ref pendingToggles, 0);
                int set = Interlocked.Exchange(ref pendingSwitch, 0);
                if ((toggles & 1) != 0) config.Enabled = !config.Enabled;
                if (set != 0) config.Enabled = set == 1;
                if (toggles != 0 || set != 0) { Save(); nextTick = 0; }
                double now = Now;
                if (now < nextTick) return;
                nextTick = now + 0.1;
                var context = Context();
                var reason = RemovalPolicy.Evaluate(context, config.SelectedTerritoryIds, config.SelectedStatusIds);
                var local = objects.LocalPlayer;
                var present = local == null ? Array.Empty<BuffInstance>() :
                    local.StatusList.Select(s => new BuffInstance(s.StatusId, s.SourceId,
                        buffs.ContainsKey(s.StatusId), buffs.ContainsKey(s.StatusId))).ToArray();
                var requests = planner.Plan(context, config.SelectedTerritoryIds, config.SelectedStatusIds, present, now);
                foreach (var request in requests)
                {
                    if (Volatile.Read(ref pendingToggles) != 0 || Volatile.Read(ref pendingSwitch) != 0) break;
                    var current = Context();
                    if (disposed || RemovalPolicy.Evaluate(current, config.SelectedTerritoryIds, config.SelectedStatusIds) != GateReason.Active ||
                        current.CharacterId != context.CharacterId || current.TerritoryId != context.TerritoryId) break;
                    var self = objects.LocalPlayer;
                    if (self == null || !buffs.ContainsKey(request.StatusId) ||
                        !self.StatusList.Any(s => s.StatusId == request.StatusId &&
                            (s.SourceId == 0 ? 0xE0000000u : s.SourceId) == request.SourceId)) continue;
                    // Uses the game's user-interaction cancellation path; no direct status-memory deletion.
                    bool success = Cancel(request);
                    planner.Record(request, success, now);
                    var name = buffs[request.StatusId].Name;
                    if (success) { removed++; lastAction = $"已取消：{name}"; }
                    else lastAction = $"遊戲未接受取消：{name}";
                    log.Debug("Raid Buff Off status {StatusId}, result {Success}.", request.StatusId, success);
                }
                var area = areaNames.TryGetValue(context.TerritoryId, out var label) ? label : $"區域 {context.TerritoryId}";
                view = new(context.TerritoryId, area, present.Select(s => s.StatusId).Distinct().ToArray(),
                    reason, removed, lastAction);
                UpdateDtr(reason);
            }
        }
        catch (Exception error)
        {
            log.Error(error, "Raid Buff Off disabled after an error.");
            lock (sync) { config.Enabled = false; planner.Reset(); Save(); UpdateDtr(GateReason.Off); }
            showSettings = true;
        }
    }
    private static unsafe bool Cancel(Cancellation request) =>
        NativeStatusManager.ExecuteStatusOff(request.StatusId, request.SourceId);

    private string Reason(GateReason reason) => reason switch {
        GateReason.Off => "總開關已關閉",
        GateReason.LoggedOut => "等待登入角色",
        GateReason.Unavailable => "等待角色載入或恢復行動",
        GateReason.OutOfCombat => "戰鬥外，待命中",
        GateReason.AreaNotSelected => "目前場景未勾選，待命中",
        GateReason.NoBuffsSelected => "尚未勾選要取消的增益",
        GateReason.Active => config.TestAllAreas ? "測試模式生效中：所有區域的戰鬥皆會生效" : "生效中：目前為已勾選場景的戰鬥",
        _ => ""
    };
    private void UpdateDtr(GateReason reason)
    {
        var text = config.Enabled ? "raidbuff: ON" : "raidbuff: OFF";
        var tooltip = $"團隊增益取消\n左鍵：ON / OFF\n右鍵：詳細設定\n目前：{Reason(reason)}\n範圍：{(config.TestAllAreas ? "測試區域：所有區域" : "已勾選的絕境戰")}\n本次已取消：{removed}\n{lastAction}";
        if (text != lastDtrText) { entry.Text = Text(text); lastDtrText = text; }
        if (tooltip != lastDtrTooltip) { entry.Tooltip = Text(tooltip); lastDtrTooltip = tooltip; }
    }

    private void Draw()
    {
        if (!showSettings || disposed) return;
        lock (sync)
        {
            ImGui.SetNextWindowSize(new Vector2(720, 640), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("團隊增益取消 / Raid Buff Off", ref showSettings))
            {
                var enabled = config.Enabled;
                if (ImGui.Checkbox("總開關 ON / OFF", ref enabled)) { config.Enabled = enabled; Save(); nextTick = 0; }
                ImGui.SameLine(); ImGui.TextUnformatted($"本次已取消：{view.Removed}");
                ImGui.TextWrapped(Reason(view.Reason));
                ImGui.TextWrapped(config.TestAllAreas
                    ? "測試區域已勾選：總開關 ON 且在戰鬥中，就會在所有區域取消已勾選的增益。"
                    : "只有總開關開啟、目前絕境戰已勾選，而且角色正在戰鬥中，才會取消已勾選的增益。");
                if (view.LastAction.Length > 0) ImGui.TextWrapped(view.LastAction);
                if (ImGui.BeginTabBar("settings"))
                {
                    if (ImGui.BeginTabItem("要移除的增益")) { DrawBuffs(); ImGui.EndTabItem(); }
                    if (ImGui.BeginTabItem("生效場景")) { DrawAreas(); ImGui.EndTabItem(); }
                    ImGui.EndTabBar();
                }
            }
            ImGui.End();
        }
    }
    private void DrawBuffs()
    {
        ImGui.TextWrapped("依職業勾選要取消的團隊增益。");
        ImGui.InputTextWithHint("搜尋增益", "名稱、英文名稱或狀態 ID", ref buffSearch, 160);
        ImGui.TextWrapped("同名增益集中為一個勾選項目。可將滑鼠移到名稱上查看說明與狀態 ID。");
        var visibleJobs = groups.Where(group => buffSearch.Length == 0 ||
            group.Name.Contains(buffSearch, StringComparison.OrdinalIgnoreCase) ||
            group.English.Contains(buffSearch, StringComparison.OrdinalIgnoreCase) ||
            group.Ids.Any(id => id.ToString().Contains(buffSearch)))
            .GroupBy(group => group.Definition.Job).ToArray();
        if (ImGui.BeginChild("buff-list", new Vector2(0, 0), true))
        {
            if (visibleJobs.Length == 0)
                ImGui.TextWrapped("沒有符合的團隊增益，請清除搜尋條件。");
            else if (ImGui.BeginTable("buff-job-grid", 3,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.BordersInnerV))
            {
                foreach (var job in visibleJobs)
                {
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(job.Key);
                    ImGui.Separator();
                    foreach (var group in job)
                    {
                        bool selected = group.Ids.Any(config.SelectedStatusIds.Contains);
                        var label = group.Name;
                        if (group.Name != group.English) label += $" / {group.English}";
                        if (group.Ids.Any(view.Present.Contains)) label += "（身上有）";
                        if (ImGui.Checkbox($"{label}##buff{group.Ids[0]}", ref selected))
                        {
                            foreach (uint id in group.Ids)
                                if (selected) config.SelectedStatusIds.Add(id); else config.SelectedStatusIds.Remove(id);
                            Save(); nextTick = 0;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            var style = ImGui.GetStyle();
                            var padding = style.WindowPadding;
                            var wrapWidth = MathF.Max(1f, MathF.Min(ImGui.GetFontSize() * 24f,
                                ImGui.GetMainViewport().WorkSize.X * 0.8f - padding.X * 2f));
                            var statusIds = "Status ID: " + string.Join(", ", group.Ids);
                            // Measure before BeginTooltip so its first frame cannot reuse a stale size.
                            var textHeight = MathF.Ceiling(ImGui.CalcTextSize(group.Description, false, wrapWidth).Y) +
                                MathF.Ceiling(ImGui.CalcTextSize(statusIds, false, wrapWidth).Y);
                            ImGui.SetNextWindowSize(new Vector2(wrapWidth + padding.X * 2f,
                                textHeight + style.ItemSpacing.Y + padding.Y * 2f), ImGuiCond.Always);
                            if (ImGui.BeginTooltip())
                            {
                                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrapWidth);
                                ImGui.TextUnformatted(group.Description);
                                ImGui.TextUnformatted(statusIds);
                                ImGui.PopTextWrapPos();
                                ImGui.EndTooltip();
                            }
                        }
                    }
                }
                ImGui.EndTable();
            }
        }
        ImGui.EndChild();
    }
    private void DrawAreas()
    {
        var testAllAreas = config.TestAllAreas;
        if (ImGui.Checkbox("測試區域：所有區域（僅戰鬥中生效）", ref testAllAreas))
        {
            config.TestAllAreas = testAllAreas;
            Save(); nextTick = 0;
        }
        ImGui.TextWrapped(config.TestAllAreas
            ? "測試模式已開啟，所有區域都可在戰鬥中生效；仍需總開關 ON。"
            : "只在以下已勾選絕境戰的戰鬥中生效。");
        ImGui.Separator();
        ImGui.TextWrapped($"目前場景：{view.Area} [{view.Territory}]");
        foreach (var area in areas.OrderBy(a => a.Order))
        {
            bool selected = config.SelectedTerritoryIds.Contains(area.Id);
            if (ImGui.Checkbox($"{area.Name}##area{area.Id}", ref selected))
            {
                if (selected) config.SelectedTerritoryIds.Add(area.Id); else config.SelectedTerritoryIds.Remove(area.Id);
                Save(); nextTick = 0;
            }
        }
        if (areas.Count != 7)
            ImGui.TextWrapped($"目前遊戲資料已讀取 {areas.Count}/7 個絕境戰；未讀取的內容不會啟用。");
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        framework.Update -= Tick;
        pi.UiBuilder.Draw -= Draw;
        pi.UiBuilder.OpenConfigUi -= Open;
        pi.UiBuilder.OpenMainUi -= Open;
        commands.RemoveHandler("/rbo");
        lock (sync)
        {
            entry.OnClick = null;
            entry.Dispose();
            planner.Reset();
        }
    }
}
