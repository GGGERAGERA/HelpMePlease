using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs real MainMenu physics without editing the scene or building geometry.
[InitializeOnLoad]
public static class FootballArenaSmoke
{
    private static readonly List<string> results = new();
    private static FootballMinigame game;
    private static Rigidbody2D ballBody, playerBody;
    private static CharacterMovement2D movement;
    private static FootballGateScoreZone gate;
    private static int phase, scoreBefore, targetPoints;
    private static float until;
    private static double deadline;
    private static bool running;
    private static Vector3 anomalyStart, targetStart;
    private static FootballScoreZone target;
    private static TMPro.TMP_Text popup;
    private static Vector3 popupStart;
    private static GravityZone anomaly;
    private static GravityZone[] fields;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static FootballArenaSmoke()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("FootballOriginalSmoke", false))
            {
                Application.runInBackground = true;
                phase = 0; results.Clear(); running = true;
                until = Time.time + 1f; deadline = EditorApplication.timeSinceStartup + 90;
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                running = false;
                Application.runInBackground = SessionState.GetBool("FootballRunInBackground", false);
            }
        };
        EditorApplication.update += Tick;
    }
    [MenuItem("Tools/Subject42/Football/Play Mode Smoke Test")]
    public static void Start()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty ||
            SceneManager.GetActiveScene().path != "Assets/_Project/Scenes/MainMenu.unity")
            throw new InvalidOperationException("Open saved MainMenu in Edit Mode.");
        SessionState.SetBool("FootballRunInBackground", Application.runInBackground);
        SessionState.SetBool("FootballOriginalSmoke", true);
        SessionState.SetBool("FootballHadRecord", PlayerPrefs.HasKey("BunkerFootballBestScore"));
        SessionState.SetInt("FootballSavedRecord", PlayerPrefs.GetInt("BunkerFootballBestScore", 0));
        EditorApplication.isPlaying = true;
    }
    private static void Check(string name, bool pass, string detail = "")
    {
        results.Add($"{(pass ? "PASS" : "FAIL")}: {name} {detail}");
        File.WriteAllLines("Assets/_Project/Documentation/FootballSmoke.txt", results);
    }
    private static void Shot(Vector2 p, Vector2 velocity)
    {
        ballBody.position = p;
        ballBody.transform.position = new(p.x, p.y, ballBody.transform.position.z);
        ballBody.linearVelocity = velocity;
        ballBody.WakeUp(); Physics2D.SyncTransforms();
        until = Time.time + .15f;
    }
    private static void Tick()
    {
        if (!running || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        if (EditorApplication.timeSinceStartup > deadline) { Check("Timeout", false, $"timeScale={Time.timeScale} time={Time.time} until={until}"); Finish(); return; }
        if (Time.time < until) return;
        try
        {
            if (game == null)
            {
                var follow = UnityEngine.Object.FindFirstObjectByType<CameraFollow>();
                if (follow == null || follow.target == null) return;
                game = UnityEngine.Object.FindFirstObjectByType<FootballMinigame>();
                playerBody = follow.target.GetComponent<Rigidbody2D>();
                movement = follow.target.GetComponent<CharacterMovement2D>();
                playerBody.position = game.PlayerStart.position;
                follow.target.position = game.PlayerStart.position;

                movement.enabled = false;
                ballBody = game.Ball.GetComponent<Rigidbody2D>();
                until = Time.time + .25f;
                return;
            }
            Bounds b = game.PlayBounds;
            switch (phase++)
            {
                case 0:
                    Check("Original composition 24x28", Mathf.Abs(b.size.x-24)<.01f && Mathf.Abs(b.size.y-28)<.01f);
                    Check("Original round content", game.IsRunning && game.ActiveBallCount==4 && game.ActiveAnomalyCount==2 && game.ActiveTargetCount==3 && game.GateCount==2);
                    Check("60 second round",game.RemainingTime>58 && game.RemainingTime<=60);
                    anomaly=game.transform.parent.GetComponentInChildren<GravityZone>();
                    target=game.transform.parent.GetComponentsInChildren<FootballScoreZone>().First(t=>t.IsAcceptingBalls);
                    anomalyStart=anomaly.transform.position;targetStart=target.transform.position;until=Time.time+.4f;break;
                case 1:
                    Check("Anomaly movement restored",Vector3.Distance(anomalyStart,anomaly.transform.position)>.1f);
                    Check("Target movement restored",Vector3.Distance(targetStart,target.transform.position)>.1f);
                    ScreenCapture.CaptureScreenshot("Assets/_Project/Documentation/FootballArena_HUD.png");
                    until=Time.time+.2f;break;
                case 2:
                    // Remove only dynamic interference for repeatable perimeter checks.
                    foreach(var a in game.transform.parent.GetComponentsInChildren<GravityZone>())a.gameObject.SetActive(false);
                    foreach(var t in game.transform.parent.GetComponentsInChildren<FootballScoreZone>())t.gameObject.SetActive(false);
                    foreach(var item in game.Balls)if(item!=game.Ball)item.gameObject.SetActive(false);
                    Shot(new(b.min.x+1.5f,b.min.y+1.5f),Vector2.left*22);break;
                case 3:
                    Check("Left wall",ballBody.linearVelocity.x>15);
                    Shot(new(b.max.x-1.5f,b.min.y+1.5f),Vector2.right*22);break;
                case 4:
                    Check("Right wall",ballBody.linearVelocity.x< -15);
                    Shot(new(b.center.x,b.max.y-1.5f),Vector2.up*22);break;
                case 5:
                    Check("Top wall",ballBody.linearVelocity.y< -15);
                    Shot(new(b.min.x+1,b.max.y-1),new Vector2(-1,1).normalized*22);break;
                case 6:
                    Check("Top-left corner",ballBody.linearVelocity.x>0 && ballBody.linearVelocity.y<0);
                    Shot(new(b.max.x-1,b.max.y-1),Vector2.one.normalized*22);break;
                case 7:
                    Check("Top-right corner",ballBody.linearVelocity.x<0 && ballBody.linearVelocity.y<0);
                    Shot(new(b.min.x+1,b.min.y+1),-Vector2.one.normalized*22);break;
                case 8:
                    Check("Bottom-left corner",ballBody.linearVelocity.x>0 && ballBody.linearVelocity.y>0);
                    Shot(new(b.max.x-1,b.min.y+1),new Vector2(1,-1).normalized*22);break;
                case 9:
                    Check("Bottom-right corner",ballBody.linearVelocity.x<0 && ballBody.linearVelocity.y>0);
                    Shot(new(b.min.x+.6f,b.min.y+1.5f),Vector2.left*200);break;
                case 10:
                    Check("CCD / max speed",ballBody.position.x>b.min.x && ballBody.linearVelocity.x>0 && ballBody.linearVelocity.magnitude<=22.01f);
                    var boundary=game.transform.parent.GetComponentInChildren<FootballPlayerBoundary>();
                    Check("Local player barrier exceptions",Physics2D.GetIgnoreCollision(boundary.Collider,game.Ball.GetComponent<CircleCollider2D>()));
                    playerBody.position=new(b.center.x,b.min.y+4);playerBody.linearVelocity=Vector2.up*10;
                    until=Time.time+.3f;break;
                case 11:
                    var barrier=game.transform.parent.GetComponentInChildren<FootballPlayerBoundary>().Collider;
                    Check("Original player shooting zone retained",playerBody.position.y<barrier.bounds.min.y);
                    playerBody.position=new(b.center.x,b.min.y+1);playerBody.linearVelocity=Vector2.down*10;
                    until=Time.time+.3f;break;
                case 12:
                    Check("Player can exit entrance",playerBody.position.y<b.min.y);
                    Check("Exit resets round",!game.IsRunning && game.Score==0 && game.ActiveBallCount==0 && game.ActiveTargetCount==0 && game.ActiveAnomalyCount==0);
                    playerBody.linearVelocity=Vector2.zero;playerBody.position=game.PlayerStart.position;
                    game.StartGame();
                    Check("Reentry can start round",game.IsRunning && game.ActiveBallCount==4);
                    Shot(new(b.max.x+5,b.max.y+5),Vector2.zero);break;
                case 13:
                    Check("Outside recovery",game.BallSpawnPoints.Any(t=>Vector2.Distance(t.position,ballBody.position)<.01f));
                    Shot(new(b.min.x+2,b.min.y+4),Vector2.zero);until=Time.time+5.3f;break;
                case 14:
                    Check("Stuck recovery",game.BallSpawnPoints.Any(t=>Vector2.Distance(t.position,ballBody.position)<.01f));
                    gate=game.transform.parent.GetComponentsInChildren<FootballGateScoreZone>().First();
                    scoreBefore=game.Score;
                    Shot(gate.GetComponent<BoxCollider2D>().bounds.center,Vector2.zero);break;
                case 15:
                    Check("Gate restores +20, not victory",game.Score==scoreBefore+20 && game.IsRunning,$"score={game.Score}");
                    until=Time.time+.15f;break;
                case 16:
                    Check("Gate contact is not double-counted",game.Score==scoreBefore+20);
                    Check("Goal session stats",game.GoalCount==1 && game.GoalScore==20);
                    game.ResetGame();game.StartGame();
                    Check("Restart clears goal stats",game.GoalCount==0 && game.GoalScore==0);
                    Check("Restart recreates original content",game.ActiveBallCount==4 && game.ActiveTargetCount==3 && game.ActiveAnomalyCount==2);
                    target=game.transform.parent.GetComponentsInChildren<FootballScoreZone>().First(t=>t.IsAcceptingBalls);
                    // Isolate this score assertion from neighbouring large targets.
                    foreach(var other in game.transform.parent.GetComponentsInChildren<FootballScoreZone>())
                        if(other!=target)other.gameObject.SetActive(false);
                    scoreBefore=game.Score;targetPoints=target.Points;
                    Shot(target.transform.position,Vector2.zero);break;
                case 17:
                    Check("Colored target scores",game.Score==scoreBefore+targetPoints,$"expected={targetPoints} score={game.Score}");
                    popup=((TMPro.TMP_Text[])typeof(FootballScoreZone).GetField("scorePopups",Private).GetValue(target)).First(t=>t.gameObject.activeSelf);
                    popupStart=popup.transform.position;
                    Check("Target popup shows awarded points",popup.text=="+"+targetPoints);
                    ScreenCapture.CaptureScreenshot("Assets/_Project/Documentation/FootballTargetPoints.png");
                    Shot(game.BallSpawnPoints[0].position,Vector2.zero);
                    until=Time.time+.6f;break;
                case 18:
                    Check("Target respawns",target.IsAcceptingBalls);
                    Check("Score popup rises and fades at hit location",popup.gameObject.activeSelf && popup.transform.position.y>popupStart.y+.2f && Mathf.Abs(popup.transform.position.x-popupStart.x)<.01f && popup.alpha<1f);
                    game.ResetBall();
                    Check("Reset all four to authored points",game.Balls.All(ball=>game.BallSpawnPoints.Any(t=>Vector2.Distance(t.position,ball.transform.position)<.05f)));
                    var cam=UnityEngine.Object.FindFirstObjectByType<CameraFollow>().ControlledCamera;
                    Check("Authored camera bounds",Mathf.Abs(cam.orthographicSize-15)<.01f);
                    typeof(FootballMinigame).GetField("remainingTime",Private).SetValue(game,.01f);until=Time.time+.2f;break;
                case 19:
                    Check("Round cleanup clears popup",!popup.gameObject.activeSelf);
                    Check("Timer ends round",!game.IsRunning && game.CanStart && game.ActiveBallCount==0);
                    game.StartGame();game.transform.parent.gameObject.SetActive(false);
                    Check("Disable cancels and restores",!game.IsRunning);
                    game.transform.parent.gameObject.SetActive(true);game.StartGame();
                    Check("Reenable restores 4 balls",game.IsRunning && game.ActiveBallCount==4);
                    gate=game.transform.parent.GetComponentsInChildren<FootballGateScoreZone>().First();
                    Shot((Vector2)gate.GetComponent<BoxCollider2D>().bounds.center+Vector2.down*.9f,Vector2.up*8);break;
                case 20:
                    Check("First current goal scores on a shot",game.GoalCount==1 && game.GoalScore==20);
                    Check("Goal feedback appears",gate.GetComponentInChildren<TMPro.TMP_Text>()!=null);
                    gate=game.transform.parent.GetComponentsInChildren<FootballGateScoreZone>().Last();
                    Shot((Vector2)gate.GetComponent<BoxCollider2D>().bounds.center+Vector2.down*.9f,Vector2.up*8);break;
                case 21:
                    Check("Both current goals contribute",game.GoalCount==2 && game.GoalScore==40 && game.Score==40);
                    var hud=game.transform.parent.GetComponentInChildren<FootballMinigameHUD>();
                    var stat=(TMPro.TMP_Text)typeof(FootballMinigameHUD).GetField("goalStatsText",Private).GetValue(hud);
                    Check("HUD displays goal subtotal",stat.text.Contains("2") && stat.text.Contains("40"));
                    ScreenCapture.CaptureScreenshot("Assets/_Project/Documentation/FootballGoals_HUD.png");
                    until=Time.time+1;break;
                case 22:
                    Check("Goal feedback expires",gate.GetComponentInChildren<TMPro.TMP_Text>()==null);
                    game.ResetGame();until=Time.time+.25f;break;
                case 23:
                    Check("No automatic restart while standing in start",!game.IsRunning);
                    var start=game.transform.parent.GetComponentInChildren<FootballStartZone>();
                    var sorting=start.GetComponent<UnityEngine.Rendering.SortingGroup>();
                    Check("Start marker sorts below player",sorting.sortingLayerName=="Default" && !sorting.sortAtRoot && sorting.sortingOrder == -5);
                    playerBody.position=game.PlayerStart.position+Vector3.down*5;Physics2D.SyncTransforms();until=Time.time+.2f;break;
                case 24:
                    playerBody.position=game.PlayerStart.position;Physics2D.SyncTransforms();until=Time.time+.2f;break;
                case 25:
                    Check("Trigger reentry starts fresh round",game.IsRunning && game.GoalCount==0 && game.GoalScore==0);
                    target=game.transform.parent.GetComponentsInChildren<FootballScoreZone>().First(t=>t.IsAcceptingBalls);
                    Shot(target.transform.position,Vector2.zero);break;
                case 26:
                    Shot(game.BallSpawnPoints[0].position,Vector2.zero);until=Time.time+1.4f;break;
                case 27:
                    Check("Target popup expires",((TMPro.TMP_Text[])typeof(FootballScoreZone).GetField("scorePopups",Private).GetValue(target)).All(t=>!t.gameObject.activeSelf));
                    fields=((List<GravityZone>)typeof(FootballMinigame).GetField("activeAnomalies",Private).GetValue(game)).ToArray();
                    foreach(var field in fields)field.GetComponent<FootballPingPongMover>().enabled=false;
                    foreach(var other in game.transform.parent.GetComponentsInChildren<FootballScoreZone>())other.gameObject.SetActive(false);
                    Check("Longer gravity fields",fields.All(f=>Mathf.Abs(f.FocusArea.bounds.size.x-7)<.01f));
                    Shot((Vector2)fields[0].transform.position+Vector2.right,Vector2.zero);break;
                case 28:
                    ScreenCapture.CaptureScreenshot("Assets/_Project/Documentation/FootballPolarity.png");
                    Check("First field attracts ball",ballBody.linearVelocity.x<-.1f);
                    Shot((Vector2)fields[1].transform.position+Vector2.right,Vector2.zero);break;
                case 29:
                    Check("Second field repels ball",ballBody.linearVelocity.x>.1f);
                    typeof(FootballMinigame).GetField("anomalySwapRemaining",Private).SetValue(game,1.6f);until=Time.time+.15f;break;
                case 30:
                    float polarity=(float)typeof(GravityZone).GetField("radialPolarity",Private).GetValue(fields[0]);
                    float opposite=(float)typeof(GravityZone).GetField("radialPolarity",Private).GetValue(fields[1]);
                    Check("Smooth complementary polarity transition",polarity>0 && polarity<1 && Mathf.Abs(polarity+opposite)<.001f);
                    until=Time.time+1.6f;break;
                case 31:
                    Check("Roles exchange",(float)typeof(GravityZone).GetField("radialPolarity",Private).GetValue(fields[0])<-.99f && (float)typeof(GravityZone).GetField("radialPolarity",Private).GetValue(fields[1])>.99f);
                    float next=(float)typeof(FootballMinigame).GetField("anomalySwapRemaining",Private).GetValue(game);
                    Check("Next exchange scheduled in 10 to 15 seconds",next>9.5f && next<=15f);
                    Shot((Vector2)fields[0].transform.position+Vector2.right,Vector2.zero);break;
                case 32:
                    Check("Former attractor now repels",ballBody.linearVelocity.x>0,$"velocity={ballBody.linearVelocity} position={ballBody.position} center={fields[0].transform.position}");
                    Shot((Vector2)fields[1].transform.position+Vector2.right,Vector2.zero);break;
                case 33:
                    Check("Former repeller now attracts",ballBody.linearVelocity.x<0,$"velocity={ballBody.linearVelocity}");
                    game.ResetGame();game.StartGame();
                    fields=((List<GravityZone>)typeof(FootballMinigame).GetField("activeAnomalies",Private).GetValue(game)).ToArray();
                    Check("Restart resets initial polarity",(float)typeof(GravityZone).GetField("radialPolarity",Private).GetValue(fields[0])>.99f);
                    Finish();break;
            }
        }
        catch(Exception e){Check("Exception",false,e.ToString());Finish();}
    }
    private static void Finish()
    {
        running=false;SessionState.SetBool("FootballOriginalSmoke",false);
        if(game!=null)game.ResetGame();
        if(movement!=null)movement.enabled=true;
        if(SessionState.GetBool("FootballHadRecord",false))PlayerPrefs.SetInt("BunkerFootballBestScore",SessionState.GetInt("FootballSavedRecord",0));
        else PlayerPrefs.DeleteKey("BunkerFootballBestScore");
        PlayerPrefs.Save();
        game=null;EditorApplication.isPlaying=false;
    }
}
