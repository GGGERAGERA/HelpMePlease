using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorldLootRewardReel : MonoBehaviour
{
    private enum ReelState
    {
        Hidden,
        Spinning,
        Braking,
        Snapping,
        RevealDelay,
        AwaitingClaim
    }

    private sealed class RewardCard
    {
        public RectTransform Rect;
        public Image Background;
        public Image Icon;
        public TextMeshProUGUI Label;
        public WorldLootRewardDefinition Reward;
    }

    [Header("Spin")]
    [SerializeField, Min(1)] private int visibleCardCount = 9;
    [SerializeField, Min(40f)] private float cardSpacing = 210f;
    [SerializeField, Min(1f)] private float spinSpeed = 620f;
    [SerializeField, Min(0f)] private float minimumSpinTime = 0.8f;

    [Header("Braking")]
    [SerializeField, Min(0.1f)] private float minBrakeDuration = 2.1f;
    [SerializeField, Min(0.1f)] private float maxBrakeDuration = 3.1f;
    [SerializeField, Min(0.5f)] private float minExtraTravelCards = 2.4f;
    [SerializeField, Min(0.5f)] private float maxExtraTravelCards = 4.6f;
    [SerializeField] private AnimationCurve brakingCurve =
        new(
            new Keyframe(0f, 0f, 2f, 2f),
            new Keyframe(1f, 1f, 0f, 0f)
        );
    [SerializeField, Min(0.05f)] private float snapDuration = 0.28f;
    [SerializeField, Min(0f)] private float revealDelay = 0.35f;

    private static WorldLootRewardReel instance;
    private static string lastClaimedReward;

    private readonly List<WorldLootRewardDefinition> rewards = new();
    private readonly List<RewardCard> cards = new();

    private GameObject canvasRoot;
    private RectTransform cardsRoot;
    private Button stopButton;
    private TextMeshProUGUI stopButtonText;
    private GameObject revealRoot;
    private TextMeshProUGUI revealText;
    private Button claimButton;
    private TextMeshProUGUI statusText;
    private ReelState state;
    private Action<WorldLootRewardDefinition> claimedCallback;
    private RewardCard winningCard;
    private float stateElapsed;
    private float brakeDuration;
    private float brakeDistance;
    private float previousBrakeProgress;
    private float snapStartX;
    private float previousSnapShift;
    private float previousTimeScale = 1f;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool ownsPause;
    private bool rewardApplied;

    public static bool IsModalOpen => instance != null &&
        instance.state != ReelState.Hidden;
    public static string LastClaimedReward => lastClaimedReward;

    public static bool TryShow(
        IReadOnlyList<WorldLootRewardDefinition> rewardPool,
        Action<WorldLootRewardDefinition> onClaimed)
    {
        if (IsModalOpen || rewardPool == null || rewardPool.Count == 0)
            return false;

        EnsureInstance();
        return instance.ShowInternal(rewardPool, onClaimed);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = new(
            "World Loot Reward Reel (Runtime)",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        instance = root.AddComponent<WorldLootRewardReel>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildUi();
        canvasRoot.SetActive(false);
        state = ReelState.Hidden;
    }

    private bool ShowInternal(
        IReadOnlyList<WorldLootRewardDefinition> rewardPool,
        Action<WorldLootRewardDefinition> onClaimed)
    {
        rewards.Clear();

        for (int i = 0; i < rewardPool.Count; i++)
        {
            if (rewardPool[i] != null)
                rewards.Add(rewardPool[i]);
        }

        if (rewards.Count == 0)
            return false;

        EnsureEventSystem();
        claimedCallback = onClaimed;
        rewardApplied = false;
        winningCard = null;
        stateElapsed = 0f;
        state = ReelState.Spinning;
        statusText.text = "РУЛЕТКА ЗАПУЩЕНА";
        revealRoot.SetActive(false);
        stopButton.gameObject.SetActive(true);
        stopButton.interactable = false;
        stopButtonText.text = "STOP";
        ResetCards();
        AcquirePause();
        canvasRoot.SetActive(true);
        return true;
    }

    private void Update()
    {
        if (state == ReelState.Hidden)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        stateElapsed += deltaTime;

        switch (state)
        {
            case ReelState.Spinning:
                MoveCards(spinSpeed * deltaTime);
                stopButton.interactable = stateElapsed >= minimumSpinTime;
                statusText.text = stopButton.interactable
                    ? "НАЖМИТЕ STOP"
                    : "РАЗГОН...";
                break;
            case ReelState.Braking:
                UpdateBraking();
                break;
            case ReelState.Snapping:
                UpdateSnapping();
                break;
            case ReelState.RevealDelay:
                if (stateElapsed >= revealDelay)
                    ShowReveal();
                break;
        }
    }

    private void BeginBraking()
    {
        if (state != ReelState.Spinning ||
            stateElapsed < minimumSpinTime)
        {
            return;
        }

        state = ReelState.Braking;
        stateElapsed = 0f;
        previousBrakeProgress = 0f;
        brakeDuration = UnityEngine.Random.Range(
            Mathf.Min(minBrakeDuration, maxBrakeDuration),
            Mathf.Max(minBrakeDuration, maxBrakeDuration)
        );
        float minimumTravel = Mathf.Min(
            minExtraTravelCards,
            maxExtraTravelCards
        );
        float maximumTravel = Mathf.Max(
            minExtraTravelCards,
            maxExtraTravelCards
        );
        float nonAcceleratingTravelLimit =
            spinSpeed * brakeDuration * 0.5f / cardSpacing;
        maximumTravel = Mathf.Max(
            minimumTravel,
            Mathf.Min(maximumTravel, nonAcceleratingTravelLimit)
        );
        float travelCards = UnityEngine.Random.Range(
            minimumTravel,
            maximumTravel
        );
        brakeDistance = travelCards * cardSpacing;
        stopButton.interactable = false;
        stopButtonText.text = "ТОРМОЖЕНИЕ";
        statusText.text = "РУЛЕТКА ЗАМЕДЛЯЕТСЯ";
    }

    private void UpdateBraking()
    {
        float t = Mathf.Clamp01(stateElapsed / Mathf.Max(0.01f, brakeDuration));
        float progress = EvaluateBrakeProgress(t);
        float deltaDistance =
            (progress - previousBrakeProgress) * brakeDistance;
        previousBrakeProgress = progress;
        MoveCards(Mathf.Max(0f, deltaDistance));

        if (t < 1f)
            return;

        winningCard = FindClosestCard();

        if (winningCard == null)
        {
            CloseWithoutReward();
            return;
        }

        state = ReelState.Snapping;
        stateElapsed = 0f;
        snapStartX = winningCard.Rect.anchoredPosition.x;
        previousSnapShift = 0f;
        statusText.text = "ФИКСАЦИЯ НАГРАДЫ";
    }

    private void UpdateSnapping()
    {
        float t = Mathf.Clamp01(stateElapsed / Mathf.Max(0.01f, snapDuration));
        float eased = t * t * (3f - 2f * t);
        float targetShift = -snapStartX * eased;
        ShiftCards(targetShift - previousSnapShift);
        previousSnapShift = targetShift;

        if (t < 1f)
            return;

        ShiftCards(-winningCard.Rect.anchoredPosition.x);
        HighlightWinner();
        state = ReelState.RevealDelay;
        stateElapsed = 0f;
        statusText.text = "ПОБЕДИТЕЛЬ ОПРЕДЕЛЁН";
    }

    private float EvaluateBrakeProgress(float t)
    {
        if (brakingCurve == null || brakingCurve.length < 2)
            return 1f - Mathf.Pow(1f - t, 3f);

        return Mathf.Clamp01(brakingCurve.Evaluate(t));
    }

    private void ShowReveal()
    {
        if (winningCard == null || winningCard.Reward == null)
        {
            CloseWithoutReward();
            return;
        }

        revealText.text =
            $"<size=24>ПОЛУЧЕНО</size>\n<b>{winningCard.Reward.DisplayName}</b>";
        revealRoot.SetActive(true);
        stopButton.gameObject.SetActive(false);
        claimButton.interactable = true;
        state = ReelState.AwaitingClaim;
        statusText.text = "НАГРАДА ГОТОВА";
    }

    private void Claim()
    {
        if (state != ReelState.AwaitingClaim || rewardApplied ||
            winningCard?.Reward == null)
        {
            return;
        }

        claimButton.interactable = false;

        if (!winningCard.Reward.Apply())
        {
            claimButton.interactable = true;
            statusText.text = "НЕ УДАЛОСЬ НАЧИСЛИТЬ НАГРАДУ";
            return;
        }

        rewardApplied = true;
        WorldLootRewardDefinition claimed = winningCard.Reward;
        lastClaimedReward = claimed.DisplayName;
        CloseModal();
        claimedCallback?.Invoke(claimed);
        claimedCallback = null;
    }

    private void ResetCards()
    {
        int count = cards.Count;
        float center = (count - 1) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            RewardCard card = cards[i];
            card.Rect.anchoredPosition =
                new Vector2((i - center) * cardSpacing, 0f);
            card.Rect.localScale = Vector3.one;
            card.Background.color = new Color(0.1f, 0.14f, 0.2f, 1f);
            AssignReward(card, RollWeightedReward());
        }
    }

    private void MoveCards(float distance)
    {
        if (distance <= 0f)
            return;

        ShiftCards(-distance);
        float recycleBoundary = cardSpacing * (cards.Count * 0.5f + 0.5f);

        for (int i = 0; i < cards.Count; i++)
        {
            RewardCard card = cards[i];

            if (card.Rect.anchoredPosition.x >= -recycleBoundary)
                continue;

            float rightmost = FindRightmostX();
            card.Rect.anchoredPosition = new Vector2(
                rightmost + cardSpacing,
                card.Rect.anchoredPosition.y
            );
            AssignReward(card, RollWeightedReward());
        }
    }

    private void ShiftCards(float deltaX)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            Vector2 position = cards[i].Rect.anchoredPosition;
            position.x += deltaX;
            cards[i].Rect.anchoredPosition = position;
        }
    }

    private float FindRightmostX()
    {
        float result = float.MinValue;

        for (int i = 0; i < cards.Count; i++)
            result = Mathf.Max(result, cards[i].Rect.anchoredPosition.x);

        return result;
    }

    private RewardCard FindClosestCard()
    {
        RewardCard closest = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < cards.Count; i++)
        {
            float distance = Mathf.Abs(cards[i].Rect.anchoredPosition.x);

            if (distance >= closestDistance)
                continue;

            closest = cards[i];
            closestDistance = distance;
        }

        return closest;
    }

    private void HighlightWinner()
    {
        if (winningCard == null)
            return;

        winningCard.Rect.localScale = Vector3.one * 1.12f;
        winningCard.Background.color = new Color(0.22f, 0.64f, 0.72f, 1f);
    }

    private WorldLootRewardDefinition RollWeightedReward()
    {
        float totalWeight = 0f;

        for (int i = 0; i < rewards.Count; i++)
            totalWeight += rewards[i].Weight;

        float roll = UnityEngine.Random.value * totalWeight;

        for (int i = 0; i < rewards.Count; i++)
        {
            roll -= rewards[i].Weight;

            if (roll <= 0f)
                return rewards[i];
        }

        return rewards[rewards.Count - 1];
    }

    private static void AssignReward(
        RewardCard card,
        WorldLootRewardDefinition reward)
    {
        card.Reward = reward;
        card.Label.text = reward != null ? reward.DisplayName : "—";
        bool hasIcon = reward != null && reward.Icon != null;
        card.Icon.gameObject.SetActive(hasIcon);

        if (hasIcon)
            card.Icon.sprite = reward.Icon;
    }

    private void AcquirePause()
    {
        previousTimeScale = Time.timeScale;
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ownsPause = true;
    }

    private void RestorePause()
    {
        if (!ownsPause)
            return;

        Time.timeScale = previousTimeScale;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        ownsPause = false;
    }

    private void CloseModal()
    {
        state = ReelState.Hidden;
        canvasRoot.SetActive(false);
        RestorePause();
    }

    private void CloseWithoutReward()
    {
        claimedCallback = null;
        CloseModal();
    }

    private void BuildUi()
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 1;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot = gameObject;
        RectTransform blocker = CreateRect("Input Blocker", transform);
        Stretch(blocker);
        Image blockerImage = blocker.gameObject.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0.78f);
        blockerImage.raycastTarget = true;

        RectTransform panel = CreateRect("Reward Reel Panel", blocker);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(1160f, 570f);
        panel.gameObject.AddComponent<Image>().color =
            new Color(0.035f, 0.05f, 0.075f, 0.99f);

        TextMeshProUGUI title = CreateText(
            "Title", panel, "НАГРАДА ИЗ СУНДУКА", 34f,
            TextAlignmentOptions.Center, Color.white
        );
        SetRect(title.rectTransform, new Vector2(0f, 205f),
            new Vector2(900f, 54f));

        statusText = CreateText(
            "Status", panel, string.Empty, 18f,
            TextAlignmentOptions.Center,
            new Color(0.58f, 0.82f, 0.9f, 1f)
        );
        SetRect(statusText.rectTransform, new Vector2(0f, 164f),
            new Vector2(700f, 30f));

        RectTransform viewport = CreateRect("Reel Viewport", panel);
        SetRect(viewport, new Vector2(0f, 30f), new Vector2(1040f, 230f));
        viewport.gameObject.AddComponent<Image>().color =
            new Color(0.015f, 0.025f, 0.04f, 1f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        cardsRoot = CreateRect("Recycled Cards", viewport);
        Stretch(cardsRoot);

        int count = Mathf.Clamp(visibleCardCount, 7, 11);
        for (int i = 0; i < count; i++)
            cards.Add(CreateCard(cardsRoot, i));

        RectTransform marker = CreateRect("Winner Marker", viewport);
        marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        marker.sizeDelta = new Vector2(8f, 222f);
        marker.gameObject.AddComponent<Image>().color =
            new Color(1f, 0.78f, 0.18f, 0.9f);

        TextMeshProUGUI markerLabel = CreateText(
            "Winner Label", viewport, "▲ WINNER", 17f,
            TextAlignmentOptions.Center,
            new Color(1f, 0.82f, 0.26f, 1f)
        );
        SetRect(markerLabel.rectTransform, new Vector2(0f, -102f),
            new Vector2(180f, 30f));

        stopButton = CreateButton(
            "Stop", panel, "STOP", BeginBraking,
            new Vector2(0f, -137f), new Vector2(240f, 62f),
            out stopButtonText
        );

        RectTransform reveal = CreateRect("Reveal", panel);
        revealRoot = reveal.gameObject;
        SetRect(reveal, new Vector2(0f, -210f), new Vector2(780f, 120f));
        reveal.gameObject.AddComponent<Image>().color =
            new Color(0.08f, 0.13f, 0.18f, 0.98f);

        revealText = CreateText(
            "Reward", reveal, string.Empty, 32f,
            TextAlignmentOptions.Center, Color.white
        );
        Stretch(revealText.rectTransform, 14f, 220f, 8f, 8f);

        claimButton = CreateButton(
            "Claim", reveal, "ЗАБРАТЬ", Claim,
            new Vector2(278f, 0f), new Vector2(190f, 54f), out _
        );
        revealRoot.SetActive(false);
    }

    private RewardCard CreateCard(Transform parent, int index)
    {
        RectTransform rect = CreateRect($"Reward Card {index}", parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(178f, 170f);
        Image background = rect.gameObject.AddComponent<Image>();

        RectTransform iconRect = CreateRect("Icon", rect);
        SetRect(iconRect, new Vector2(0f, 30f), new Vector2(54f, 54f));
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TextMeshProUGUI label = CreateText(
            "Label", rect, string.Empty, 25f,
            TextAlignmentOptions.Center, Color.white
        );
        SetRect(label.rectTransform, new Vector2(0f, -30f),
            new Vector2(164f, 70f));
        label.textWrappingMode = TextWrappingModes.Normal;

        return new RewardCard
        {
            Rect = rect,
            Background = background,
            Icon = icon,
            Label = label
        };
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject target = new(name, typeof(RectTransform));
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        UnityEngine.Events.UnityAction action,
        Vector2 position,
        Vector2 size,
        out TextMeshProUGUI labelText)
    {
        RectTransform rect = CreateRect(name, parent);
        SetRect(rect, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.55f, 0.7f, 1f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        labelText = CreateText(
            "Label", rect, label, 23f,
            TextAlignmentOptions.Center, Color.white
        );
        Stretch(labelText.rectTransform);
        return button;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(
        RectTransform rect,
        float left = 0f,
        float right = 0f,
        float bottom = 0f,
        float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystem = new(
            "EventSystem (World Loot Runtime)",
            typeof(EventSystem),
            typeof(StandaloneInputModule)
        );
        DontDestroyOnLoad(eventSystem);
    }

    private void OnDestroy()
    {
        RestorePause();

        if (instance == this)
            instance = null;
    }
}
