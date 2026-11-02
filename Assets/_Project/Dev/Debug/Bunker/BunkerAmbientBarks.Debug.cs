#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

public sealed partial class BunkerAmbientBarks
{
    public IReadOnlyList<BunkerBarkLine> DebugLines => lines;
    public string DebugLastAttempt { get; private set; } = "Показ ещё не запрашивался";
    public float DebugRandomRemaining => Mathf.Max(0, randomRemaining);
    public string DebugActiveCategory => IsSpeaking ? activeCategory.ToString() : "—";
    public string DebugBlockReason
    {
        get
        {
            var reasons = new List<string>();
            if (!isActiveAndEnabled) reasons.Add("контроллер выключен");
            if (view == null) reasons.Add("нет view");
            if (player == null || !player.gameObject.activeInHierarchy) reasons.Add("нет активного игрока");
            if (Time.timeScale <= 0f) reasons.Add("пауза");
            if (SceneTransitionOverlay.IsTransitioning) reasons.Add("переход сцены");
            if (panels != null && panels.IsAnyPanelOpen) reasons.Add("открыто игровое окно");
            if (intro != null && intro.IsPlaying) reasons.Add("интро");
            if (minigames != null) foreach (var game in minigames)
                if (game != null && game.isActiveAndEnabled && game.IsRunning) reasons.Add("мини-игра: " + game.name);
            if (blockingInterfaces != null) foreach (var root in blockingInterfaces)
                if (root != null && root.activeInHierarchy) reasons.Add("интерфейс: " + root.name);
            return reasons.Count > 0 ? string.Join(", ", reasons) : "нет";
        }
    }
    public bool DebugTryPlay(BunkerBarkCategory category, string key)
    {
        bool played = category switch
        {
            BunkerBarkCategory.Random => TryPlay(category, null, player),
            BunkerBarkCategory.Room => TryEnterRoom(key),
            _ => TryPlayEvent(key)
        };
        DebugLastAttempt = played ? "Показ принят" : DebugBlockReason != "нет" ? "Подавлено: " + DebugBlockReason :
            IsSpeaking && category <= activeCategory ? "Подавлено приоритетом активной реплики" :
            category == BunkerBarkCategory.Room && roomReadyAt.TryGetValue(key ?? "", out float ready) && quietTime < ready
                ? $"Кулдаун комнаты: {ready - quietTime:0} с" :
            "Нет подходящей строки: ключ, немедленный повтор или вероятность";
        return played;
    }
    public void DebugStop() => Hide();
}
#endif
