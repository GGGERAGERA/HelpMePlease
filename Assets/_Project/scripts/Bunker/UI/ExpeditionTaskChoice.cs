using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ExpeditionTaskChoice : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text description;
    [SerializeField] private TMP_Text reward;
    [SerializeField] private Graphic frame;
    [SerializeField] private GameObject selectedMark;
    public Button Button => button;
    public void Present(ExpeditionTaskDefinition task, bool selected)
    {
        title.text = SurfaceMapBriefing.Text(task.Title);
        description.text = SurfaceMapBriefing.Format(task.Description, task.Target);
        reward.text = "+" + task.RewardCores;
        frame.color = selected ? new Color(.35f,.74f,.68f) : new Color(.3f,.37f,.35f);
        selectedMark.SetActive(selected);
    }
}
