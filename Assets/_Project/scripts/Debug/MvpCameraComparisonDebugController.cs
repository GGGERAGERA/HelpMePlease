using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class MvpCameraComparisonDebugController : MonoBehaviour
{
    private const string MvpSceneName = "MVP";

    private Camera activeCamera;
    private CameraShake cameraShake;
    private bool cameraShakeEnabled = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<MvpCameraComparisonDebugController>() != null)
            return;

        GameObject controller = new("MVP Camera Comparison Debug Controller");
        DontDestroyOnLoad(controller);
        controller.AddComponent<MvpCameraComparisonDebugController>();
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name != MvpSceneName)
            return;

        ResolveActiveCamera();

        if (Input.GetKeyDown(KeyCode.F2))
            SetCameraSize(7f);
        else if (Input.GetKeyDown(KeyCode.F3))
            SetCameraSize(9.5f);
        else if (Input.GetKeyDown(KeyCode.F4))
            SetCameraSize(11f);
        else if (Input.GetKeyDown(KeyCode.F12))
            SetCameraSize(12.5f);

        if (Input.GetKeyDown(KeyCode.F11))
            ToggleCameraShake();
    }

    private void ResolveActiveCamera()
    {
        Camera resolved = Camera.main;
        if (resolved == activeCamera)
            return;

        activeCamera = resolved;
        cameraShake = activeCamera != null
            ? activeCamera.GetComponent<CameraShake>()
            : null;
        cameraShakeEnabled = cameraShake == null || !cameraShake.DebugSuppressed;
    }

    private void SetCameraSize(float size)
    {
        if (activeCamera == null || !activeCamera.orthographic)
            return;

        activeCamera.orthographicSize = size;
        Debug.Log(
            "DEBUG CAMERA SIZE: " +
            size.ToString("0.0", CultureInfo.InvariantCulture)
        );
    }

    private void ToggleCameraShake()
    {
        if (cameraShake == null)
            return;

        cameraShakeEnabled = !cameraShakeEnabled;
        cameraShake.SetDebugSuppressed(!cameraShakeEnabled);
        Debug.Log("DEBUG CAMERA SHAKE: " +
            (cameraShakeEnabled ? "ON" : "OFF"));
    }
}
#endif
