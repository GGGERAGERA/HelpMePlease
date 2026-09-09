using System.IO;
using UnityEditor;

// Opt-in local editor command; no scene or asset work runs without a request.
[InitializeOnLoad]
public static class BunkerNavigationProbe
{
    static BunkerNavigationProbe() { EditorApplication.update += Command; }
    private static void Command()
    {
        const string request = "Artifacts/BunkerNetwork/author.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try
        {
            BunkerNavigationAuthoring.Build();
            File.WriteAllText(request + ".result", "Authored and saved.");
        }
        catch (System.Exception e) { File.WriteAllText(request + ".result", e.ToString()); }
    }
}
