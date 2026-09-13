#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

public sealed class OrbitalRewardLabController : OrbitalLabSession
{
    public const string ScenePath = "Assets/_Project/Scenes/Dev/Labs/OrbitalRewardLab.unity";
    private int tab;
    public void OpenNormalReward() { if (CanEdit()) Rewards.ShowUpgradeChoices(); }
    public void OpenRandomReward()
    {
        if (!CanEdit()) return;
        var choices = Provider.GetEligibleKinds();
        if (choices.Count > 0) Rewards.DebugForceOrbitalReward(choices[UnityEngine.Random.Range(0, choices.Count)]);
    }
    public void GiveRewards(int count)
    {
        if (!CanEdit()) return;
        int applied = 0;
        for (int i = 0; i < count; i++)
        {
            var choices = Provider.GetEligibleKinds();
            if (choices.Count == 0) break;
            // Use a currently eligible ring for bulk grants; individual Direct Give respects the selected ring.
            var kind = choices[UnityEngine.Random.Range(0, choices.Count)];
            int target = Station.State.Rings.FindIndex(r => Station.State.CanTargetRingReward(kind, r.StableRingId));
            if (target >= 0) TargetRing = target;
            if (Give(kind)) applied++;
        }
        Notice = $"Direct bulk grants: {applied}/{count}";
    }
    protected override void DrawPanel()
    {
        Button("RESET BUILD", () => ResetBuild());
        Text($"Reward: {(Rewards.IsChoosingUpgrade && Station.RewardFlow.PendingReward == null ? "Cards" : Station.RewardFlow.CompactStatus)}");
        Text($"Placement: {Station.InputOwner.Mode} | Links: {Station.State.ResolveLinkPairs().Count()}");
        tab = GUILayout.Toolbar(tab, new[] { "Rewards", "Build / Orbit", "Live state" });
        if (tab == 2) { DrawBuildInfo(true); return; }
        if (tab == 0)
        {
        Heading("Reward Flow — real production cards");
        Button("OPEN NORMAL REWARD", OpenNormalReward);
        Button("OPEN RANDOM REWARD", OpenRandomReward);
        Row(("GIVE 3 REWARDS", () => GiveRewards(3)), ("GIVE 10 REWARDS", () => GiveRewards(10)));
        Text("Give 3/10 = instant eligible grants (no cards)");
        DrawRingTarget();
        Heading("Direct Give — no cards");
        int column = 0;
        foreach (OrbitalRewardKind kind in Enum.GetValues(typeof(OrbitalRewardKind)))
        {
            var definition = Provider.GetDefinition(kind, Station.State);
            if (definition == null) continue;
            if (column % 2 == 0) GUILayout.BeginHorizontal();
            Button(kind == OrbitalRewardKind.LinkPair ? "Link Pair (2 nodes)" :
                System.Text.RegularExpressions.Regex.Replace(kind.ToString(), "([a-z])([A-Z])", "$1 $2"), () => Give(kind));
            if (++column % 2 == 0) GUILayout.EndHorizontal();
        }
        if (column % 2 != 0) GUILayout.EndHorizontal();
        return;
        }
        DrawRingTarget();
        Heading("Build controls");
        Button("CLEAR MODULES", ClearModules);
        Row(("ADD RING", () => Give(OrbitalRewardKind.NewRing)), ("ADD MOUNT", () => Give(OrbitalRewardKind.AddMount)),
            ("CORE +1", () => Give(OrbitalRewardKind.CoreUpgrade)));
        Button("REMOVE LAST MODULE", () =>
        {
            if (CanEdit() && Station.State.Modules.Count > 0) Station.RemoveModule(Station.State.Modules.Last().StableModuleId);
        });
        Button("REMOVE LAST RING", () =>
        {
            if (CanEdit()) Notice = Station.RemoveRing(Station.State.Rings.Last().StableRingId, out var error) ? "Ring removed" : error;
        });
        Button("MANY RINGS preset", () => ApplyPreset("MANY RINGS"));
        DrawOrbitControls();
    }
}
#endif
