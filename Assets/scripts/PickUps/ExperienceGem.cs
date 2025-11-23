using UnityEngine;

public class ExperienceGem : PickUp , ICollectible
{
    public int expierenceGranted;
    public void Collect()
    {
      PlayerStats player = FindAnyObjectByType<PlayerStats>();
        player.IncreaseExperience(expierenceGranted);
    }

}
