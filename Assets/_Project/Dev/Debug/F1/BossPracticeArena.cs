using UnityEngine;

// Local practice scene only. Uses the real movement, health and boss ability.
public sealed class BossPracticeArena : MonoBehaviour
{
    public GameObject playerTemplate;
    public GameObject bossPrefab;
    public Camera arenaCamera;
    public PlayerHealth Player { get; private set; }
    public BossRocketAttack Boss { get; private set; }

    void Start() => RestartRound();

    public void RestartRound()
    {
        if (Player != null) { Player.gameObject.SetActive(false); Destroy(Player.gameObject); }
        if (Boss != null) { Boss.gameObject.SetActive(false); Destroy(Boss.gameObject); }
        var player = Instantiate(playerTemplate, new Vector3(6, 0, 0), Quaternion.identity);
        player.name = "Player";
        player.SetActive(true);
        Player = player.GetComponent<PlayerHealth>();
        Boss = Instantiate(bossPrefab, new Vector3(-14, 0, 0), Quaternion.identity).GetComponent<BossRocketAttack>();
        Boss.name = "Boss (practice)";
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) RestartRound();
    }

    void LateUpdate()
    {
        if (Player != null)
            arenaCamera.transform.position = new Vector3(Player.transform.position.x, Player.transform.position.y + 2f, -10f);
    }

    void OnGUI()
    {
        GUI.Box(new Rect(12, 12, 500, 105), "BOSS PRACTICE — Dodge the rockets");
        GUI.Label(new Rect(24, 40, 480, 25), "WASD / arrows: move    Space: dash    R: restart round");
        if (Player != null)
            GUI.Label(new Rect(24, 65, 480, 25), $"HP: {Player.CurrentHealth:0} / {Player.MaxHealth:0}" +
                (Player.IsDead ? "    Defeated — press R" : ""));
        if (Boss != null)
            GUI.Label(new Rect(24, 89, 480, 25), $"Boss: {Boss.State}    Pending rockets: {Boss.PendingRocketCount}");
    }
}
