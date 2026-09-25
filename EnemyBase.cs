using System.Collections.Generic;
using UnityEngine;

namespace AiLab01
{
    /// <summary>
    /// Базовый класс врага: владеет конечным автоматом, отвечает за движение
    /// (векторное — не менее 8 направлений), «память взгляда», атаку и подпись состояния.
    /// Конкретные типы врагов переопределяют способ обнаружения игрока.
    /// </summary>
    public abstract class EnemyBase : MonoBehaviour
    {
        [Header("Основные параметры")]
        public string displayName = "Враг";
        public float moveSpeed = 2.3f;
        public float attackRange = 0.95f;
        public float attackDamage = 12f;
        public float attackCooldown = 1f;
        public float idleDuration = 2f;
        public float searchDuration = 3f;
        [Tooltip("Сколько секунд после пропадания из зоны видимости игрок ещё считается видимым")]
        public float loseSightDelay = 0.35f;
        [Tooltip("Максимальная дистанция преследования: дальше NPC «теряет» игрока")]
        public float chaseLeashRange = 7.5f;

        [Header("Здоровье")]
        public float maxHp = 30f;
        [Tooltip("Через сколько секунд после уничтожения враг возвращается на исходную позицию")]
        public float respawnDelay = 5f;
        [Tooltip("false — после уничтожения враг удаляется со сцены навсегда")]
        public bool respawnOnDeath = true;

        [Header("Обход препятствий")]
        [Tooltip("Как часто перестраивать маршрут, когда прямой путь к цели закрыт стеной")]
        public float repathInterval = 0.6f;

        [Header("Ссылки")]
        public Transform player;
        public LayerMask obstacleMask;
        public Transform[] patrolPoints;

        /// Список всех врагов сцены (для HUD и демо-режима).
        public static readonly List<EnemyBase> All = new List<EnemyBase>();

        public StateMachine Brain { get; private set; }
        public Rigidbody2D Rb { get; private set; }
        public bool PlayerVisible { get; private set; }
        public float Hp { get; private set; }
        public bool IsDead { get; private set; }
        public Vector2 LastKnownPlayerPos { get; protected set; }
        public Vector2 Facing => facing;
        public float DistanceToPlayer => player != null
            ? Vector2.Distance(transform.position, player.position)
            : float.MaxValue;
        public bool HasTeleportAbility => teleportState != null;
        public string CurrentStateName => Brain.CurrentName;

        protected Vector2 facing = Vector2.right;
        protected Vector2 anchor;
        protected Health playerHealth;

        protected float nextAttackTime;
        protected float sightMemory;

        protected IdleState idleState;
        protected PatrolState patrolState;
        protected ChaseState chaseState;
        protected AttackState attackState;
        protected SearchState searchState;
        protected TeleportState teleportState; // только у телепортирующегося врага

        TextMesh stateLabel;
        int wpIndex = -1;

        protected virtual void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();
            Brain = new StateMachine();
            anchor = transform.position;
            Hp = maxHp;
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }
            if (player != null) playerHealth = player.GetComponent<Health>();
            facing = transform.right;

