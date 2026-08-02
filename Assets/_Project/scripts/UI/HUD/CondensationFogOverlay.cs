using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public sealed class CondensationFogOverlay : MonoBehaviour
{
    private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int FogTimeId = Shader.PropertyToID("_FogTime");
    private static readonly int RestoreAmountId =
        Shader.PropertyToID("_RestoreAmount");
    private static readonly int BrushCenterId =
        Shader.PropertyToID("_BrushCenter");
    private static readonly int BrushRadiusId =
        Shader.PropertyToID("_BrushRadius");
    private static readonly int ScreenAspectId =
        Shader.PropertyToID("_ScreenAspect");

    private const int RestorePass = 1;
    private const int BrushPass = 2;

    public static CondensationFogOverlay Instance { get; private set; }

    [Header("References")]
    [SerializeField] private RawImage fogImage;
    [SerializeField] private Shader fogShader;

    [Header("Mask")]
    [SerializeField] private Vector2Int maskResolution =
        new(256, 144);
    [SerializeField, Range(0.02f, 0.2f)]
    private float brushRadius = 0.075f;
    [SerializeField, Min(0.1f)] private float refogSpeed = 0.32f;
    [SerializeField, Min(0.1f)] private float minimumMouseMovement = 1f;

    [Header("Visual")]
    [SerializeField, Range(0f, 1f)] private float fogOpacity = 0.82f;
    [SerializeField, Min(0.01f)] private float fadeInDuration = 1.25f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.55f;

    private RenderTexture fogMask;
    private Material runtimeMaterial;
    private Vector2 previousMousePosition;
    private float currentFade;
    private float targetFade;
    private bool hasPreviousMousePosition;
    private bool isVisible;
    private bool isHiding;
    private bool acceptsBrushInput;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;

        if (fogImage == null)
            fogImage = GetComponent<RawImage>();

        fogImage.raycastTarget = false;
        fogImage.enabled = false;
    }

    private void Update()
    {
        if (!isVisible || runtimeMaterial == null)
            return;

        float fadeDuration = targetFade > currentFade
            ? fadeInDuration
            : fadeOutDuration;
        currentFade = Mathf.MoveTowards(
            currentFade,
            targetFade,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration)
        );
        runtimeMaterial.SetFloat(FadeId, currentFade * fogOpacity);
        runtimeMaterial.SetFloat(FogTimeId, Time.time);

        if (acceptsBrushInput && Time.timeScale > 0f)
        {
            RestoreFog();
            UpdateMouseBrush();
        }

        if (!isHiding || currentFade > 0f)
            return;

        ReleaseRuntimeResources();
    }

    public void Show()
    {
        if (isVisible && runtimeMaterial != null && fogMask != null)
        {
            targetFade = 1f;
            isHiding = false;
            acceptsBrushInput = true;
            hasPreviousMousePosition = false;
            return;
        }

        if (fogImage == null || fogShader == null)
        {
            Debug.LogWarning(
                "[CondensationFogOverlay] RawImage or fog shader is not assigned.",
                this
            );
            return;
        }

        runtimeMaterial = new Material(fogShader)
        {
            name = "Condensation Fog (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };

        RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(
            RenderTextureFormat.R8
        )
            ? RenderTextureFormat.R8
            : RenderTextureFormat.ARGB32;
        fogMask = new RenderTexture(
            Mathf.Max(16, maskResolution.x),
            Mathf.Max(9, maskResolution.y),
            0,
            format,
            RenderTextureReadWrite.Linear
        )
        {
            name = "Condensation Fog Mask",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        fogMask.Create();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = fogMask;
        GL.Clear(false, true, Color.white);
        RenderTexture.active = previous;

        runtimeMaterial.SetTexture(MaskTexId, fogMask);
        fogImage.texture = fogMask;
        fogImage.material = runtimeMaterial;
        fogImage.enabled = true;
        currentFade = 0f;
        targetFade = 1f;
        isVisible = true;
        isHiding = false;
        acceptsBrushInput = true;
        hasPreviousMousePosition = false;
    }

    public void Hide()
    {
        if (!isVisible)
            return;

        acceptsBrushInput = false;
        hasPreviousMousePosition = false;
        targetFade = 0f;
        isHiding = true;
    }

    public void HideImmediate()
    {
        ReleaseRuntimeResources();
    }

    private void RestoreFog()
    {
        if (fogMask == null || runtimeMaterial == null)
            return;

        float restoreAmount = 1f - Mathf.Exp(
            -Mathf.Max(0f, refogSpeed) * Time.deltaTime
        );
        runtimeMaterial.SetFloat(RestoreAmountId, restoreAmount);
        DrawFullscreenPass(RestorePass);
    }

    private void UpdateMouseBrush()
    {
        Vector2 currentMousePosition = Input.mousePosition;

        if (!hasPreviousMousePosition)
        {
            previousMousePosition = currentMousePosition;
            hasPreviousMousePosition = true;
            return;
        }

        float distance = Vector2.Distance(
            previousMousePosition,
            currentMousePosition
        );

        if (distance < minimumMouseMovement)
            return;

        float brushRadiusPixels = Mathf.Max(
            1f,
            brushRadius * Mathf.Max(1, Screen.height)
        );
        int stampCount = Mathf.Max(
            1,
            Mathf.CeilToInt(distance / (brushRadiusPixels * 0.35f))
        );

        for (int i = 1; i <= stampCount; i++)
        {
            Vector2 screenPosition = Vector2.Lerp(
                previousMousePosition,
                currentMousePosition,
                i / (float)stampCount
            );
            DrawBrush(screenPosition);
        }

        previousMousePosition = currentMousePosition;
    }

    private void DrawBrush(Vector2 screenPosition)
    {
        float width = Mathf.Max(1f, Screen.width);
        float height = Mathf.Max(1f, Screen.height);
        float normalizedY = Mathf.Clamp01(screenPosition.y / height);

        if (SystemInfo.graphicsUVStartsAtTop)
            normalizedY = 1f - normalizedY;

        runtimeMaterial.SetVector(
            BrushCenterId,
            new Vector4(
                Mathf.Clamp01(screenPosition.x / width),
                normalizedY,
                0f,
                0f
            )
        );
        runtimeMaterial.SetFloat(BrushRadiusId, brushRadius);
        runtimeMaterial.SetFloat(ScreenAspectId, width / height);
        DrawFullscreenPass(BrushPass);
    }

    private void DrawFullscreenPass(int passIndex)
    {
        if (fogMask == null || runtimeMaterial == null)
            return;

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = fogMask;
        GL.PushMatrix();
        GL.LoadOrtho();

        if (runtimeMaterial.SetPass(passIndex))
        {
            GL.Begin(GL.QUADS);
            GL.TexCoord2(0f, 0f);
            GL.Vertex3(0f, 0f, 0f);
            GL.TexCoord2(1f, 0f);
            GL.Vertex3(1f, 0f, 0f);
            GL.TexCoord2(1f, 1f);
            GL.Vertex3(1f, 1f, 0f);
            GL.TexCoord2(0f, 1f);
            GL.Vertex3(0f, 1f, 0f);
            GL.End();
        }

        GL.PopMatrix();
        RenderTexture.active = previous;
    }

    private void ReleaseRuntimeResources()
    {
        isVisible = false;
        isHiding = false;
        acceptsBrushInput = false;
        hasPreviousMousePosition = false;
        currentFade = 0f;
        targetFade = 0f;

        if (fogImage != null)
        {
            fogImage.enabled = false;
            fogImage.texture = null;
            fogImage.material = null;
        }

        if (fogMask != null)
        {
            if (RenderTexture.active == fogMask)
                RenderTexture.active = null;

            fogMask.Release();
            Destroy(fogMask);
            fogMask = null;
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
    }

    private void OnDisable()
    {
        HideImmediate();

        if (Instance == this)
            Instance = null;
    }
}
