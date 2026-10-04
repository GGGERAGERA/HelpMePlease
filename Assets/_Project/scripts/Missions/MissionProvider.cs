using System;
using UnityEngine;

/// <summary>Scene interaction adapter. Providers reference mission IDs, never event spawners or concrete sectors.</summary>
public sealed class MissionProvider : MonoBehaviour, IBunkerInteractable
{
    [SerializeField] private string providerName="OPERATOR";
    [SerializeField] private string[] missionIds=Array.Empty<string>();
    [SerializeField] private GameObject interactionArrow;
    private MissionService observedService;

    public bool HasActionableMission
    {
        get
        {
            var service=MetaProgressionManager.Instance?.Missions;
            if(service==null)return false;
            foreach(string id in missionIds)
            {
                if(service.Find(id)==null)continue;
                var state=service.GetState(id);
                if(state==MissionState.Available||state==MissionState.ReadyToTurnIn)return true;
            }
            return false;
        }
    }
    private void OnEnable() => BindService();
    private void LateUpdate()
    {
        // Composition may initialize or replace the meta service after this provider enables.
        if(observedService!=MetaProgressionManager.Instance?.Missions)BindService();
    }
    private void BindService()
    {
        if(observedService!=null)observedService.Changed-=RefreshArrow;
        observedService=MetaProgressionManager.Instance?.Missions;
        if(observedService!=null)observedService.Changed+=RefreshArrow;
        RefreshArrow();
    }
    private void RefreshArrow()
    { if(interactionArrow!=null)interactionArrow.SetActive(isActiveAndEnabled&&HasActionableMission); }
    private void OnDisable()
    {
        if(observedService!=null)observedService.Changed-=RefreshArrow;
        observedService=null;
        if(interactionArrow!=null)interactionArrow.SetActive(false);
    }
    public string ProviderName => providerName;
    public System.Collections.Generic.IReadOnlyList<string> MissionIds => missionIds;
    public string InteractionText => providerName;
    // Match ordinary bunker stations: cursor hover and interaction are not proximity-gated.
    public bool CanInteract => isActiveAndEnabled&&MetaProgressionManager.Instance?.Missions!=null;
    public MissionDefinition GetMission()
    {
        var service=MetaProgressionManager.Instance?.Missions;
        if(service==null)return null;
        MissionDefinition selected=null; int best=-1;
        foreach(string id in missionIds)
        {
            var definition=service.Find(id); if(definition==null)continue;
            var state=service.GetState(id);
            int priority=state==MissionState.ReadyToTurnIn?4:state==MissionState.Available?3:state==MissionState.Active||state==MissionState.ObjectiveCompleted?2:1;
            if(priority>best) { selected=definition; best=priority; }
        }
        return selected;
    }
    public void Interact()
    { if(CanInteract)BunkerContext.Instance?.Panels?.OpenMission(this); }
}
