#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class Subject42DebugMenu
{
    private bool IsProductionBunker =>
        SceneManager.GetActiveScene().path == "Assets/_Project/Scenes/MainBuild/MainMenu.unity" &&
        BunkerContext.Instance != null && BunkerContext.Instance.gameObject.scene == SceneManager.GetActiveScene();

    private BunkerGateVisual[] BunkerDoors() => FindObjectsByType<BunkerGateVisual>(FindObjectsSortMode.None)
        .Where(door => door.gameObject.scene == SceneManager.GetActiveScene())
        .OrderBy(door => BunkerDoorName(door), StringComparer.Ordinal)
        .ThenBy(door => door.transform.position.x)
        .ThenBy(door => door.transform.position.y)
        .ToArray();

    private void AddBunkerSection()
    {
        if (!IsProductionBunker) return;
        var doors = BunkerDoors();
        AddHint("Двери меняются временно, до перезагрузки сцены.");
        BunkerActionRow("Все двери", null, "Открыть все", () => SetAllBunkerDoors(true),
            "Закрыть все", () => SetAllBunkerDoors(false), doors.Length > 0);
        foreach (var door in doors)
        {
            string state = door.DebugIsLocked ? "Заблокирована" : door.IsOpen ? "Открыта" : "Закрыта";
            if (door.DebugHasOverride) state += " · вручную";
            BunkerActionRow(BunkerDoorName(door), state,
                door.IsOpen ? "Закрыть" : "Открыть", () =>
                {
                    if (door != null) door.DebugSetOpen(!door.IsOpen);
                    RefreshCurrentTab();
                }, door.DebugIsLocked ? "Разблокировать" : "Заблокировать", () =>
                {
                    if (door != null) door.DebugSetLocked(!door.DebugIsLocked);
                    RefreshCurrentTab();
                });
        }
        if (doors.Length == 0) AddHint("Двери не найдены.");
        AddRow("Обычный режим", "Вернуть двери под управление игры", mutedColor, "Вернуть",
            doors.Any(door => door.DebugHasOverride), () =>
            {
                foreach (var door in doors) if (door != null) door.DebugResetOverride();
                RefreshCurrentTab();
            });

        var wallet = CurrencyManager.Instance;
        BunkerActionRow("Деньги", wallet != null ? $"{wallet.TotalGold} монет" : "Кошелёк недоступен",
            "+100", () => AddBunkerDebugGold(100), "+1000", () => AddBunkerDebugGold(1000), wallet != null);
    }

    private void SetAllBunkerDoors(bool open)
    {
        if (!IsProductionBunker) return;
        foreach (var door in BunkerDoors()) door.DebugSetOpen(open);
        RefreshCurrentTab();
    }

    private void AddBunkerDebugGold(int amount)
    {
        if (!IsProductionBunker || CurrencyManager.Instance == null) return;
        CurrencyManager.Instance.AddGoldExact(amount);
        RefreshCurrentTab();
    }

    private static string BunkerDoorName(BunkerGateVisual door)
    {
        string key = door.name.ToLowerInvariant();
        if (key.Contains("character")) return "Персонажи";
        if (key.Contains("module") || key.Contains("weapon")) return "Модули";
        if (key.Contains("football")) return "Футбол";
        if (key.Contains("gallery")) return "Галерея";
        if (key.Contains("chill")) return "Комната отдыха";
        if (key.Contains("arcade")) return "Игровая";
        if (key.Contains("troph")) return "Трофеи";
        for (var parent = door.transform; parent != null; parent = parent.parent)
            if (parent.name == "RUN_EXIT") return "Выход на поверхность";
        var room = door.GetComponentInParent<BunkerRoomAccess>();
        if (room != null)
            return room.RoomId switch
            {
                BunkerRoomId.CharacterSelection => "Персонажи",
                BunkerRoomId.WeaponSelection => "Оружие",
                BunkerRoomId.UpgradeStation => "Улучшения",
                BunkerRoomId.AnomalyStation => "Аномалии",
                BunkerRoomId.SecretRoom => "Тайная комната",
                _ => "Дверь"
            };
        return "Дверь";
    }

    private void BunkerActionRow(string title, string state,
        string firstLabel, UnityEngine.Events.UnityAction firstAction,
        string secondLabel, UnityEngine.Events.UnityAction secondAction, bool available = true)
    {
        var row = CreateRect(title, contentRoot);
        row.gameObject.AddComponent<Image>().color = rowColor;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
        var text = CreateText("Название", row,
            string.IsNullOrEmpty(state) ? title : $"<b>{title}</b>\n<size=14>{state}</size>",
            18f, TextAlignmentOptions.MidlineLeft, Color.white);
        Stretch(text.rectTransform, 12f, 258f, 4f, 4f);
        text.overflowMode = TextOverflowModes.Ellipsis;
        PlaceButton(firstLabel, firstAction, -136f);
        PlaceButton(secondLabel, secondAction, -8f);

        void PlaceButton(string label, UnityEngine.Events.UnityAction action, float x)
        {
            var button = CreateButton(row, label, action, 120f, available);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
            rect.pivot = new Vector2(1f, .5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(120f, 36f);
            button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 14f;
        }
    }
}
#endif
