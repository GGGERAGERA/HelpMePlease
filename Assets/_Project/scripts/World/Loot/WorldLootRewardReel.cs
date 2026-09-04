using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorldLootRewardReel : MonoBehaviour
{
    private enum ReelState
    {
        Hidden,
        Transfer,
        Revealing,
        Spinning,
        Braking,
        Snapping,
        RevealDelay,
        Result,
        Closing
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
    [SerializeField, Min(40f)] private float cardSpacing = 110f;
    [SerializeField, Min(1f)] private float spinSpeed = 620f;
    [SerializeField, Min(0f)] private float minimumSpinTime = 0.8f;
    [SerializeField, Min(0.1f)] private float autoStopTime = 5f;
    [SerializeField] private KeyCode stopHotkey = KeyCode.R;

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
    [SerializeField, Min(0f)] private float resultDisplayTime = 0.8f;

    [Header("Presentation")]
    [SerializeField] private Vector2 panelSize = new(540f, 144f);
    [SerializeField, Min(0.05f)] private float transferDuration = 0.3f;
    [SerializeField, Min(0.05f)] private float panelRevealDuration = 0.18f;
    [SerializeField, Min(0.05f)] private float panelCloseDuration = 0.2f;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip reelStartClip;
    [SerializeField] private AudioClip cardTickClip;
    [SerializeField] private AudioClip stopClip;
    [SerializeField] private AudioClip rewardClip;

    private static WorldLootRewardReel instance;
    private static string lastClaimedReward;
    private static bool openingReserved;
    private static bool missingReported;
    private bool viewValid;

    private readonly List<WorldLootRewardDefinition> rewards = new();
    private readonly List<RewardCard> cards = new();

    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private RectTransform transferPacket;
    [SerializeField] private Image transferPacketImage;
    [SerializeField] private RectTransform markerRect;
    [SerializeField] private Image markerImage;
    [SerializeField] private RectTransform cardsRoot;
    [SerializeField] private Button stopButton;
    [SerializeField] private TextMeshProUGUI stopButtonText;
    [SerializeField] private GameObject revealRoot;
    [SerializeField] private TextMeshProUGUI revealText;
    [SerializeField] private TextMeshProUGUI statusText;
    private ReelState state;
    private Action<WorldLootRewardDefinition> claimedCallback;
    private RewardCard winningCard;
    private float stateElapsed;
    private float brakeDuration;
    private float brakeDistance;
    private float previousBrakeProgress;
    private float snapStartX;
    private float previousSnapShift;
    private bool rewardApplied;
    private PlayerHealth observedPlayerHealth;
    private bool hadObservedPlayer;
    private Vector2 transferStart;
    private Vector2 transferTarget;
    private float markerPulse;

    public static bool IsActive => openingReserved ||
        (instance != null && instance.state != ReelState.Hidden);
    public static string LastClaimedReward => lastClaimedReward;
    public static Vector2 PresentationPanelSize => instance != null
        ? instance.panelSize
        : new Vector2(540f, 144f);
    public static float PresentationTransferDuration => instance != null
        ? instance.transferDuration
        : 0.3f;
    public static string ActiveStateLabel => instance == null
        ? openingReserved ? "OPENING" : "IDLE"
        : instance.state == ReelState.Hidden && openingReserved
            ? "OPENING"
            : instance.GetStateLabel();

    public static bool TryReserveOpening()
    {
        if (IsActive)
            return false;

        openingReserved = true;
        return true;
    }

    public static void ReleaseOpeningReservation()
    {
        openingReserved = false;
    }

    public static bool TryShow(
        IReadOnlyList<WorldLootRewardDefinition> rewardPool,
        Vector3 chestWorldPosition,
        Action<WorldLootRewardDefinition> onClaimed)
    {
        if ((instance != null && instance.state != ReelState.Hidden) ||
            rewardPool == null || rewardPool.Count == 0)
            return false;

        if (instance == null || !instance.viewValid)
        {
            if (!missingReported)
            {
                Debug.LogError("[WorldLootRewardReel] Authored reel view is missing or invalid.");
                missingReported = true;
            }
            openingReserved = false;
            return false;
        }
        return instance.ShowInternal(
            rewardPool,
            chestWorldPosition,
            onClaimed
        );
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        missingReported = false;
        viewValid = canvasRoot != null && canvasRect != null && panelRect != null &&
            panelCanvasGroup != null && transferPacket != null && transferPacketImage != null &&
            markerRect != null && markerImage != null && cardsRoot != null && stopButton != null &&
            stopButtonText != null && revealRoot != null && revealText != null && statusText != null;
        if (!viewValid)
        {
            Debug.LogError("[WorldLootRewardReel] Authored shell references are missing.", this);
            missingReported = true;
            enabled = false;
            gameObject.SetActive(false);
            return;
        }
        int count = Mathf.Clamp(visibleCardCount, 7, 11);
        for (int i = 0; i < count; i++)
            cards.Add(CreateCard(cardsRoot, i));
        stopButton.onClick.AddListener(BeginBraking);
        canvasRoot.SetActive(false);
        state = ReelState.Hidden;
    }

    private bool ShowInternal(
        IReadOnlyList<WorldLootRewardDefinition> rewardPool,
        Vector3 chestWorldPosition,
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

        claimedCallback = onClaimed;
        rewardApplied = false;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        observedPlayerHealth = player != null
            ? player.GetComponent<PlayerHealth>()
            : null;
        hadObservedPlayer = observedPlayerHealth != null;
        winningCard = null;
        stateElapsed = 0f;
        state = ReelState.Transfer;
        statusText.text = "ДЕКОДИРОВАНИЕ";
        revealRoot.SetActive(false);
        stopButton.gameObject.SetActive(true);
        stopButton.interactable = false;
        stopButtonText.text = $"[{stopHotkey} — СТОП]";
        ResetCards();
        canvasRoot.SetActive(true);
        PrepareTransfer(chestWorldPosition);
        openingReserved = false;
        return true;
    }

    private void Update()
    {
        if (state == ReelState.Hidden)
            return;

        if (ShouldAbortForRunLifecycle())
        {
            CloseWithoutReward();
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;
        stateElapsed += deltaTime;
        UpdateMarkerPulse(deltaTime);

        switch (state)
        {
            case ReelState.Transfer:
                UpdateTransfer();
                break;
            case ReelState.Revealing:
                UpdatePanelReveal();
                break;
            case ReelState.Spinning:
                MoveCards(spinSpeed * deltaTime);
                stopButton.interactable = stateElapsed >= minimumSpinTime;
                statusText.text = stopButton.interactable
                    ? $"[{stopHotkey} — СТОП]"
                    : "СИНХРОНИЗАЦИЯ...";

                if (stateElapsed >= minimumSpinTime &&
                    Input.GetKeyDown(stopHotkey))
                {
                    BeginBraking();
                }
                else if (stateElapsed >= Mathf.Max(
                    minimumSpinTime,
                    autoStopTime))
                {
                    BeginBraking();
                }
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
            case ReelState.Result:
                if (stateElapsed >= resultDisplayTime)
                {
                    if (rewardApplied)
                        BeginClosing();
                    else
                        CloseWithoutReward();
                }
                break;
            case ReelState.Closing:
                UpdatePanelClose();
                break;
        }
    }

    private void PrepareTransfer(Vector3 chestWorldPosition)
    {
        Canvas.ForceUpdateCanvases();
        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
        panelRect.localScale = Vector3.one * 0.85f;
        transferPacket.gameObject.SetActive(true);
        transferPacketImage.color = new Color(0.2f, 1f, 0.95f, 1f);

        Vector2 startScreen = new(Screen.width * 0.5f, Screen.height * 0.5f);
        Camera worldCamera = Camera.main;

        if (worldCamera != null)
        {
            Vector3 projected = worldCamera.WorldToScreenPoint(
                chestWorldPosition
            );

            if (projected.z > 0f)
                startScreen = projected;
        }

        Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(
            null,
            panelRect.position
        );
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            startScreen,
            null,
            out transferStart
        );
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            targetScreen,
            null,
            out transferTarget
        );
        transferPacket.anchoredPosition = transferStart;
    }

    private void UpdateTransfer()
    {
        float t = Mathf.Clamp01(
            stateElapsed / Mathf.Max(0.01f, transferDuration)
        );
        float eased = t * t * (3f - 2f * t);
        transferPacket.anchoredPosition = Vector2.LerpUnclamped(
            transferStart,
            transferTarget,
            eased
        );
        float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.45f;
        transferPacket.localScale = Vector3.one * pulse;

        if (t < 1f)
            return;

        transferPacket.gameObject.SetActive(false);
        state = ReelState.Revealing;
        stateElapsed = 0f;
    }

    private void UpdatePanelReveal()
    {
        float t = Mathf.Clamp01(
            stateElapsed / Mathf.Max(0.01f, panelRevealDuration)
        );
        panelCanvasGroup.alpha = t;
        panelRect.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, t);

        if (t < 1f)
            return;

        panelCanvasGroup.alpha = 1f;
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;
        panelRect.localScale = Vector3.one;
        state = ReelState.Spinning;
        stateElapsed = 0f;
        statusText.text = "СИНХРОНИЗАЦИЯ...";
        PlayOptional(reelStartClip);
    }

    private void BeginClosing()
    {
        state = ReelState.Closing;
        stateElapsed = 0f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
    }

    private void UpdatePanelClose()
    {
        float t = Mathf.Clamp01(
            stateElapsed / Mathf.Max(0.01f, panelCloseDuration)
        );
        panelCanvasGroup.alpha = 1f - t;
        panelRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.95f, t);

        if (t >= 1f)
            CloseOverlay();
    }

    private void UpdateMarkerPulse(float deltaTime)
    {
        markerPulse = Mathf.Max(0f, markerPulse - deltaTime * 8f);
        float amount = Mathf.Clamp01(markerPulse);
        markerRect.localScale = Vector3.one * Mathf.Lerp(1f, 1.18f, amount);
        markerImage.color = Color.Lerp(
            new Color(1f, 0.78f, 0.18f, 0.9f),
            new Color(0.25f, 1f, 0.95f, 1f),
            amount
        );
    }

    private void TriggerMarkerPulse(float strength = 1f)
    {
        markerPulse = Mathf.Max(markerPulse, strength);
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
        stopButtonText.text = "ОСТАНОВКА...";
        statusText.text = "ОСТАНОВКА...";
        TriggerMarkerPulse(1.4f);
        PlayOptional(stopClip);
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

        WorldLootRewardDefinition reward = winningCard.Reward;

        if (!reward.Apply())
        {
            revealText.text = "ОШИБКА НАЧИСЛЕНИЯ";
            statusText.text = "НАГРАДА НЕ ПРИМЕНЕНА";
            revealRoot.SetActive(true);
            stopButton.gameObject.SetActive(false);
            state = ReelState.Result;
            stateElapsed = 0f;
            return;
        }

        rewardApplied = true;
        lastClaimedReward = reward.DisplayName;
        revealText.text = $"+{reward.DisplayName}";
        revealRoot.SetActive(true);
        stopButton.gameObject.SetActive(false);
        statusText.text = "ПОЛУЧЕНО";
        state = ReelState.Result;
        stateElapsed = 0f;
        PlayOptional(rewardClip);
        claimedCallback?.Invoke(reward);
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

        bool crossedMarker = false;

        for (int i = 0; i < cards.Count; i++)
        {
            float previousX = cards[i].Rect.anchoredPosition.x;

            if (previousX > 0f && previousX - distance <= 0f)
                crossedMarker = true;
        }

        if (crossedMarker)
        {
            TriggerMarkerPulse();
            PlayOptional(cardTickClip);
        }

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
        card.Background.color = new Color(0.1f, 0.14f, 0.2f, 1f);
        bool hasIcon = reward != null && reward.Icon != null;
        card.Icon.gameObject.SetActive(hasIcon);

        if (hasIcon)
            card.Icon.sprite = reward.Icon;
    }

    private string GetStateLabel()
    {
        return state switch
        {
            ReelState.Transfer => "TRANSFER",
            ReelState.Revealing => "BOOT",
            ReelState.Spinning => "SPINNING",
            ReelState.Braking => "BRAKING",
            ReelState.Snapping => "BRAKING",
            ReelState.RevealDelay => "RESULT",
            ReelState.Result => "RESULT",
            ReelState.Closing => "CLOSING",
            _ => "IDLE"
        };
    }

    private bool ShouldAbortForRunLifecycle()
    {
        if (RunStateManager.Instance != null &&
            RunStateManager.Instance.IsRunEnded)
        {
            return true;
        }

        if (!hadObservedPlayer)
            return false;

        return observedPlayerHealth == null || observedPlayerHealth.IsDead;
    }

    private void PlayOptional(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private void CloseOverlay()
    {
        state = ReelState.Hidden;
        panelCanvasGroup.alpha = 1f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
        panelRect.localScale = Vector3.one;
        transferPacket.gameObject.SetActive(false);
        canvasRoot.SetActive(false);
        winningCard = null;
        observedPlayerHealth = null;
        hadObservedPlayer = false;
    }

    private void CloseWithoutReward()
    {
        claimedCallback = null;
        CloseOverlay();
    }

    private RewardCard CreateCard(Transform parent, int index)
    {
        RectTransform rect = CreateRect($"Reward Card {index}", parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(80f, 52f);
        Image background = rect.gameObject.AddComponent<Image>();
        background.raycastTarget = false;

        RectTransform iconRect = CreateRect("Icon", rect);
        SetRect(iconRect, new Vector2(0f, 10f), new Vector2(18f, 18f));
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TextMeshProUGUI label = CreateText(
            "Label", rect, string.Empty, 13f,
            TextAlignmentOptions.Center, Color.white
        );
        SetRect(label.rectTransform, new Vector2(0f, -10f),
            new Vector2(76f, 30f));
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

    private void OnDestroy()
    {
        if (stopButton != null) stopButton.onClick.RemoveListener(BeginBraking);
        claimedCallback = null;
        openingReserved = false;

        if (instance == this)
            instance = null;
    }
}
