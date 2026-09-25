using UnityEngine;
using UnityEngine.InputSystem;

namespace AiLab01
{
    /// <summary>
    /// Атака игрока (ближний бой): по нажатию атаки бьёт всех врагов в радиусе.
    /// Работает через новую Input System (карта «Player», действие «Attack» —
    /// Пробел / Enter / ЛКМ). Урон получает любой EnemyBase в радиусе,
    /// если он не «растворён» (фаза телепорта) и не уничтожен.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Attack")]
        [SerializeField] private float attackRadius = 1.25f;
        [SerializeField] private float attackDamage = 10f;
        [SerializeField] private float attackCooldown = 0.45f;

        [Header("Enemy")]
        [SerializeField] private LayerMask enemyLayer;

        private PlayerInput playerInput;

        private float attackTimer;

        private void Awake()
        {
            playerInput = new PlayerInput();
        }

        private void OnEnable()
        {
            playerInput.Enable();
            playerInput.Player.Attack.performed += OnAttack;
        }

        private void OnDisable()
        {
            playerInput.Player.Attack.performed -= OnAttack;
            playerInput.Disable();
        }

        private void OnDestroy()
        {
            playerInput.Dispose();
        }

        private void Update()
        {
            if (attackTimer > 0f)
                attackTimer -= Time.deltaTime;
        }

        private void OnAttack(InputAction.CallbackContext context)
        {
            if (attackTimer > 0f)
                return;
            if (DemoDirector.Driving)          // во время авто-демо игрок не атакует
                return;

            Attack();
            attackTimer = attackCooldown;
        }

        private void Attack()
        {
            // визуальная обратная связь: кольцо взмаха вокруг игрока
            Fx.RingAt(transform.position, attackRadius * 1.7f,
                new Color(0.55f, 0.95f, 1f, 0.85f), 0.22f, 2.5f);

            Collider2D[] enemies = Physics2D.OverlapCircleAll(
                transform.position,
                attackRadius,
                enemyLayer);

            foreach (Collider2D enemyCollider in enemies)
            {
                EnemyBase enemy = enemyCollider.GetComponentInParent<EnemyBase>();

                if (enemy != null)
                {
                    enemy.TakeDamage(attackDamage);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, attackRadius);
        }
    }
}