            // нормализуем физический радиус корпуса: дистанции атаки и скана
            // рассчитаны на корпус ~0.4 м (иначе враг не сможет войти в радиус атаки)
            var col = GetComponent<CircleCollider2D>();
            if (col != null && col.radius > 0.42f)
                col.radius = 0.40f;
        }

        protected virtual void Start()
        {
            CreateStates();
            Brain.Set(idleState);
            CreateStateLabel();
        }

        protected virtual void Update()
        {
            if (IsDead) return;     // уничтоженный враг не думает и не рисуется
            Sense();
            Brain.Tick();
            UpdateStateLabel();
        }

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        // ---------------- Восприятие ----------------

        /// Обновление «видимости» игрока с учётом короткой памяти взгляда.
        void Sense()
        {
            if (player == null) { PlayerVisible = false; return; }

            if (RawCanSeePlayer())
            {
                PlayerVisible = true;
                sightMemory = loseSightDelay;
                LastKnownPlayerPos = player.position;
            }
            else
            {
                sightMemory -= Time.deltaTime;
                if (sightMemory <= 0f) PlayerVisible = false;
            }
        }

        /// «Сырая» проверка обнаружения — у каждого типа врага своя.
        protected abstract bool RawCanSeePlayer();

        /// Line of Sight: между NPC и игроком не должно быть стены (Raycast/Linecast).
        protected bool HasLineOfSight()
        {
            var hit = Physics2D.Linecast(transform.position, player.position, obstacleMask);
            return hit.collider == null;
        }

        // ---------------- Движение ----------------

        // маршрут обхода препятствий (общий для всех состояний)
        Vector2[] navPath;
        int navIndex;
        Vector2 navGoal;
        float repathTimer;
        const float WaypointReach = 0.3f;

        /// <summary>
        /// Плавное векторное движение к цели (любое из 8+ направлений) с обходом
        /// препятствий: если прямая свободна — идём напролом; иначе строим путь
        /// по сетке занятости (A* в NavGrid) и следуем его поворотным точкам.
        /// </summary>
        public void MoveTo(Vector2 target)
        {
            Vector2 pos = transform.position;

            // 1) прямой путь свободен (с учётом радиуса корпуса) — кратчайший вариант
            if (NavGrid.LineClear(pos, target, obstacleMask))
            {
                navPath = null;
                Drive(target);
                return;
            }

            // 2) путь закрыт стеной — строим/перестраиваем маршрут
            repathTimer -= Time.deltaTime;
            bool rebuild = navPath == null || navIndex >= navPath.Length
                || (target - navGoal).sqrMagnitude > 0.36f
                || repathTimer <= 0f;
            if (rebuild)
            {
                navGoal = target;
                repathTimer = repathInterval;
                var found = new List<Vector2>();
                navPath = NavGrid.FindPath(pos, target, obstacleMask, found) ? found.ToArray() : null;
                navIndex = 0;
            }

            if (navPath == null || navPath.Length == 0)
            {
                Drive(target);   // пути нет — хотя бы направление (патруль потом сменит точку)
                return;
            }

            while (navIndex < navPath.Length - 1 &&
                   Vector2.Distance(pos, navPath[navIndex]) < WaypointReach)
                navIndex++;

            Drive(navPath[navIndex]);
        }

        /// Прямое управление скоростью к точке: поворот «взгляда» + скорость.
        void Drive(Vector2 target)
        {
            Vector2 to = target - (Vector2)transform.position;
            SetFacing(to);
            Rb.linearVelocity = to.sqrMagnitude > 0.01f ? to.normalized * moveSpeed : Vector2.zero;
        }

        public void Stop() => Rb.linearVelocity = Vector2.zero;

        /// Плавный поворот «взгляда» к направлению.
        public void SetFacing(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            float target = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float cur = transform.eulerAngles.z;
            float next = cur + Mathf.DeltaAngle(cur, target) * Mathf.Min(1f, 10f * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, next);
            facing = transform.right;
        }

        public void SetFacingAngle(float deg) =>
            SetFacing(new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)));

        // ---------------- Атака ----------------

        public bool TryAttack()
        {
            if (Time.time < nextAttackTime) return false;
            nextAttackTime = Time.time + attackCooldown;
            if (playerHealth != null) playerHealth.TakeDamage(attackDamage);
            OnAttackFeedback();
            return true;
        }

        protected virtual void OnAttackFeedback()
        {
            Fx.Flash(GetComponentInChildren<SpriteRenderer>(), new Color(1f, 1f, 1f), 0.1f);
            if (player != null)
                Fx.RingAt(player.position, 0.8f, new Color(1f, 0.35f, 0.3f, 0.9f), 0.3f, 2f);
        }

        // ---------------- Здоровье ----------------

        /// Получение урона от игрока: вспышка, всплывающее число, при нуле — уничтожение.
        public void TakeDamage(float damage)
        {
            if (IsDead) return;
            Hp = Mathf.Max(0f, Hp - damage);
            Fx.Flash(GetComponentInChildren<SpriteRenderer>(), new Color(1f, 1f, 1f), 0.08f);
            Fx.FloatText((Vector2)transform.position + Vector2.up * 1.6f,
                "−" + Mathf.RoundToInt(damage), new Color(1f, 0.85f, 0.4f));
            if (Hp <= 0f) Die();
        }

        void Die()
        {
            IsDead = true;
            Stop();
            Fx.RingAt(transform.position, 2.6f, new Color(1f, 0.75f, 0.35f, 0.95f), 0.5f, 4f);
            Fx.FloatText((Vector2)transform.position + Vector2.up * 1.6f,
                displayName + " ПОРАЖЁН", new Color(1f, 0.6f, 0.3f));
            SetAlive(false);
            if (respawnOnDeath)
                Invoke(nameof(Respawn), respawnDelay);
            else
                Destroy(gameObject, 1f);
        }

        /// Возврат на исходную позицию с полным здоровьем и перезапуском автомата.
        void Respawn()
        {
            transform.position = anchor;
            Rb.position = anchor;
            Hp = maxHp;
            IsDead = false;
            sightMemory = 0f;
            PlayerVisible = false;
            SetAlive(true);
            Brain.Set(idleState);
        }

        /// Включение/выключение «тела» врага (уничтожение/респаун, фаза телепорта).
        void SetAlive(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (!(r is LineRenderer)) r.enabled = on;
            foreach (var c in GetComponents<Collider2D>()) c.enabled = on;
            if (Rb != null) Rb.simulated = on;
            if (stateLabel != null) stateLabel.gameObject.SetActive(on);
        }

        // ---------------- Патрулирование ----------------

        /// Следующая точка маршрута. Базовая версия идёт по точкам patrolPoints.
        public virtual Vector2 GetNextPatrolPoint()
        {
            if (patrolPoints == null || patrolPoints.Length == 0) return anchor;
            wpIndex = (wpIndex + 1) % patrolPoints.Length;
            return patrolPoints[wpIndex].position;
        }

        // ---------------- Переходы FSM ----------------

        public void GoToChase() => Brain.Set(chaseState);
        public void GoToPatrol() => Brain.Set(patrolState);
        public void GoToSearch() => Brain.Set(searchState);
        public void GoToAttack() => Brain.Set(attackState);
        public void GoToTeleport()
        {
            if (teleportState != null) Brain.Set(teleportState);
        }

        /// Условие телепортации, проверяемое в состоянии Chase.
        public virtual bool ShouldTeleport() => false;

        /// Условие телепортации, проверяемое в состоянии Search.
        public virtual bool ShouldTeleportToLastKnown() => false;

        public virtual void OnSearchStart() { }
        public virtual void OnSearchFailed() { }

        /// Создание экземпляров состояний для конкретного типа врага.
        protected abstract void CreateStates();

        // ---------------- Подпись состояния над врагом ----------------

        void CreateStateLabel()
        {
            stateLabel = WorldText.Create(
                (Vector2)transform.position + Vector2.up * 1.2f,
                displayName, TextAnchor.MiddleCenter, goName: "Label_" + displayName);
        }

        void UpdateStateLabel()
        {
            if (stateLabel == null) return;
            stateLabel.text = displayName + " · " + Brain.CurrentName;
            stateLabel.transform.position = (Vector2)transform.position + Vector2.up * 1.2f;
        }
    }
}
