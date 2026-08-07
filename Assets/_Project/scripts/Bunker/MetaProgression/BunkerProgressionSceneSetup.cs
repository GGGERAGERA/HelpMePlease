using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BunkerProgressionSceneSetup : MonoBehaviour
{
    private const string BunkerSceneName = "MainMenu";

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != BunkerSceneName)
            return;

        CreateGate(
            "ShopRoom_ProgressionGate",
            new Vector3(46.5f, -17f, 0f),
            BunkerRoomGateMode.Locked,
            BunkerStationId.Weapon,
            2,
            new Vector2(1.5f, 3.8f),
            new Vector2(-4f, -2.2f),
            new Vector2(8.5f, 6.5f));

        CreateGate(
            "EastRoom_SealedGate",
            new Vector3(55.5f, -17f, 0f),
            BunkerRoomGateMode.Sealed,
            BunkerStationId.Anomaly,
            1,
            new Vector2(1.5f, 3.8f),
            new Vector2(4f, -2.2f),
            new Vector2(8.5f, 6.5f));
    }

    private static void CreateGate(
        string objectName,
        Vector3 position,
        BunkerRoomGateMode mode,
        BunkerStationId stationId,
        int requiredLevel,
        Vector2 blockerSize,
        Vector2 occluderOffset,
        Vector2 occluderSize)
    {
        GameObject gateObject = new(objectName);
        gateObject.transform.position = position;
        BunkerRoomGate gate = gateObject.AddComponent<BunkerRoomGate>();
        gate.Configure(mode, stationId, requiredLevel, blockerSize, occluderOffset, occluderSize);
    }
}
