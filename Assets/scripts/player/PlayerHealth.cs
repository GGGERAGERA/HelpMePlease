using UnityEngine;
public class PlayerHealth : MonoBehaviour
{
    private PlayerContext _context;

    public void Initialize(PlayerContext context)
    {
        _context = context;
        // Можно подписаться на события, обновить UI и т.д.
    }
}