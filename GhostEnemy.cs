using System.Collections.Generic;
using UnityEngine;

namespace AiLab01
{
    /// <summary>
    /// Тип №2 — «Призрак», телепортирующийся противник.
    /// Обнаружение: периодический скан 360° малым радиусом; после «фиксации» цель
    /// удерживается, пока игрок в пределах trackDistance и виден (Line of Sight).
    /// Дополнительно к базовому автомату имеет состояние TELEPORT.
    /// </summary>
    public class GhostEnemy : EnemyBase
    {
        [Header("Сканирование 360°")]
        public float scanRadius = 2f;
        public float scanInterval = 0.5f;
        public float trackDistance = 6f;

        /// Корпус игрока ~0.4 м: импульс скана «цепляет» игрока краем кольца,
        /// поэтому фактическая проверка — центр игрока в пределах scanRadius + корпус.
        const float PlayerBodyRadius = 0.4f;

        [Header("Телепортация")]
        public float teleportCooldown = 5f;
        public float maxTeleportDistance = 6f;
        public float teleportTriggerDistance = 3.2f;

        /// true, когда игрок зафиксирован сканом (цель «не забыта» до конца поиска).
        public bool PlayerLocked { get; private set; }

        public Vector2 PendingTeleportTarget { get; set; }

        float scanTimer;
        float teleportReadyTime;
        bool searchTeleportUsed;
        ScanPulse pulse;
        LineRenderer link;

        protected override void Awake()
        {
            base.Awake();

            // миграция старых сцен: радиус скана увеличен вдвое (1 м → 2 м).
            // Точная проверка старого дефолта — значения, заданные вручную, не трогаем.
            if (Mathf.Approximately(scanRadius, 1f))
            {
                scanRadius = 2f;
                Debug.Log("[ПРИЗРАК] радиус скана увеличен: 1 м → 2 м " +
                          "(пересоберите сцену меню AI Lab 1, чтобы сохранить значение)");
            }

            // визуальный импульс сканирования (дочерний объект)
            var p = new GameObject("ScanPulse");
            p.transform.SetParent(transform, false);
            pulse = p.AddComponent<ScanPulse>();
            pulse.Init(scanRadius, scanInterval);

            // линия «захвата цели» — видна, пока игрок отслеживается
            var l = new GameObject("Link");
            l.transform.SetParent(transform, false);
            link = l.AddComponent<LineRenderer>();
            link.positionCount = 2;
            link.useWorldSpace = true;
            link.startWidth = 0.06f;
            link.endWidth = 0.06f;
            link.sortingOrder = 4;
            link.material = new Material(Shader.Find("Sprites/Default"));
            link.startColor = link.endColor = new Color(0.75f, 0.5f, 1f, 0.6f);
            link.enabled = false;
        }

        protected override void CreateStates()
        {
            idleState = new IdleState(this);
            patrolState = new PatrolState(this);
            chaseState = new ChaseState(this);
            attackState = new AttackState(this);
            searchState = new SearchState(this);
            teleportState = new TeleportState(this);
        }

        /// Периодический 360°-скан: радиус мал (1 м), но круговой и с проверкой стен.
        protected override bool RawCanSeePlayer()
        {
            if (player == null) return false;

            scanTimer += Time.deltaTime;
            if (scanTimer >= scanInterval)
            {
                scanTimer = 0f;
                if (pulse != null) pulse.Play();
                if (DistanceToPlayer <= scanRadius + PlayerBodyRadius && HasLineOfSight())
                    PlayerLocked = true; // цель зафиксирована
            }

            // после фиксации игрок удерживается, пока он близко и нет стены
            if (PlayerLocked && DistanceToPlayer <= trackDistance && HasLineOfSight())
                return true;
            return false;
        }

        public override void OnSearchStart() => searchTeleportUsed = false;

        public override void OnSearchFailed() => PlayerLocked = false;

        // ---------------- Телепортация ----------------

        /// В Chase: телепортируемся, если игрок пытается убежать (далеко) и способность готова.
        public override bool ShouldTeleport()
        {
            if (Time.time < teleportReadyTime || player == null) return false;
            if (PlayerVisible && DistanceToPlayer > teleportTriggerDistance)
            {
                PendingTeleportTarget = player.position;
                return true;
            }
            return false;
        }

        /// В Search: один раз за поиск можно телепортироваться к последней известной позиции.
        public override bool ShouldTeleportToLastKnown()
        {
            if (Time.time < teleportReadyTime || searchTeleportUsed) return false;
            if (Vector2.Distance(transform.position, LastKnownPlayerPos) > teleportTriggerDistance)
            {
                PendingTeleportTarget = LastKnownPlayerPos;
                searchTeleportUsed = true;
                return true;
            }
            return false;
        }

        /// Подбор точки назначения: около цели, не дальше maxTeleportDistance,
        /// и только там, где нет препятствия (запрет телепортации внутрь стен).
        public bool FindTeleportDestination(Vector2 target, out Vector2 dest)
        {
            Vector2 from = transform.position;
            Vector2 dir = target - from;
            float d = dir.magnitude;
            // желаемая точка — чуть дальше радиуса атаки от цели
            Vector2 desired = d > 0.01f ? target - dir / d * (attackRange * 0.75f) : target;
            Vector2 clamped = from + Vector2.ClampMagnitude(desired - from, maxTeleportDistance);

            // проверяем саму точку и кольца кандидатов вокруг неё
            var cands = new List<Vector2> { clamped };
            float[] rs = { 0.7f, 1.4f, 2.1f };
            for (int ri = 0; ri < rs.Length; ri++)
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f + ri * 0.35f;
                    cands.Add(clamped + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rs[ri]);
                }
            cands.Sort((a, b) => (a - clamped).sqrMagnitude.CompareTo((b - clamped).sqrMagnitude));

            foreach (var c in cands)
                if (!Physics2D.OverlapCircle(c, 0.45f, obstacleMask))
                {
                    dest = c;
                    return true;
                }
            dest = default;
            return false;
        }

        /// Мгновенное перемещение в проверенную точку + эффекты.
        public void ApplyTeleport(Vector2 dest)
        {
            Fx.RingAt(transform.position, 1.6f, new Color(0.75f, 0.5f, 1f, 0.95f), 0.4f, 3f);
            transform.position = new Vector3(dest.x, dest.y, transform.position.z);
            Rb.position = dest;
            teleportReadyTime = Time.time + teleportCooldown;
            Fx.RingAt(dest, 1.8f, new Color(0.75f, 0.5f, 1f, 0.95f), 0.45f, 3f);
        }

        /// Видимость/материальность (фаза «в телепорте» враг неуязвим и не сталкивается).
        public void SetCorporeal(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (!(r is LineRenderer)) r.enabled = on;
            foreach (var c in GetComponents<Collider2D>()) c.enabled = on;
            Rb.simulated = on;
        }

        protected override void Update()
        {
            base.Update();
            bool show = PlayerVisible;
            if (link != null)
            {
                link.enabled = show;
                if (show && player != null)
                {
                    link.SetPosition(0, transform.position);
                    link.SetPosition(1, player.position);
                }
            }
        }

        /// Вместо маршрута — свободное блуждание вокруг исходной позиции.
        public override Vector2 GetNextPatrolPoint()
        {
            for (int i = 0; i < 12; i++)
            {
                Vector2 p = anchor + Random.insideUnitCircle.normalized * Random.Range(1.5f, 4.5f);
                if (!Physics2D.OverlapCircle(p, 0.5f, obstacleMask)) return p;
            }
            return anchor;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.75f, 0.5f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, scanRadius);
        }
    }
}
