using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalProjectilePool
    {
        private const int Capacity = 256;

        private sealed class Projectile
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Damage;
            public float DieAt;
            public bool Active;
        }

        private readonly Projectile[] projectiles = new Projectile[Capacity];
        private readonly OrbitalEnemyCrowd crowd;
        private int cursor;
        public float VisualAlpha = 1f;

        public OrbitalProjectilePool(Transform parent, OrbitalPrimitiveFactory factory,
            OrbitalEnemyCrowd crowd)
        {
            this.crowd = crowd;
            Transform root = new GameObject("Projectile Pool").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < Capacity; i++)
            {
                SpriteRenderer renderer = factory.CreateSprite($"Projectile {i + 1:000}", root,
                    factory.Circle, new Color(.25f, 1f, 1f, 1f), new Vector2(.12f, .12f), 13);
                renderer.gameObject.SetActive(false);
                projectiles[i] = new Projectile { Transform = renderer.transform, Renderer = renderer };
            }
        }

        public void Fire(Vector2 position, Vector2 direction, float speed, float damage, float range)
        {
            Projectile projectile = projectiles[cursor++];
            if (cursor >= Capacity) cursor = 0;
            projectile.Transform.position = position;
            float powerScale = Mathf.Clamp(.1f + damage * .0025f, .11f, .2f);
            projectile.Transform.localScale = Vector3.one * powerScale;
            projectile.Velocity = direction.normalized * speed;
            projectile.Damage = damage;
            projectile.DieAt = Time.time + Mathf.Max(.25f, range / Mathf.Max(.1f, speed) + .2f);
            projectile.Active = true;
            projectile.Renderer.color = new Color(.25f, 1f, 1f, Mathf.Clamp01(VisualAlpha));
            projectile.Renderer.gameObject.SetActive(true);
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < Capacity; i++)
            {
                Projectile projectile = projectiles[i];
                if (!projectile.Active) continue;
                if (Time.time >= projectile.DieAt)
                {
                    Deactivate(projectile);
                    continue;
                }
                Vector2 position = (Vector2)projectile.Transform.position + projectile.Velocity * deltaTime;
                projectile.Transform.position = position;
                int target = crowd.FindNearest(position, .24f);
                if (target < 0) continue;
                crowd.Damage(target, projectile.Damage);
                Deactivate(projectile);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++) Deactivate(projectiles[i]);
        }

        private static void Deactivate(Projectile projectile)
        {
            if (!projectile.Active) return;
            projectile.Active = false;
            projectile.Renderer.gameObject.SetActive(false);
        }
    }
}
