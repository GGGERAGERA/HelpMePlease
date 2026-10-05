using UnityEngine;

[CreateAssetMenu(menuName = "Subject42/Onboarding Step")]
public sealed class OnboardingStepDefinition : ScriptableObject
{
    [SerializeField] private string id;
    public string Id => id;
}
