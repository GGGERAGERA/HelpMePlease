using System.Text;
using TMPro;
using UnityEngine;

public sealed class MatrixRainBackground : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI columnPrefab;
    [SerializeField] private RectTransform container;

    [SerializeField] private int columns = 40;
    [SerializeField] private float minSpeed = 40f;
    [SerializeField] private float maxSpeed = 160f;
    [SerializeField] private int minLength = 12;
    [SerializeField] private int maxLength = 28;
    [SerializeField] private Color normalColor = new(0.1f, 0.8f, 1f, 0.45f);
    [SerializeField] private Color headColor = new(0.8f, 1f, 1f, 0.9f);
    [SerializeField] private float headChance = 0.25f;
    [SerializeField] private float glitchChancePerSecond = 0.35f;
    [SerializeField] private float glitchDuration = 0.08f;
    [SerializeField] private Color glitchColor = Color.red;
    [SerializeField, Range(0f, 1f)] private float wordChance = 0.15f;

    private TextMeshProUGUI[] texts;
    private RectTransform[] rects;
    private float[] speeds;
    private float[] glitchTimers;
    private string[] cachedTexts;

    private static readonly string[] Digits =
    {
    "0", "1", "00", "01", "10", "11", "000", "111", "010", "101"
};

    private static readonly string[] Words =
    {
    "AI", "SYS", "RUN", "NODE", "SCAN", "DATA", "ERR", "CORE",
    "0xFF", "NET", "GPU", "BOT", "GEN", "SYNC"
};

    private void Start()
    {
        Canvas.ForceUpdateCanvases();

        texts = new TextMeshProUGUI[columns];
        rects = new RectTransform[columns];
        speeds = new float[columns];
        glitchTimers = new float[columns];
        cachedTexts = new string[columns];

        float width = container.rect.width;

        if (width <= 0f)
        {
            width = ((RectTransform)transform).rect.width;
        }

        for (int i = 0; i < columns; i++)
        {
            TextMeshProUGUI text = Instantiate(columnPrefab, container);
            RectTransform rect = text.rectTransform;

            float x = Mathf.Lerp(-width * 0.5f, width * 0.5f, i / (float)(columns - 1));
            float y = Random.Range(0f, container.rect.height);

            rect.anchoredPosition = new Vector2(x, y);

            text.color = normalColor;
            cachedTexts[i] = GenerateColumn();
            text.text = cachedTexts[i];
            text.gameObject.SetActive(true);

            texts[i] = text;
            rects[i] = rect;
            speeds[i] = Random.Range(minSpeed, maxSpeed);
        }
    }

    private void Update()
    {
        float height = container.rect.height;

        for (int i = 0; i < columns; i++)
        {
            RectTransform rect = rects[i];
            rect.anchoredPosition += Vector2.down * speeds[i] * Time.unscaledDeltaTime;

            if (rect.anchoredPosition.y < -height)
            {
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, height);
                cachedTexts[i] = GenerateColumn();
                texts[i].text = cachedTexts[i];
                speeds[i] = Random.Range(minSpeed, maxSpeed);
            }
            UpdateGlitch(i);
        }
    }

    private string GenerateColumn()
    {
        int length = Random.Range(minLength, maxLength + 1);
        bool hasHead = Random.value <= headChance;

        StringBuilder sb = new();

        for (int i = 0; i < length; i++)
        {
            string token = GetRandomToken();

            if (i == 0 && hasHead)
            {
                sb.Append("<size=120%><color=#");
                sb.Append(ToHex(headColor));
                sb.Append(">");
                sb.Append(token);
                sb.Append("</color></size>");
            }
            else
            {
                sb.Append(token);
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }
    private void UpdateGlitch(int index)
    {
        if (glitchTimers[index] > 0f)
        {
            glitchTimers[index] -= Time.unscaledDeltaTime;

            if (glitchTimers[index] <= 0f)
            {
                texts[index].text = cachedTexts[index];
            }

            return;
        }

        if (Random.value <= glitchChancePerSecond * Time.unscaledDeltaTime)
        {
            glitchTimers[index] = glitchDuration;
            texts[index].text = GenerateGlitchColumn();
        }
    }

    private string GenerateGlitchColumn()
    {
        string[] glitchTokens = { "###", "???", "ERR", "NULL", "0x00", "////", "SYNC" };

        int length = Random.Range(minLength, maxLength + 1);
        StringBuilder sb = new();

        for (int i = 0; i < length; i++)
        {
            sb.Append("<color=#");
            sb.Append(ToHex(glitchColor));
            sb.Append(">");
            sb.Append(glitchTokens[Random.Range(0, glitchTokens.Length)]);
            sb.Append("</color>");
            sb.Append('\n');
        }

        return sb.ToString();
    }
    private static string ToHex(Color color)
    {
        return ColorUtility.ToHtmlStringRGBA(color);
    }
    private string GetRandomToken()
    {
        bool useWord = Random.value <= wordChance;

        if (useWord)
        {
            return Words[Random.Range(0, Words.Length)];
        }

        return Digits[Random.Range(0, Digits.Length)];
    }
}