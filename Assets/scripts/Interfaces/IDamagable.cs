
//Это не скрипт, а правило:
//«Если ты хочешь, чтобы тебя можно было ударить — реализуй эти два метода».
// IDamageable.cs

using UnityEngine;

public interface IDamageable
{
    void TakeDamage(int damage, Vector2 attackDirection, GameObject attacker);
    void Die();
}