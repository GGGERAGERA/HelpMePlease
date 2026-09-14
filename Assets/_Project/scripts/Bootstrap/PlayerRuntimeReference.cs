using UnityEngine;

public interface IPlayerRuntimeResolver
{
    GameObject ResolvePlayer(string playerTag);
}

public sealed class TagPlayerRuntimeResolver : IPlayerRuntimeResolver
{
    public GameObject ResolvePlayer(string playerTag)
    {
        return string.IsNullOrWhiteSpace(playerTag)
            ? GameObject.FindGameObjectWithTag("Player")
            : GameObject.FindGameObjectWithTag(playerTag);
    }
}

public static class PlayerRuntimeReference
{
    private const float ResolveCooldownSeconds = 1f;
    private const string DefaultPlayerTag = "Player";
    private static readonly IPlayerRuntimeResolver DefaultResolver =
        new TagPlayerRuntimeResolver();

    private static IPlayerRuntimeResolver resolver = DefaultResolver;
    private static GameObject cachedPlayer;
    private static PlayerHealth cachedPlayerHealth;
    private static float nextResolveTime;

    public static GameObject CachedPlayer => cachedPlayer;
    public static Transform PlayerTransform => ResolvePlayerTransform(forceLookup: false);
    public static PlayerHealth PlayerHealth => ResolvePlayerHealth(forceLookup: false);
    public static IPlayerRuntimeResolver Resolver => resolver;

    public static Transform ResolvePlayerTransform(
        string playerTag = DefaultPlayerTag,
        bool forceLookup = false)
    {
        if (cachedPlayer == null)
        {
            if (!forceLookup || Time.time < nextResolveTime)
                return null;

            nextResolveTime = Time.time + ResolveCooldownSeconds;
            Bind(ResolveWithResolver(playerTag));
        }

        return cachedPlayer != null ? cachedPlayer.transform : null;
    }

    public static void ConfigureResolver(IPlayerRuntimeResolver playerResolver)
    {
        resolver = playerResolver ?? DefaultResolver;
        Clear();
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

    private static GameObject ResolveWithResolver(string playerTag)
    {
        IPlayerRuntimeResolver activeResolver = resolver ?? DefaultResolver;
        string normalizedTag = string.IsNullOrWhiteSpace(playerTag)
            ? DefaultPlayerTag
            : playerTag;

        if (activeResolver == null)
            return null;

        return activeResolver.ResolvePlayer(normalizedTag);
    }
}
