using UnityEngine;

public class ProjectilePoolManager : MonoBehaviour
{
    public static ProjectilePoolManager Instance; // чтобы другие могли найти
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject); // на всякий случай (если меняешь сцены)
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
