using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class FootballMazeButton : MonoBehaviour
{
    public FootballMazeLab Lab;
    public int Section;
    public FootballMazeDoor[] OpenDoors;
    public FootballMazeDoor[] CloseDoors;
    public bool Toggle;
    public SpriteRenderer Face;
    public Color CircuitColor = Color.cyan;
    public AudioSource Feedback;
    public Transform ResultFocus;
    public bool Activated { get; private set; }
    private int lastShot = -1;
    private float flashUntil;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.isTrigger || other.attachedRigidbody != Lab.BallBody ||
            Lab.Completed || Lab.Checkpoint != Section ||
            !Lab.Ball.IsFreeShot || Lab.Ball.IsDribbling || Lab.Ball.IsAiming ||
            Lab.BallBody.linearVelocity.sqrMagnitude < 1.44f ||
            Lab.Ball.ShotSequence == lastShot || (!Toggle && Activated)) return;

        lastShot = Lab.Ball.ShotSequence;
        Activated = Toggle ? !Activated : true;
        foreach (var door in OpenDoors) door.SetOpen(Activated);
        foreach (var door in CloseDoors) door.SetOpen(!Activated);
        flashUntil = Time.unscaledTime + .3f;
        Feedback.Play();
        Lab.ShowSwitchResult(ResultFocus);
    }

    public void Restore()
    {
        Activated = false;
        lastShot = -1;
        flashUntil = 0f;
        Refresh();
    }

    private void Update() => Refresh();
    private void Refresh()
    {
        Face.color = Time.unscaledTime < flashUntil ? Color.white :
            CircuitColor * (Activated ? 1f : .55f);
    }
}
