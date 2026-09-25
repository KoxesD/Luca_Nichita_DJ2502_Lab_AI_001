using UnityEngine;

namespace AiLab01
{
    /// <summary>Здоровье игрока: получение урона, смерть и респаун в начальной точке.</summary>
    public class Health : MonoBehaviour
    {
        public float maxHp = 100f;
        public float respawnDelay = 1.5f;
        public Vector3 respawnPoint;

        public float Hp { get; private set; }
        public float MaxHp => maxHp;
        public bool IsDead { get; private set; }

        void Awake()
        {
            Hp = maxHp;
            if (respawnPoint == Vector3.zero) respawnPoint = transform.position;
        }

        public void TakeDamage(float damage)
        {
            if (IsDead) return;
            Hp = Mathf.Max(0f, Hp - damage);
            Fx.Flash(GetComponentInChildren<SpriteRenderer>(), new Color(1f, 0.45f, 0.45f), 0.12f);
            if (Hp <= 0f) Die();
        }

        void Die()
        {
            IsDead = true;
            SetVisuals(false);
            Invoke(nameof(Respawn), respawnDelay);
        }

        void Respawn()
        {
            transform.position = respawnPoint;
            Hp = maxHp;
            IsDead = false;
            SetVisuals(true);
        }

        void SetVisuals(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = on;
            foreach (var c in GetComponents<Collider2D>()) c.enabled = on;
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.simulated = on;
            var pc = GetComponent<PlayerController>();
            if (pc != null) pc.enabled = on;
        }
    }
}
