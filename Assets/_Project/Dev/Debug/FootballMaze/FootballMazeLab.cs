using UnityEngine;
using UnityEngine.UI;

/// <summary>Owns only the authored maze's puzzle progress and recovery, never football input/physics.</summary>
public sealed class FootballMazeLab : MonoBehaviour
{
    public const string ScenePath = "Assets/_Project/Dev/Labs/FootballMazeLab/FootballMazeLab.unity";
    [System.Serializable]
    public sealed class CheckpointData
    {
        public Transform PlayerSpawn;
        public Transform BallSpawn;
        [TextArea] public string Hint;
    }

    public Rigidbody2D Player;
    public BallRollVisual Ball;
    public FootballBallRuntime BallRuntime;
    public Rigidbody2D BallBody;
    public FootballMazeButton[] Buttons;
    public FootballMazeDoor[] Doors;
    public CheckpointData[] Checkpoints;
    public GravityZone Anomaly;
    public LocalAnomalyData AnomalyData;
    public CameraFollow CameraFollow;
    public Text Hint;
    public Text Status;
    public GameObject CompletionPanel;
    public AudioSource CheckpointSound;
    public int Checkpoint { get; private set; }
    public bool Completed { get; private set; }
    private float focusUntil;
    public bool AllSolved
    {
        get { foreach (var button in Buttons) if (!button.Activated) return false; return true; }
    }

    private void Start()
    {
        PlayerRuntimeReference.Bind(Player.gameObject);
        Anomaly.Initialize(AnomalyData, null, new Vector2(7f, 7f));
        Anomaly.ConfigureForce(12f);
        Anomaly.ConfigureRadialPolarity(-1f, new Color(.15f, .85f, 1f));
        Anomaly.ConfigureAffectedColliderFilter(c => !c.isTrigger && c.attachedRigidbody == BallBody);
        RestartMaze();
    }

    private void OnDestroy()
    {
        if (Player != null && PlayerRuntimeReference.CachedPlayer == Player.gameObject) PlayerRuntimeReference.Clear();
        BallRollVisual.CancelActiveSlowMotion();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) ReturnBall();
        if (Input.GetKeyDown(KeyCode.Backspace)) RestartMaze();
        if (focusUntil > 0f && Time.unscaledTime >= focusUntil)
        {
            CameraFollow.EndTemporaryFocus(this, .35f);
            focusUntil = 0f;
        }
        if (!Completed && (BallBody.position.x < 0f || BallBody.position.x > 91f ||
            BallBody.position.y < 0f || BallBody.position.y > 49f)) ReturnBall();
    }

    public bool SectionSolved(int section)
    {
        foreach (var button in Buttons)
            if (button.Section == section && !button.Activated) return false;
        return true;
    }

    public void ReachCheckpoint(int index)
    {
        Checkpoint = index;
        Hint.text = Checkpoints[index].Hint;
        Status.text = $"КОНТРОЛЬНАЯ ТОЧКА {index + 1} / 6";
        CheckpointSound.Play();
    }

    public void ReturnBall()
    {
        if (Completed) return;
        BallRollVisual.CancelActiveSlowMotion();
        // Recover the pair before resetting shutters; no old-side player can be stranded.
        Player.gameObject.SetActive(false);
        Vector3 playerSpawn = Checkpoints[Checkpoint].PlayerSpawn.position;
        Player.transform.position = playerSpawn;
        Player.position = playerSpawn;
        Player.linearVelocity = Vector2.zero;
        Player.gameObject.SetActive(true);
        BallRuntime.RespawnNow(Checkpoints[Checkpoint].BallSpawn);
        Physics2D.SyncTransforms();
        foreach (var button in Buttons) if (button.Section >= Checkpoint) button.Restore();
        foreach (var door in Doors) if (door.Section >= Checkpoint) door.Restore();
        CameraFollow.EndTemporaryFocus(this, .1f);
        focusUntil = 0f;
        CameraFollow.transform.position = Player.transform.position + CameraFollow.offset;
        Status.text = $"УЧАСТОК {Checkpoint + 1} / 6 · R — ВОЗВРАТ · BACKSPACE — ЗАНОВО";
    }

    public void RestartMaze()
    {
        Completed = false;
        Checkpoint = 0;
        CompletionPanel.SetActive(false);
        Hint.text = Checkpoints[0].Hint;
        ReturnBall();
    }

    public void ShowSwitchResult(Transform focus)
    {
        CameraFollow.BeginTemporaryFocus(this, focus, 1f, .6f, .25f);
        focusUntil = Time.unscaledTime + 1.35f;
    }

    public void Complete()
    {
        Completed = true;
        BallRollVisual.CancelActiveSlowMotion();
        BallBody.linearVelocity = Vector2.zero;
        CompletionPanel.SetActive(true);
        CheckpointSound.Play();
        Hint.text = "Мяч доставлен. Лабиринт пройден!";
    }
}
