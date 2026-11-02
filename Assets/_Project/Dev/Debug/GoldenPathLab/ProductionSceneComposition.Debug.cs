#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed partial class ProductionSceneComposition
{
    partial void BindDevelopmentSession()
    {
        // Rebind only an explicitly started lab session; ordinary gameplay creates none.
        if (role == SceneRole.Gameplay || role == SceneRole.Bunker)
            BotRunSession.Current?.BindScene(characters, RunFlowController.Instance, rewards, gameplayArea);
    }
}
#endif
