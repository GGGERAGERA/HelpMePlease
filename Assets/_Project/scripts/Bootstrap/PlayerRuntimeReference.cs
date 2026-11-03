using UnityEngine;

public static class PlayerRuntimeReference
{
    private const float ResolveCooldownSeconds = 1f;
    private const string DefaultPlayerTag = "Player";
    private static GameObject cachedPlayer;
    private static PlayerHealth cachedPlayerHealth;
    private static float nextResolveTime;

    public static GameObject CachedPlayer => cachedPlayer;
    public static Transform PlayerTransform => ResolvePlayerTransform(forceLookup: false);
    public static PlayerHealth PlayerHealth => ResolvePlayerHealth(forceLookup: false);

    public static Transform ResolvePlayerTransform(
        string playerTag = DefaultPlayerTag,
        bool forceLookup = false)
    {
        if (cachedPlayer == null)
        {
            if (!forceLookup || Time.time < nextResolveTime)
                return null;

            nextResolveTime = Time.time + ResolveCooldownSeconds;
            Bind(GameObject.FindGameObjectWithTag(string.IsNullOrWhiteSpace(playerTag)
                ? DefaultPlayerTag : playerTag));
        }

        return cachedPlayer != null ? cachedPlayer.transform : null;
    }

    public static PlayerHealth ResolvePlayerHealth(string playerTag = DefaultPlayerTag, bool forceLookup = false)
    {
        ResolvePlayerTransform(playerTag, forceLookup);
        return cachedPlayerHealth;
    }

    public static void Bind(GameObject player)
    {
        cachedPlayer = player;
        cachedPlayerHealth =
            player != null ? player.GetComponent<PlayerHealth>() : null;

        if (cachedPlayer == null)
            nextResolveTime = Time.time;
    }

    public static void Clear()
    {
        Bind(null);
    }
}
