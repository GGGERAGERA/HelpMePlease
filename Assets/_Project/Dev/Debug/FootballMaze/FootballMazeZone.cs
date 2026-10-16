using UnityEngine;

/// <summary>Checkpoints require both physical bodies, so the only ball cannot be left behind.</summary>
public sealed class FootballMazeZone : MonoBehaviour
{
    public FootballMazeLab Lab;
    public BoxCollider2D Area;
    public int Checkpoint;
    public bool Finish;

    private void FixedUpdate()
    {
        if (Lab.Completed) return;
        if (Finish)
        {
            if (Area.OverlapPoint(Lab.BallBody.position) && Lab.AllSolved)
                Lab.Complete();
        }
        else if (Checkpoint == Lab.Checkpoint + 1 && Lab.SectionSolved(Lab.Checkpoint) &&
            Area.OverlapPoint(Lab.Player.position) && Area.OverlapPoint(Lab.BallBody.position))
            Lab.ReachCheckpoint(Checkpoint);
    }
}
