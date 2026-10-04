using UnityEngine;

public interface IMissionStorage
{
    string Load();
    void Save(string json);
    bool HasClaimReceipt(string missionId);
    bool TryClaimGold(string missionId,int amount,string committedState);
}

/// <summary>Separate versioned meta key; existing surface map/content JSON remains unchanged.</summary>
public sealed class PlayerPrefsMissionStorage : IMissionStorage
{
    public const string KeyPrefix="META_MISSIONS_V1_";
    private readonly string mapId;
    public string Key => KeyPrefix+mapId;
    public PlayerPrefsMissionStorage(string mapId) { this.mapId=mapId; }
    public string ReceiptId(string missionId) => "mission:"+mapId+":"+missionId;
    public string Load() => PlayerPrefs.GetString(Key,string.Empty);
    public void Save(string json) { PlayerPrefs.SetString(Key,json); PlayerPrefs.Save(); }
    public bool HasClaimReceipt(string missionId) => CurrencyManager.HasGoldRewardReceipt(ReceiptId(missionId));
    public bool TryClaimGold(string missionId,int amount,string committedState) => CurrencyManager.Instance!=null &&
        CurrencyManager.Instance.TryGrantGoldExactOnce(ReceiptId(missionId),amount,() => PlayerPrefs.SetString(Key,committedState));
}
