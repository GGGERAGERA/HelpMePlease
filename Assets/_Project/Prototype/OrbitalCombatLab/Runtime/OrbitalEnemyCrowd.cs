using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalEnemyCrowd
    {
        public const int Capacity = 300;

        public sealed class Enemy
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 PushVelocity;
            public float Hp;
            public float RespawnAt;
            public float FlashUntil;
            public float LastRingHit;
            public float SlowUntil;
            public float SlowMultiplier = 1f;
            public bool Active;
        }

        public readonly Enemy[] Enemies = new Enemy[Capacity];
        public int DesiredCount { get; private set; }
        public int ActiveCount { get; private set; }
        public float EnemyMaxHp = 38f;
        public float EnemySpeed = 1.8f;
        public bool DamagePlayer;
        public float VisualAlpha = 1f;

        private readonly Transform root;
        private readonly OrbitalLabStats stats;
        private readonly System.Action<Vector2> deathPop;
        private readonly System.Random random = new(42042);
        private Vector2 center;
        private float spawnRadius = 12f;

        public OrbitalEnemyCrowd(Transform parent, OrbitalPrimitiveFactory factory, OrbitalLabStats stats,
            System.Action<Vector2> deathPop)
        {
            this.stats = stats;
            this.deathPop = deathPop;
            root = new GameObject("Enemy Crowd Pool").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < Capacity; i++)
            {
                SpriteRenderer renderer = factory.CreateSprite($"Enemy {i + 1:000}", root,
                    factory.Circle, new Color(.43f, .055f, .075f, 1f), new Vector2(.34f, .34f), 8);
                renderer.gameObject.SetActive(false);
                Enemies[i] = new Enemy { Transform = renderer.transform, Renderer = renderer };
            }
        }

        public void SetCount(int count, Vector2 newCenter, float outerRadius)
        {
            center = newCenter;
            spawnRadius = Mathf.Max(outerRadius + 4.5f, 10f);
            DesiredCount = Mathf.Clamp(count, 0, Capacity);
            for (int i = 0; i < Capacity; i++)
            {
                if (i < DesiredCount)
                {
                    if (!Enemies[i].Active) Spawn(i, true);
                }
                else Deactivate(i);
            }
            Recount();
        }

        public void Tick(Vector2 newCenter, float outerRadius, float deltaTime, bool immortal,
            ref float playerHp)
        {
            center = newCenter;
            spawnRadius = Mathf.Max(outerRadius + 4.5f, 10f);
            float now = Time.unscaledTime;
            int count = 0;
            for (int i = 0; i < DesiredCount; i++)
            {
                Enemy enemy = Enemies[i];
                if (!enemy.Active)
                {
                    if (now >= enemy.RespawnAt) Spawn(i, false);
                    continue;
                }

                Vector2 position = enemy.Transform.position;
                Vector2 toCenter = center - position;
                float distance = toCenter.magnitude;
                Vector2 direction = distance > .001f ? toCenter / distance : Vector2.zero;
                enemy.PushVelocity = Vector2.MoveTowards(enemy.PushVelocity, Vector2.zero,
                    8.5f * deltaTime);
                float speedMultiplier = now < enemy.SlowUntil ? enemy.SlowMultiplier : 1f;
                position += (direction * EnemySpeed * speedMultiplier + enemy.PushVelocity) * deltaTime;
                enemy.Transform.position = new Vector3(position.x, position.y, 0f);
                Color visual = now < enemy.FlashUntil
                    ? new Color(1f, .36f, .28f, 1f)
                    : new Color(.43f, .055f, .075f, 1f);
                visual.a = Mathf.Clamp01(VisualAlpha);
                enemy.Renderer.color = visual;
                if (distance < .52f && DamagePlayer && !immortal)
                    playerHp = Mathf.Max(0f, playerHp - 9f * deltaTime);
                count++;
            }
            ActiveCount = count;
            stats.ActiveEnemies = count;
        }

        public int FindNearest(Vector2 position, float range)
        {
            int best = -1;
            float bestSqr = range * range;
            for (int i = 0; i < DesiredCount; i++)
            {
                Enemy enemy = Enemies[i];
                if (!enemy.Active) continue;
                float sqr = ((Vector2)enemy.Transform.position - position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }
            return best;
        }

        public bool Damage(int index, float amount)
        {
            if (index < 0 || index >= DesiredCount) return false;
            Enemy enemy = Enemies[index];
            if (!enemy.Active) return false;
            enemy.Hp -= amount;
            enemy.FlashUntil = Time.unscaledTime + .075f;
            enemy.Transform.localScale = Vector3.one * .39f;
            if (enemy.Hp > 0f) return true;
            Kill(index);
            return true;
        }

        public void Push(int index, Vector2 origin, float force)
        {
            if (index < 0 || index >= DesiredCount) return;
            Enemy enemy = Enemies[index];
            if (!enemy.Active) return;
            Vector2 direction = (Vector2)enemy.Transform.position - origin;
            if (direction.sqrMagnitude < .0001f)
                direction = Vector2.right;
            enemy.PushVelocity += direction.normalized * force;
        }

        public void Slow(int index, float multiplier, float duration)
        {
            if (index < 0 || index >= DesiredCount) return;
            Enemy enemy = Enemies[index];
            if (!enemy.Active) return;
            enemy.SlowMultiplier = Mathf.Clamp(multiplier, .05f, 1f);
            enemy.SlowUntil = Mathf.Max(enemy.SlowUntil, Time.unscaledTime + duration);
        }

        public void ApplyRingContact(Vector2 newCenter, OrbitalRing[] rings, int ringCount,
            bool damageEnabled, bool pushEnabled, float deltaTime)
        {
            if (!damageEnabled && !pushEnabled) return;
            float now = Time.unscaledTime;
            for (int i = 0; i < DesiredCount; i++)
            {
                Enemy enemy = Enemies[i];
                if (!enemy.Active) continue;
                Vector2 delta = (Vector2)enemy.Transform.position - newCenter;
                float radius = delta.magnitude;
                for (int r = 0; r < ringCount; r++)
                {
                    OrbitalRing ring = rings[r];
                    if (Mathf.Abs(radius - ring.Settings.Radius) > .15f ||
                        now - enemy.LastRingHit < .28f) continue;
                    enemy.LastRingHit = now;
                    if (damageEnabled && ring.Settings.ContactDamage > 0f)
                        Damage(i, ring.Settings.ContactDamage);
                    if (pushEnabled && ring.Settings.ContactPush > 0f && enemy.Active)
                        enemy.PushVelocity += delta.normalized * ring.Settings.ContactPush;
                    break;
                }
            }
        }

        private void Kill(int index)
        {
            Enemy enemy = Enemies[index];
            deathPop?.Invoke(enemy.Transform.position);
            enemy.Active = false;
            enemy.Renderer.gameObject.SetActive(false);
            enemy.RespawnAt = Time.unscaledTime + .28f;
            stats.Kills++;
        }

        private void Spawn(int index, bool stagger)
        {
            Enemy enemy = Enemies[index];
            float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
            float radius = spawnRadius + (float)random.NextDouble() * 4f;
            enemy.Transform.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            enemy.Transform.localScale = Vector3.one * .34f;
            enemy.Hp = EnemyMaxHp;
            enemy.PushVelocity = Vector2.zero;
            enemy.LastRingHit = -99f;
            enemy.Active = true;
            enemy.Renderer.gameObject.SetActive(true);
            if (stagger)
            {
                float inward = (float)random.NextDouble() * Mathf.Max(0f, spawnRadius - 2f);
                Vector2 direction = (center - (Vector2)enemy.Transform.position).normalized;
                enemy.Transform.position += (Vector3)(direction * inward);
            }
        }

        private void Deactivate(int index)
        {
            Enemy enemy = Enemies[index];
            enemy.Active = false;
            enemy.Renderer.gameObject.SetActive(false);
        }

        private void Recount()
        {
            int count = 0;
            for (int i = 0; i < DesiredCount; i++) if (Enemies[i].Active) count++;
            ActiveCount = count;
            stats.ActiveEnemies = count;
        }
    }
}
