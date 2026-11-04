using System;
using UnityEngine;

internal sealed class RouteRiskOffer : Interactable
{
    private Func<bool> canOffer, activate;
    public override KeyCode InteractionKey => KeyCode.F;
    public override bool CanInteract => base.CanInteract && canOffer?.Invoke() == true;

    internal void Initialize(string promptKey, Func<bool> available, Func<bool> accept)
    {
        SetPromptKey(promptKey);
        canOffer = available;
        activate = accept;
    }

    public override void Interact()
    {
        if (!CanInteract || activate?.Invoke() != true) return;
        gameObject.SetActive(false);
    }
}
