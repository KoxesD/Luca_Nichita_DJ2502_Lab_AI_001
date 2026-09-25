using UnityEngine;

namespace AiLab01
{
    /// <summary>
    /// Игрок: движение в 8 направлениях (WASD / стрелки) через Rigidbody2D.
    /// Диагональ нормализуется, поэтому скорость во всех 8 направлениях одинакова.
    /// Спрайт поворачивается по направлению движения.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public float speed = 3.5f;

        Rigidbody2D rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            NormalizeCollider();
        }

        /// <summary>
        /// Дистанции атаки врагов и радиус скана Призрака рассчитаны на корпус игрока
        /// ~0.4 м. Спрайт игрока масштабируется трансформом (1.3), поэтому радиус
        /// коллайдера приводится к эффективным 0.4 м независимо от масштаба — иначе
        /// враги физически не могут приблизиться на дистанцию атаки/скана.
        /// </summary>
        void NormalizeCollider()
        {
            var col = GetComponent<CircleCollider2D>();
            if (col == null) return;
            float s = Mathf.Max(Mathf.Abs(transform.localScale.x),
                                Mathf.Abs(transform.localScale.y), 0.01f);
            float want = 0.40f / s;
            if (col.radius > want + 0.005f)
                col.radius = want;
        }

        void Update()
        {
            // пока работает авто-демо (F1), игрок управляется DemoDirector'ом
            if (DemoDirector.Driving) return;

            float x = Input.GetAxisRaw("Horizontal"); // A/D и ←/→
            float y = Input.GetAxisRaw("Vertical");   // W/S и ↑/↓
            Vector2 dir = new Vector2(x, y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            rb.linearVelocity = dir * speed;

            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }
    }
}
