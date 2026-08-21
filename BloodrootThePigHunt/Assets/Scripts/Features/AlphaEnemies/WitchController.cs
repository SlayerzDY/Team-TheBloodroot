using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Bloodroot.Features.AlphaEnemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public class WitchController : MonoBehaviour, global::IDamage
    {
        [Header("Identity")]
        [SerializeField] private WitchVariant variant = WitchVariant.ShieldBearer;

        [Header("Required Runtime References")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform secondaryAttackTarget;
        [SerializeField] private bool alternateAttackTargets = true;
        [SerializeField] private bool resolveTargetFromGameManager = true;
        [SerializeField] private Rigidbody flightBody;

        [Header("Vitals")]
        [SerializeField, Min(1)] private int baseMaxHealth = 1;
        [SerializeField, Min(0)] private int baseMagicDamage = 28;
        [SerializeField, Min(0f)] private float deathDestroyDelay = 4f;
        [SerializeField] private bool destroyOnDeath = true;

        [Header("Flying Navigation")]
        [SerializeField] private Transform[] flightWaypoints;
        [SerializeField, Min(0.01f)] private float baseFlightSpeed = 7f;
        [SerializeField, Min(0.1f)] private float attackOrbitRadius = 13f;
        [SerializeField, Min(0f)] private float orbitDegreesPerSecond = 28f;
        [SerializeField, Min(0f)] private float hoverHeight = 7f;
        [SerializeField, Min(0f)] private float minimumGroundClearance = 3f;
        [SerializeField, Min(1f)] private float maximumVerticalDistanceFromHome = 25f;
        [SerializeField, Min(0.05f)] private float waypointTolerance = 1.25f;
        [SerializeField, Min(0.05f)] private float obstacleProbeRadius = 0.75f;
        [SerializeField, Min(0.1f)] private float obstacleProbeDistance = 4f;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Ranged Magic")]
        [SerializeField] private GameObject magicProjectilePrefab;
        [SerializeField] private Transform magicSpawnPoint;
        [SerializeField, Min(0.1f)] private float magicRange = 28f;
        [SerializeField, Min(0.05f)] private float magicCooldown = 2.25f;
        [SerializeField, Min(0.01f)] private float projectileSpeedScalar = 1f;

        [Header("Shield / Root Fragments")]
        [SerializeField] private WitchRootFragment[] rootFragments;
        [SerializeField] private GameObject shieldVisual;

        [Header("Summoning")]
        [SerializeField] private GameObject[] minionPrefabs;
        [SerializeField] private Transform[] minionSpawnPoints;
        [SerializeField, Min(1)] private int maximumActiveMinions = 3;
        [SerializeField, Min(0.1f)] private float summonCooldown = 9f;
        [SerializeField, Min(0.5f)] private float minionGroundSampleRadius = 15f;
        [SerializeField] private bool destroySpawnedMinionsOnDeath;

        [Header("Variant Multipliers")]
        [SerializeField, Min(0.01f)] private float shieldBearerHealthMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float summonerHealthMultiplier = 0.9f;
        [SerializeField, Min(0.01f)] private float matriarchHealthMultiplier = 1.8f;
        [SerializeField, Min(0f)] private float matriarchDamageMultiplier = 1.35f;

        [Header("Difficulty Scaling")]
        [SerializeField, Min(1)] private int difficultyLevel = 1;
        [SerializeField, Min(0f)] private float healthPerLevel = 0.15f;
        [SerializeField, Min(0f)] private float damagePerLevel = 0.1f;
        [SerializeField, Min(0f)] private float speedPerLevel = 0.02f;
        [SerializeField, Min(0.01f)] private float healthScalar = 1f;
        [SerializeField, Min(0f)] private float damageScalar = 1f;
        [SerializeField, Min(0.01f)] private float speedScalar = 1f;

        [Header("Authored Presentation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string flyingParameter = "Flying";
        [SerializeField] private string castTrigger = "Cast";
        [SerializeField] private string summonTrigger = "Summon";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string shieldHitTrigger = "ShieldHit";
        [SerializeField] private string deathTrigger = "Death";
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip castClip;
        [SerializeField] private AudioClip summonClip;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip shieldHitClip;
        [SerializeField] private AudioClip shieldBreakClip;
        [SerializeField] private AudioClip deathClip;
        [SerializeField] private AudioClip[] ambientClips =
            Array.Empty<AudioClip>();
        [SerializeField] private Vector2 ambientIntervalRange =
            new Vector2(7f, 15f);
        [SerializeField] private Collider[] combatColliders;

        [Header("Configured Loot Hooks")]
        [SerializeField] private GameObject[] lootPrefabs;
        [SerializeField] private Transform lootDropPoint;
        [SerializeField, Min(0f)] private float lootScatterRadius = 0.75f;

        [Header("Authored Events")]
        [SerializeField] private WitchControllerEvent onDamaged = new WitchControllerEvent();
        [SerializeField] private WitchControllerEvent onDied = new WitchControllerEvent();
        [SerializeField] private UnityEvent onShieldHit = new UnityEvent();
        [SerializeField] private UnityEvent onShieldBroken = new UnityEvent();
        [SerializeField] private GameObjectEvent onMinionSummoned = new GameObjectEvent();

        private readonly List<GameObject> activeMinions = new List<GameObject>();
        private readonly RaycastHit[] groundClearanceHits = new RaycastHit[PhysicsHitBufferCapacity];
        private readonly RaycastHit[] attackLineHits = new RaycastHit[PhysicsHitBufferCapacity];
        private Vector3 homePosition;
        private Vector3 desiredFlightPosition;
        private Vector3 cachedObstacleAvoidance;
        private int waypointIndex;
        private int spawnPointIndex;
        private float orbitAngle;
        private float nextMagicAt;
        private float nextSummonAt;
        private float nextFlightClearanceAt;
        private float nextAmbientAt;
        private float ambientEndsAt;
        private float cachedMinimumGroundHeight = float.NegativeInfinity;
        private int currentHealth;
        private int scaledMaxHealth;
        private int scaledMagicDamage;
        private float scaledFlightSpeed = 1f;
        private bool shieldActive;
        private bool combatEnabled = true;
        private bool isDead;
        private bool projectileWarningIssued;
        private bool useSecondaryTargetNext;
        private bool ambientClipPlaying;

        private const int PhysicsHitBufferCapacity = 64;
        private const float FlightClearanceUpdateInterval = 0.1f;

        public event Action<WitchController> Damaged;
        public event Action<WitchController> Died;
        public event Action ShieldBroken;
        public event Action<GameObject> MinionSummoned;

        public WitchVariant Variant => variant;
        public Transform Target => target;
        public Transform SecondaryAttackTarget => secondaryAttackTarget;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => scaledMaxHealth;
        public int MagicDamage => scaledMagicDamage;
        public bool IsShielded => shieldActive;
        public bool IsDead => isDead;
        public bool CombatEnabled => combatEnabled;
        public int ConfiguredAmbientClipCount
        {
            get
            {
                int count = 0;
                foreach (AudioClip clip in
                         ambientClips ?? Array.Empty<AudioClip>())
                {
                    if (clip != null)
                        count++;
                }

                return count;
            }
        }

        protected virtual bool SupportsShield => false;
        protected virtual bool SupportsSummoning => false;
        protected int ActiveMinionCount => activeMinions.Count;
        protected float HealthRatio => scaledMaxHealth > 0
            ? currentHealth / (float)scaledMaxHealth
            : 0f;

        protected virtual void Awake()
        {
            if (flightBody == null)
            {
                flightBody = GetComponent<Rigidbody>();
            }

            homePosition = transform.position;
            desiredFlightPosition = transform.position;
            CacheCollidersIfNeeded();
            BindRootFragments(false);
            RecalculateScaledValues(true);
            RefreshShieldState(false);
            SetAnimatorFlying(true);
        }

        protected virtual void OnEnable()
        {
            nextMagicAt = Time.time + magicCooldown;
            nextSummonAt = Time.time + summonCooldown;
            ScheduleNextAmbient();
            ResetFlightClearanceCache();
            ResolveTargetIfNeeded();
        }

        protected virtual void Update()
        {
            if (isDead)
            {
                return;
            }

            RemoveMissingMinions();
            ResolveTargetIfNeeded();
            UpdateDesiredFlightPosition();
            if (flightBody == null || flightBody.isKinematic)
            {
                MoveWithoutDynamicBody(Time.deltaTime);
            }

            if (!combatEnabled || target == null)
            {
                return;
            }

            TickCombat();
            TickAmbientAudio();
        }

        protected virtual void FixedUpdate()
        {
            if (isDead || flightBody == null || flightBody.isKinematic)
            {
                return;
            }

            Vector3 toDestination = desiredFlightPosition - flightBody.position;
            Vector3 velocity = Vector3.ClampMagnitude(
                toDestination / Mathf.Max(0.0001f, Time.fixedDeltaTime),
                scaledFlightSpeed);
            flightBody.linearVelocity = velocity;
            RotateTowardTargetOrVelocity(velocity, Time.fixedDeltaTime);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetSecondaryAttackTarget(Transform newTarget)
        {
            secondaryAttackTarget = newTarget == target ? null : newTarget;
            useSecondaryTargetNext = false;
        }

        public void SetCombatEnabled(bool enabled)
        {
            combatEnabled = enabled && !isDead;
            if (!combatEnabled)
                StopAmbientIfPlaying();
        }

        public void PrepareForEncounter(
            Transform encounterTarget,
            int level,
            float encounterHealthScalar,
            float encounterDamageScalar,
            float encounterSpeedScalar)
        {
            enabled = true;
            target = encounterTarget;
            secondaryAttackTarget = null;
            useSecondaryTargetNext = false;
            isDead = false;
            combatEnabled = true;
            SetCollidersEnabled(true);
            ApplyDifficulty(level, encounterHealthScalar, encounterDamageScalar, encounterSpeedScalar, true);
            BindRootFragments(true);
            RefreshShieldState(false);
            nextMagicAt = Time.time + magicCooldown;
            nextSummonAt = Time.time + summonCooldown;
            ScheduleNextAmbient();
            ResetFlightClearanceCache();
            SetAnimatorFlying(true);
            OnEncounterPrepared();
        }

        public void ConfigureAmbientAudio(
            AudioSource authoredAudioSource,
            AudioClip[] authoredAmbientClips,
            float minimumInterval,
            float maximumInterval)
        {
            audioSource = authoredAudioSource;
            ambientClips = authoredAmbientClips ?? Array.Empty<AudioClip>();
            float minimum = Mathf.Max(0.1f, minimumInterval);
            ambientIntervalRange = new Vector2(
                minimum,
                Mathf.Max(minimum, maximumInterval));
            ScheduleNextAmbient();
        }

        public bool ValidateAmbientAudio(out string error)
        {
            if (audioSource == null)
            {
                error = "Witch ambient audio requires an authored AudioSource.";
                return false;
            }

            if (ConfiguredAmbientClipCount == 0)
            {
                error =
                    "Witch ambient audio requires at least one authored non-null clip.";
                return false;
            }

            if (ambientIntervalRange.x < 0.1f ||
                ambientIntervalRange.y < ambientIntervalRange.x)
            {
                error = "Witch ambient audio interval is invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void ApplyDifficulty(
            int level,
            float encounterHealthScalar,
            float encounterDamageScalar,
            float encounterSpeedScalar,
            bool restoreHealth = true)
        {
            difficultyLevel = Mathf.Max(1, level);
            healthScalar = Mathf.Max(0.01f, encounterHealthScalar);
            damageScalar = Mathf.Max(0f, encounterDamageScalar);
            speedScalar = Mathf.Max(0.01f, encounterSpeedScalar);
            RecalculateScaledValues(restoreHealth);
        }

        public void NotifyRootFragmentDestroyed(WitchRootFragment fragment)
        {
            if (!SupportsShield || !shieldActive)
            {
                return;
            }

            RefreshShieldState(true);
        }

        public void TakeDamage(int amount)
        {
            if (isDead || amount <= 0)
            {
                return;
            }

            //if (shieldActive)
            //{
            //    TriggerAnimator(shieldHitTrigger);
            //    PlayClip(shieldHitClip);
            //    AlphaEnemyEventUtility.Invoke(onShieldHit, this, nameof(onShieldHit));
            //    return;
            //}

            currentHealth = Mathf.Max(0, currentHealth - amount);
            TriggerAnimator(hitTrigger);
            PlayClip(hitClip);
            AlphaEnemyEventUtility.Invoke(onDamaged, this, this, nameof(onDamaged));
            AlphaEnemyEventUtility.Invoke(Damaged, this, this, nameof(Damaged));
            if (currentHealth == 0)
            {
                Die();
            }
            else
            {
                OnHealthChanged();
            }
        }

        public void onDeath(bool dead)
        {
            if (dead)
            {
                Die();
            }
        }

        private void UpdateDesiredFlightPosition()
        {
            if (target != null)
            {
                orbitAngle = Mathf.Repeat(orbitAngle + orbitDegreesPerSecond * Time.deltaTime, 360f);
                float angleRadians = orbitAngle * Mathf.Deg2Rad;
                Vector3 orbitOffset = new Vector3(Mathf.Cos(angleRadians), 0f, Mathf.Sin(angleRadians)) * attackOrbitRadius;
                desiredFlightPosition = target.position + orbitOffset + Vector3.up * hoverHeight;
            }
            else if (flightWaypoints != null && flightWaypoints.Length > 0)
            {
                Transform waypoint = GetNextValidWaypoint();
                desiredFlightPosition = waypoint != null ? waypoint.position : homePosition;
                if (Vector3.Distance(transform.position, desiredFlightPosition) <= waypointTolerance)
                {
                    waypointIndex = (waypointIndex + 1) % flightWaypoints.Length;
                }
            }
            else
            {
                desiredFlightPosition = homePosition;
            }

            bool refreshClearance = Time.time >= nextFlightClearanceAt;
            if (refreshClearance)
            {
                RefreshGroundClearance();
            }

            if (!float.IsNegativeInfinity(cachedMinimumGroundHeight))
            {
                desiredFlightPosition.y = Mathf.Max(
                    desiredFlightPosition.y,
                    cachedMinimumGroundHeight);
            }

            desiredFlightPosition.y = Mathf.Clamp(
                desiredFlightPosition.y,
                homePosition.y - maximumVerticalDistanceFromHome,
                homePosition.y + maximumVerticalDistanceFromHome);
            if (refreshClearance)
            {
                RefreshObstacleAvoidance();
                nextFlightClearanceAt = Time.time + FlightClearanceUpdateInterval;
            }

            desiredFlightPosition += cachedObstacleAvoidance;
        }

        private Transform GetNextValidWaypoint()
        {
            for (int checkedCount = 0; checkedCount < flightWaypoints.Length; checkedCount++)
            {
                Transform waypoint = flightWaypoints[waypointIndex % flightWaypoints.Length];
                if (waypoint != null)
                {
                    return waypoint;
                }

                waypointIndex = (waypointIndex + 1) % flightWaypoints.Length;
            }

            return null;
        }

        private void RefreshGroundClearance()
        {
            Vector3 probeOrigin = desiredFlightPosition + Vector3.up * maximumVerticalDistanceFromHome;
            float probeDistance = maximumVerticalDistanceFromHome * 2f + minimumGroundClearance;
            int hitCount = Physics.RaycastNonAlloc(
                probeOrigin,
                Vector3.down,
                groundClearanceHits,
                probeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            bool foundGround = false;
            RaycastHit nearestGround = default;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = groundClearanceHits[index];
                if (hit.transform == null ||
                    AlphaEnemyEventUtility.IsSameHierarchy(hit.transform, transform) ||
                    AlphaEnemyEventUtility.IsSameHierarchy(hit.transform, target) ||
                    hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearestGround = hit;
                foundGround = true;
            }

            cachedMinimumGroundHeight = foundGround
                ? nearestGround.point.y + minimumGroundClearance
                : float.NegativeInfinity;
        }

        private void RefreshObstacleAvoidance()
        {
            cachedObstacleAvoidance = Vector3.zero;
            Vector3 toDestination = desiredFlightPosition - transform.position;
            float distance = toDestination.magnitude;
            if (distance <= 0.01f)
            {
                return;
            }

            if (Physics.SphereCast(
                    transform.position,
                    obstacleProbeRadius,
                    toDestination / distance,
                    out RaycastHit hit,
                    Mathf.Min(distance, obstacleProbeDistance),
                    obstacleMask,
                    QueryTriggerInteraction.Ignore) &&
                hit.transform != null &&
                !AlphaEnemyEventUtility.IsSameHierarchy(hit.transform, transform) &&
                !AlphaEnemyEventUtility.IsSameHierarchy(hit.transform, target))
            {
                Vector3 lateral = Vector3.Cross(Vector3.up, hit.normal).normalized;
                if (lateral.sqrMagnitude <= 0.01f)
                {
                    lateral = transform.right;
                }

                cachedObstacleAvoidance = (lateral + Vector3.up).normalized * obstacleProbeDistance;
            }
        }

        private void ResetFlightClearanceCache()
        {
            cachedMinimumGroundHeight = float.NegativeInfinity;
            cachedObstacleAvoidance = Vector3.zero;
            nextFlightClearanceAt = Time.time;
        }

        private void MoveWithoutDynamicBody(float deltaTime)
        {
            Vector3 previousPosition = transform.position;
            transform.position = Vector3.MoveTowards(
                transform.position,
                desiredFlightPosition,
                scaledFlightSpeed * deltaTime);
            RotateTowardTargetOrVelocity(transform.position - previousPosition, deltaTime);
        }

        private void RotateTowardTargetOrVelocity(Vector3 velocity, float deltaTime)
        {
            Vector3 lookDirection = target != null
                ? target.position - transform.position
                : velocity;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            Quaternion rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, 360f * deltaTime);
            if (flightBody != null && !flightBody.isKinematic)
            {
                flightBody.MoveRotation(rotation);
            }
            else
            {
                transform.rotation = rotation;
            }
        }

        protected virtual void TickCombat()
        {
            TryCastMagic();
            TrySummonMinion();
        }

        protected virtual void TryCastMagic()
        {
            TryCastProjectileVolley(
                projectileCount: 1,
                spreadDegrees: 0f,
                homeOnTarget: true,
                damageMultiplier: 1f,
                speedMultiplier: 1f);
        }

        protected bool TryCastProjectileVolley(
            int projectileCount,
            float spreadDegrees,
            bool homeOnTarget,
            float damageMultiplier,
            float speedMultiplier)
        {
            if (magicProjectilePrefab == null || Time.time < nextMagicAt)
            {
                return false;
            }

            Transform attackTarget = ResolveAttackTargetForCast();
            if (attackTarget == null)
            {
                return false;
            }

            Transform spawn = magicSpawnPoint != null ? magicSpawnPoint : transform;
            Vector3 direction = attackTarget.position - spawn.position;
            Vector3 baseDirection = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : spawn.forward;
            int count = Mathf.Max(1, projectileCount);
            float clampedDamageMultiplier = Mathf.Max(0f, damageMultiplier);
            int projectileDamage = Mathf.Max(0, Mathf.RoundToInt(
                scaledMagicDamage * clampedDamageMultiplier));
            for (int index = 0; index < count; index++)
            {
                float normalizedIndex = count <= 1
                    ? 0f
                    : index / (float)(count - 1) - 0.5f;
                float angle = normalizedIndex * Mathf.Max(0f, spreadDegrees);
                Vector3 projectileDirection = Quaternion.AngleAxis(
                    angle,
                    Vector3.up) * baseDirection;
                Quaternion rotation = Quaternion.LookRotation(
                    projectileDirection,
                    Vector3.up);
                GameObject projectile = Instantiate(
                    magicProjectilePrefab,
                    spawn.position,
                    rotation);
                AlphaEnemyProjectile alphaProjectile =
                    projectile.GetComponent<AlphaEnemyProjectile>();
                if (alphaProjectile != null)
                {
                    alphaProjectile.ConfigureDirectional(
                        gameObject,
                        attackTarget,
                        projectileDamage,
                        projectileDirection,
                        projectileSpeedScalar * Mathf.Max(0.01f, speedMultiplier),
                        homeOnTarget);
                }
                else
                {
                    global::Damage legacyDamage =
                        projectile.GetComponent<global::Damage>();
                    if (legacyDamage != null)
                    {
                        float multiplier = baseMagicDamage > 0
                            ? projectileDamage / (float)baseMagicDamage
                            : 0f;
                        legacyDamage.SetDamageMultiplier(multiplier);
                    }
                    else if (!projectileWarningIssued)
                    {
                        projectileWarningIssued = true;
                        Debug.LogWarning(
                            $"{name}: configured magic projectile has neither AlphaEnemyProjectile nor Damage; it will not receive witch damage scaling.",
                            this);
                    }
                }
            }

            nextMagicAt = Time.time + magicCooldown;
            if (alternateAttackTargets && secondaryAttackTarget != null)
            {
                useSecondaryTargetNext = attackTarget != secondaryAttackTarget;
            }
            TriggerAnimator(castTrigger);
            PlayClip(castClip);
            return true;
        }

        protected Transform ResolveAttackTargetForCast()
        {
            Transform preferred = alternateAttackTargets &&
                                  useSecondaryTargetNext &&
                                  secondaryAttackTarget != null
                ? secondaryAttackTarget
                : target;
            Transform fallback = preferred == target
                ? secondaryAttackTarget
                : target;

            if (IsAttackTargetViable(preferred))
            {
                return preferred;
            }

            return fallback != preferred && IsAttackTargetViable(fallback)
                ? fallback
                : null;
        }

        private bool IsAttackTargetViable(Transform candidate)
        {
            return candidate != null &&
                   Vector3.Distance(transform.position, candidate.position) <=
                   magicRange &&
                   HasAttackLine(candidate);
        }

        private bool HasAttackLine(Transform attackTarget)
        {
            if (attackTarget == null)
            {
                return false;
            }

            Vector3 origin = magicSpawnPoint != null ? magicSpawnPoint.position : transform.position;
            Vector3 toTarget = attackTarget.position - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f)
            {
                return true;
            }

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                toTarget / distance,
                attackLineHits,
                distance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            Transform nearestTransform = null;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = attackLineHits[index];
                if (hit.transform == null || AlphaEnemyEventUtility.IsSameHierarchy(hit.transform, transform))
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestTransform = hit.transform;
                }
            }

            return nearestTransform == null ||
                   AlphaEnemyEventUtility.IsSameHierarchy(
                       nearestTransform,
                       attackTarget);
        }

        protected bool TrySummonMinion()
        {
            if (!SupportsSummoning || Time.time < nextSummonAt || activeMinions.Count >= maximumActiveMinions ||
                minionPrefabs == null || minionPrefabs.Length == 0 ||
                minionSpawnPoints == null || minionSpawnPoints.Length == 0)
            {
                return false;
            }

            GameObject prefab = GetRandomConfiguredMinion();
            Transform spawnPoint = GetNextConfiguredSpawnPoint();
            if (prefab == null || spawnPoint == null)
            {
                nextSummonAt = Time.time + summonCooldown;
                if (prefab == null)
                {
                    Debug.LogWarning(
                        $"{name}: Witch summoning requires an exact regular " +
                        "Boar prefab. Root Boar, Juggernaut, and legacy hog " +
                        "prefabs are rejected.",
                        this);
                }
                return false;
            }

            if (!CampaignSafetyEnemyRuntimeAdapter.TryValidatePrefab(
                    prefab,
                    out Component authoredController,
                    out NavMeshAgent authoredAgent,
                    out _,
                    out string prefabError) ||
                authoredController.GetType() !=
                    typeof(global::BoarBruteAI))
            {
                nextSummonAt = Time.time + summonCooldown;
                Debug.LogWarning(
                    $"{name}: rejected a Witch minion outside the exact " +
                    $"regular Boar contract. {prefabError}",
                    this);
                return false;
            }

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = authoredAgent.agentTypeID,
                areaMask = authoredAgent.areaMask
            };
            if (!NavMesh.SamplePosition(
                    spawnPoint.position,
                    out NavMeshHit groundHit,
                    minionGroundSampleRadius,
                    filter))
            {
                nextSummonAt = Time.time + summonCooldown;
                Debug.LogWarning(
                    $"{name}: no compatible NavMesh was found within {minionGroundSampleRadius:0.#}m of minion spawn '{spawnPoint.name}'. Summon skipped.",
                    this);
                return false;
            }

            Quaternion groundRotation = Quaternion.Euler(
                0f,
                spawnPoint.eulerAngles.y,
                0f);
            GameObject minion = Instantiate(
                prefab,
                groundHit.position,
                groundRotation);

            if (!CampaignSafetyEnemyRuntimeAdapter.TryPrepare(
                    minion,
                    out string preparationError))
            {
                Destroy(minion);
                nextSummonAt = Time.time + summonCooldown;
                Debug.LogWarning(
                    $"{name}: the summoned regular Boar lost its campaign " +
                    $"runtime contract and was discarded. {preparationError}",
                    this);
                return false;
            }

            if (!CampaignSafetyEnemyRuntimeAdapter
                    .TryGetExactAllowedController(
                        minion,
                        out Component spawnedController,
                        out string controllerError) ||
                spawnedController.GetType() !=
                    typeof(global::BoarBruteAI))
            {
                Destroy(minion);
                nextSummonAt = Time.time + summonCooldown;
                Debug.LogWarning(
                    $"{name}: the summoned minion is not the exact regular " +
                    $"Boar and was discarded. {controllerError}",
                    this);
                return false;
            }

            if (!CampaignSafetyEnemyRuntimeAdapter.TryGetAgent(
                    minion,
                    out NavMeshAgent spawnedAgent,
                    out string agentError))
            {
                Destroy(minion);
                nextSummonAt = Time.time + summonCooldown;
                Debug.LogWarning(
                    $"{name}: the summoned regular Boar lost its NavMesh " +
                    $"contract and was discarded. {agentError}",
                    this);
                return false;
            }

            if (!spawnedAgent.isOnNavMesh &&
                (!spawnedAgent.Warp(groundHit.position) ||
                 !spawnedAgent.isOnNavMesh))
            {
                Destroy(minion);
                nextSummonAt = Time.time + summonCooldown;
                Debug.LogWarning(
                    $"{name}: the summoned minion could not attach to its authored NavMesh and was discarded.",
                    this);
                return false;
            }

            Transform minionTarget = secondaryAttackTarget != null
                ? secondaryAttackTarget
                : target;
            Vector3 alertPosition = minionTarget != null
                ? minionTarget.position
                : transform.position;
            if (!CampaignSafetyEnemyRuntimeAdapter.TryInitialize(
                    spawnedController,
                    difficultyLevel,
                    out string initializationError))
            {
                Destroy(minion);
                nextSummonAt = Time.time + summonCooldown;
                Debug.LogWarning(
                    $"{name}: the summoned regular Boar could not be " +
                    $"initialized, so it was discarded. {initializationError}",
                    this);
                return false;
            }

            if (!CampaignSafetyEnemyRuntimeAdapter.TryAlert(
                    spawnedController,
                    alertPosition,
                    out string alertError))
            {
                Destroy(minion);
                nextSummonAt = Time.time + summonCooldown;
                Debug.LogWarning(
                    $"{name}: the summoned regular Boar could not be " +
                    $"alerted, so it was discarded. {alertError}",
                    this);
                return false;
            }

            activeMinions.Add(minion);
            spawnPointIndex = (spawnPointIndex + 1) % minionSpawnPoints.Length;
            nextSummonAt = Time.time + summonCooldown;
            TriggerAnimator(summonTrigger);
            PlayClip(summonClip);
            AlphaEnemyEventUtility.Invoke(onMinionSummoned, minion, this, nameof(onMinionSummoned));
            AlphaEnemyEventUtility.Invoke(MinionSummoned, minion, this, nameof(MinionSummoned));
            return true;
        }

        private GameObject GetRandomConfiguredMinion()
        {
            int startIndex = UnityEngine.Random.Range(0, minionPrefabs.Length);
            for (int checkedCount = 0; checkedCount < minionPrefabs.Length; checkedCount++)
            {
                GameObject prefab = minionPrefabs[(startIndex + checkedCount) % minionPrefabs.Length];
                if (CampaignSafetyEnemyRuntimeAdapter.TryValidatePrefab(
                        prefab,
                        out Component controller,
                        out _,
                        out _,
                        out _) &&
                    controller.GetType() == typeof(global::BoarBruteAI))
                {
                    return prefab;
                }
            }

            return null;
        }

        private Transform GetNextConfiguredSpawnPoint()
        {
            for (int checkedCount = 0; checkedCount < minionSpawnPoints.Length; checkedCount++)
            {
                Transform spawnPoint = minionSpawnPoints[(spawnPointIndex + checkedCount) % minionSpawnPoints.Length];
                if (spawnPoint != null)
                {
                    return spawnPoint;
                }
            }

            return null;
        }

        private void RemoveMissingMinions()
        {
            for (int index = activeMinions.Count - 1; index >= 0; index--)
            {
                if (activeMinions[index] == null)
                {
                    activeMinions.RemoveAt(index);
                }
            }
        }

        private void BindRootFragments(bool resetFragments)
        {
            if (rootFragments == null)
            {
                return;
            }

            foreach (WitchRootFragment fragment in rootFragments)
            {
                if (fragment == null)
                {
                    continue;
                }

                fragment.SetOwner(this);
                if (resetFragments)
                {
                    fragment.gameObject.SetActive(true);
                    fragment.ResetFragment();
                }
            }
        }

        private void RefreshShieldState(bool announceBreak)
        {
            bool wasShielded = shieldActive;
            shieldActive = SupportsShield && HasLivingRootFragment();
            if (shieldVisual != null)
            {
                shieldVisual.SetActive(shieldActive);
            }

            if (announceBreak && wasShielded && !shieldActive)
            {
                PlayClip(shieldBreakClip);
                AlphaEnemyEventUtility.Invoke(onShieldBroken, this, nameof(onShieldBroken));
                AlphaEnemyEventUtility.Invoke(ShieldBroken, this, nameof(ShieldBroken));
            }
        }

        private bool HasLivingRootFragment()
        {
            if (rootFragments == null || rootFragments.Length == 0)
            {
                return false;
            }

            foreach (WitchRootFragment fragment in rootFragments)
            {
                if (fragment != null && !fragment.IsBroken)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveTargetIfNeeded()
        {
            if (target != null || !resolveTargetFromGameManager || global::gameManager.instance == null ||
                global::gameManager.instance.player == null)
            {
                return;
            }

            target = global::gameManager.instance.player.transform;
        }

        private void RecalculateScaledValues(bool restoreHealth)
        {
            float oldMaximum = Mathf.Max(1, scaledMaxHealth);
            float oldRatio = currentHealth > 0 ? currentHealth / oldMaximum : 1f;
            int levelOffset = Mathf.Max(0, difficultyLevel - 1);
            float variantHealth = variant switch
            {
                WitchVariant.Summoner => summonerHealthMultiplier,
                WitchVariant.Matriarch => matriarchHealthMultiplier,
                _ => shieldBearerHealthMultiplier
            };
            float variantDamage = variant == WitchVariant.Matriarch ? matriarchDamageMultiplier : 1f;
            // A serialized base value of one is the explicit one-hit testing
            // contract. Do not let difficulty or variant multipliers inflate
            // it; restoring a production value above one restores scaling.
            scaledMaxHealth = baseMaxHealth == 1
                ? 1
                : Mathf.Max(1, Mathf.RoundToInt(
                    baseMaxHealth * variantHealth * healthScalar *
                    (1f + healthPerLevel * levelOffset)));
            scaledMagicDamage = Mathf.Max(0, Mathf.RoundToInt(
                baseMagicDamage * variantDamage * damageScalar * (1f + damagePerLevel * levelOffset)));
            scaledFlightSpeed = Mathf.Max(0.01f,
                baseFlightSpeed * speedScalar * (1f + speedPerLevel * levelOffset));
            currentHealth = restoreHealth
                ? scaledMaxHealth
                : Mathf.Clamp(Mathf.RoundToInt(scaledMaxHealth * oldRatio), 1, scaledMaxHealth);
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            combatEnabled = false;
            currentHealth = 0;
            OnDying();
            shieldActive = false;
            if (shieldVisual != null)
            {
                shieldVisual.SetActive(false);
            }

            if (flightBody != null && !flightBody.isKinematic)
            {
                flightBody.linearVelocity = Vector3.zero;
            }

            SetCollidersEnabled(false);
            SetAnimatorFlying(false);
            TriggerAnimator(deathTrigger);
            PlayClip(deathClip);
            if (destroySpawnedMinionsOnDeath)
            {
                foreach (GameObject minion in activeMinions)
                {
                    if (minion != null)
                    {
                        Destroy(minion);
                    }
                }

                activeMinions.Clear();
            }
            SpawnConfiguredLoot();
            AlphaEnemyEventUtility.Invoke(onDied, this, this, nameof(onDied));
            AlphaEnemyEventUtility.Invoke(Died, this, this, nameof(Died));
            if (destroyOnDeath)
            {
                Destroy(gameObject, deathDestroyDelay);
            }
            else
            {
                enabled = false;
            }
        }

        protected virtual void OnEncounterPrepared() { }

        protected virtual void OnHealthChanged() { }

        protected virtual void OnDying() { }

        private void SpawnConfiguredLoot()
        {
            if (lootPrefabs == null || lootPrefabs.Length == 0)
            {
                return;
            }

            Vector3 origin = lootDropPoint != null ? lootDropPoint.position : transform.position;
            foreach (GameObject lootPrefab in lootPrefabs)
            {
                if (lootPrefab == null)
                {
                    continue;
                }

                Vector2 scatter = UnityEngine.Random.insideUnitCircle * lootScatterRadius;
                Instantiate(lootPrefab, origin + new Vector3(scatter.x, 0f, scatter.y), Quaternion.identity);
            }
        }

        private void CacheCollidersIfNeeded()
        {
            if (combatColliders == null || combatColliders.Length == 0)
            {
                combatColliders = GetComponentsInChildren<Collider>(true);
            }
        }

        private void SetCollidersEnabled(bool enabledValue)
        {
            CacheCollidersIfNeeded();
            foreach (Collider combatCollider in combatColliders)
            {
                if (combatCollider != null)
                {
                    combatCollider.enabled = enabledValue;
                }
            }
        }

        private void SetAnimatorFlying(bool value)
        {
            if (animator != null && !string.IsNullOrWhiteSpace(flyingParameter))
            {
                animator.SetBool(flyingParameter, value);
            }
        }

        private void TriggerAnimator(string triggerName)
        {
            if (animator != null && !string.IsNullOrWhiteSpace(triggerName))
            {
                animator.SetTrigger(triggerName);
            }
        }

        private void PlayClip(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                StopAmbientIfPlaying();
                audioSource.PlayOneShot(clip);
                ScheduleNextAmbient();
            }
        }

        private void TickAmbientAudio()
        {
            if (ambientClipPlaying && Time.time >= ambientEndsAt)
                ambientClipPlaying = false;

            if (Time.time < nextAmbientAt || audioSource == null ||
                audioSource.isPlaying || ConfiguredAmbientClipCount == 0)
            {
                return;
            }

            AudioClip selected = null;
            int startIndex = UnityEngine.Random.Range(
                0,
                ambientClips.Length);
            for (int checkedCount = 0;
                 checkedCount < ambientClips.Length;
                 checkedCount++)
            {
                AudioClip candidate = ambientClips[
                    (startIndex + checkedCount) % ambientClips.Length];
                if (candidate != null)
                {
                    selected = candidate;
                    break;
                }
            }

            if (selected == null)
            {
                ScheduleNextAmbient();
                return;
            }

            audioSource.PlayOneShot(selected);
            ambientClipPlaying = true;
            ambientEndsAt = Time.time + selected.length /
                Mathf.Max(0.01f, Mathf.Abs(audioSource.pitch));
            ScheduleNextAmbient();
        }

        private void ScheduleNextAmbient()
        {
            float minimum = Mathf.Max(0.1f, ambientIntervalRange.x);
            float maximum = Mathf.Max(minimum, ambientIntervalRange.y);
            nextAmbientAt = Time.time + UnityEngine.Random.Range(
                minimum,
                maximum);
        }

        private void StopAmbientIfPlaying()
        {
            if (!ambientClipPlaying)
                return;

            if (audioSource != null)
                audioSource.Stop();
            ambientClipPlaying = false;
            ambientEndsAt = 0f;
        }

        protected virtual void OnValidate()
        {
            baseMaxHealth = Mathf.Max(1, baseMaxHealth);
            baseMagicDamage = Mathf.Max(0, baseMagicDamage);
            deathDestroyDelay = Mathf.Max(0f, deathDestroyDelay);
            baseFlightSpeed = Mathf.Max(0.01f, baseFlightSpeed);
            attackOrbitRadius = Mathf.Max(0.1f, attackOrbitRadius);
            orbitDegreesPerSecond = Mathf.Max(0f, orbitDegreesPerSecond);
            hoverHeight = Mathf.Max(0f, hoverHeight);
            minimumGroundClearance = Mathf.Max(0f, minimumGroundClearance);
            maximumVerticalDistanceFromHome = Mathf.Max(1f, maximumVerticalDistanceFromHome);
            waypointTolerance = Mathf.Max(0.05f, waypointTolerance);
            obstacleProbeRadius = Mathf.Max(0.05f, obstacleProbeRadius);
            obstacleProbeDistance = Mathf.Max(0.1f, obstacleProbeDistance);
            magicRange = Mathf.Max(0.1f, magicRange);
            magicCooldown = Mathf.Max(0.05f, magicCooldown);
            projectileSpeedScalar = Mathf.Max(0.01f, projectileSpeedScalar);
            maximumActiveMinions = Mathf.Max(1, maximumActiveMinions);
            summonCooldown = Mathf.Max(0.1f, summonCooldown);
            minionGroundSampleRadius = Mathf.Max(0.5f, minionGroundSampleRadius);
            shieldBearerHealthMultiplier = Mathf.Max(0.01f, shieldBearerHealthMultiplier);
            summonerHealthMultiplier = Mathf.Max(0.01f, summonerHealthMultiplier);
            matriarchHealthMultiplier = Mathf.Max(0.01f, matriarchHealthMultiplier);
            matriarchDamageMultiplier = Mathf.Max(0f, matriarchDamageMultiplier);
            difficultyLevel = Mathf.Max(1, difficultyLevel);
            healthPerLevel = Mathf.Max(0f, healthPerLevel);
            damagePerLevel = Mathf.Max(0f, damagePerLevel);
            speedPerLevel = Mathf.Max(0f, speedPerLevel);
            healthScalar = Mathf.Max(0.01f, healthScalar);
            damageScalar = Mathf.Max(0f, damageScalar);
            speedScalar = Mathf.Max(0.01f, speedScalar);
            lootScatterRadius = Mathf.Max(0f, lootScatterRadius);
            ambientClips ??= Array.Empty<AudioClip>();
            float minimumAmbientInterval = Mathf.Max(
                0.1f,
                ambientIntervalRange.x);
            ambientIntervalRange = new Vector2(
                minimumAmbientInterval,
                Mathf.Max(
                    minimumAmbientInterval,
                    ambientIntervalRange.y));
        }
    }
}
