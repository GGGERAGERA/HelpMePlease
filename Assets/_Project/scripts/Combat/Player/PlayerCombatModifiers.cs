using UnityEngine;

public class PlayerCombatModifiers : MonoBehaviour
{
    [Header("Production Offensive Upgrades")]
    [SerializeField] private float runDamageMultiplier = 1f;
    [SerializeField] private float runCritChanceBonus;
    [SerializeField] private float runAttackSizeMultiplier = 1f;

    public float RunDamageMultiplier => Mathf.Max(0.01f, runDamageMultiplier);
    public float RunCritChanceBonus => Mathf.Clamp01(runCritChanceBonus);
    public float RunAttackSizeMultiplier =>
        Mathf.Max(0.1f, runAttackSizeMultiplier);
    public float TotalDamageMultiplier => RunDamageMultiplier;

    public void SetRunDamageMultiplier(float value)
    {
        runDamageMultiplier = Mathf.Max(0.01f, value);
    }

    public void SetRunCritChanceBonus(float value)
    {
        runCritChanceBonus = Mathf.Clamp01(value);
    }

    public void SetRunAttackSizeMultiplier(float value)
    {
        runAttackSizeMultiplier = Mathf.Max(0.1f, value);
    }

}
