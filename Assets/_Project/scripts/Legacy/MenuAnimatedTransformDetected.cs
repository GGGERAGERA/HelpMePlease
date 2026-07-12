using UnityEngine;

public class MenuAnimatedTransformDetector : MonoBehaviour
{
    [SerializeField] private Transform rootToScan;
    [SerializeField] private bool logEverySecond = true;

    private Transform[] transforms;
    private Vector3[] lastLocalPositions;
    private float timer;

    private void Start()
    {
        if (rootToScan == null)
            rootToScan = transform;

        transforms = rootToScan.GetComponentsInChildren<Transform>(true);
        lastLocalPositions = new Vector3[transforms.Length];

        for (int i = 0; i < transforms.Length; i++)
            lastLocalPositions[i] = transforms[i].localPosition;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (!logEverySecond || timer < 1f)
            return;

        timer = 0f;

        for (int i = 0; i < transforms.Length; i++)
        {
            Vector3 current = transforms[i].localPosition;
            float delta = Vector3.Distance(current, lastLocalPositions[i]);

            if (delta > 0.01f)
            {
                Debug.Log(
                    $"[ANIM MOVE] {transforms[i].name} localPos changed. Delta: {delta:F3}. Current: {current}",
                    transforms[i]
                );
            }

            lastLocalPositions[i] = current;
        }
    }
}