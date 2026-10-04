using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerMissionPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text speaker,body;
    [SerializeField] private Button actionButton,leaveButton;
    private MissionProvider provider;
    private MissionService service;
    private MissionDefinition mission;
    public bool IsOpen => gameObject.activeInHierarchy;
    private void Awake() { actionButton.onClick.AddListener(Act); leaveButton.onClick.AddListener(Hide); }
    public void Show(MissionProvider source)
    {
        Unsubscribe(); provider=source; service=MetaProgressionManager.Instance?.Missions;
        mission=provider!=null?provider.GetMission():null;
        if(service==null||mission==null)return;
        gameObject.SetActive(true); service.Changed+=Refresh; Refresh();
    }
    private void Refresh()
    {
        if(service==null||mission==null||provider==null) { Hide(); return; }
        speaker.text=provider.ProviderName;
        var state=service.GetState(mission.MissionId);
        string reward=mission.Reward.Gold+" GOLD";
        var actionLabel=actionButton.GetComponentInChildren<TMP_Text>();
        actionButton.gameObject.SetActive(state==MissionState.Available||state==MissionState.ReadyToTurnIn);
        actionButton.interactable=state==MissionState.Available||state==MissionState.ReadyToTurnIn;
        if(state==MissionState.Available)
        { body.text=mission.OfferText+"\n\n<size=16>REWARD  "+reward+"</size>"; actionLabel.text="ACCEPT"; }
        else if(state==MissionState.ReadyToTurnIn)
        { body.text=mission.Title+" COMPLETE\n\n<color=#6BD9B5>OBJECTIVE VERIFIED</color>\n\nREWARD  "+reward; actionLabel.text="CLAIM REWARD"; }
        else if(state==MissionState.Completed)
        { body.text=mission.Title+" COMPLETE\n\nReward claimed.\nAssignment closed."; }
        else
        { body.text=mission.Title+"\nSector "+mission.TargetSectorId+"\n\n"+mission.Description+(state==MissionState.ObjectiveCompleted?"\n\nExtract successfully to secure the evidence.":""); }
    }
    private void Act()
    {
        if(service==null||mission==null||provider==null||!provider.CanInteract)return;
        var state=service.GetState(mission.MissionId);
        if(state==MissionState.Available)service.Accept(mission.MissionId);
        else if(state==MissionState.ReadyToTurnIn)service.Claim(mission.MissionId);
    }
    public void Hide() { gameObject.SetActive(false); }
    private void OnDisable() => Unsubscribe();
    private void Unsubscribe() { if(service!=null)service.Changed-=Refresh; }
}
