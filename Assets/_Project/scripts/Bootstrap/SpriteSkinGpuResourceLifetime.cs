using System.Reflection;
using UnityEngine;
using UnityEngine.U2D.Animation;

// Compatibility cleanup for the pinned 2D Animation 13.0.5 package.
// Its AfterSceneLoad fallback allocation also runs without any SpriteSkin/GPU
// manager, but only that manager normally invokes ClearFallbackBuffer.
internal static class SpriteSkinGpuResourceLifetime
{
    private static readonly MethodInfo ClearFallback = typeof(SpriteSkin).Assembly
        .GetType("UnityEngine.U2D.Animation.GpuDeformationSystem")?
        .GetMethod("ClearFallbackBuffer", BindingFlags.NonPublic | BindingFlags.Static);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        // Also handles repeated Play Mode sessions with domain reload disabled.
        Application.quitting -= Release;
        Application.quitting += Release;
    }

    private static void Release()
    {
        Application.quitting -= Release;
        if (ClearFallback == null)
        {
            Debug.LogWarning("2D Animation fallback cleanup changed; verify the package lifecycle integration.");
            return;
        }
        // Reuse the owner's idempotent release/null operation. Never touch live
        // frame resources, deformation settings, or other pipeline buffers.
        ClearFallback.Invoke(null, null);
    }
}
