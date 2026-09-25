using UnityEngine;

namespace AiLab01
{
    /// <summary>
    /// Тип №1 — «Страж», обычный противник.
    /// Обнаружение: направленное поле зрения 180° (дальность + угол + Raycast на препятствия).
    /// </summary>
    public class GuardEnemy : EnemyBase
    {
        [Header("Поле зрения 180°")]
        public float viewDistance = 5.5f;
        public float viewAngleDeg = 180f;

        protected override void CreateStates()
        {
            idleState = new IdleState(this);
            patrolState = new PatrolState(this);
            chaseState = new ChaseState(this);
            attackState = new AttackState(this);
            searchState = new SearchState(this);
        }

        /// Игрок обнаружен, если он: (1) в пределах дальности, (2) внутри угла обзора,
        /// (3) не закрыт препятствием (Line of Sight).
        protected override bool RawCanSeePlayer()
        {
            if (player == null) return false;
            Vector2 to = (Vector2)player.position - (Vector2)transform.position;
            if (to.magnitude > viewDistance) return false;
            if (Vector2.Angle(facing, to) > viewAngleDeg * 0.5f) return false;
            return HasLineOfSight();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector2 o = transform.position;
            float half = viewAngleDeg * 0.5f;
            float mid = Mathf.Atan2(facing.y, facing.x);
            float a0 = mid - half * Mathf.Deg2Rad;
            float a1 = mid + half * Mathf.Deg2Rad;
            Gizmos.DrawLine(o, o + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * viewDistance);
            Gizmos.DrawLine(o, o + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * viewDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(o, o + facing * viewDistance);
        }
    }
}
