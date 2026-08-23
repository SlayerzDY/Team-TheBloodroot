using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Bloodroot.Features.AlphaEnemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class WereBoarController : MonoBehaviour, global::IDamage
    {
        [Header("Required Runtime References")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Transform target;
        [SerializeField] private Transform eyePoint;
        [SerializeField] private bool resolveTargetFromGameManager = true;

        [Header("Vitals")]
        [SerializeField, Min(1)] private int baseMaxHealth = 1;
        [SerializeField, Min(0)] private int baseRushDamage = 45;
        [SerializeField, Min(0f)] private float deathDestroyDelay = 4f;

        [Header("Perception")]
        [SerializeField, Min(0.1f)] private float sightRange = 32f;
        [SerializeField, Range(1f, 360f)] private float fieldOfView = 115f;
        [SerializeField] private Vector3 targetAimOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private LayerMask lineOfSightMask = ~0;
        [SerializeField, Min(0f)] private float sightMemoryDuration = 2.5f;
        [SerializeField, Min(0.1f)] private float scentRange = 20f;
        [SerializeField, Min(0.05f)] private float scentUpdateInterval = 1.25f;
        [SerializeField, Range(1, 3)] private int minimumScentUpdates = 1;
        [SerializeField, Range(1, 3)] private int maximumScentUpdates = 3;

        [Header("Patrol")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField, Min(0.5f)] private float randomPatrolRadius = 18f;
        [SerializeField, Min(0.05f)] private float destinationTolerance = 1.2f;
        [SerializeField, Min(0f)] private float patrolPause = 1.25f;
        [SerializeField, Min(0.1f)] private float investigateWait = 2f;
        [SerializeField, Min(0.5f)] private float navMeshSampleRadius = 5f;

        [Header("Hunt Cycle")]
        [SerializeField, Min(0.1f)] private float circleDuration = 2.75f;
        [SerializeField, Min(0.1f)] private float circleRadius = 7f;
        [SerializeField, Min(0.05f)] private float circleDestinationRefresh = 0.45f;
        [SerializeField, Min(0.1f)] private float rushThroughDistance = 6f;
        [SerializeField, Min(0.1f)] private float rushDamageRadius = 1.65f;
        [SerializeField, Min(0.1f)] private float maximumRushDuration = 3.5f;
        [SerializeField, Min(0.1f)] private float retreatDistance = 9f;
        [SerializeField, Min(0.1f)] private float retreatTimeout = 3f;
        [SerializeField, Min(0.1f)] private float reEntryTimeout = 4f;

        [Header("Base Movement")]
        [SerializeField, Min(0.01f)] private float patrolSpeed = 3.25f;
        [SerializeField, Min(0.01f)] private float circleSpeed = 4.5f;
        [SerializeField, Min(0.01f)] private float rushSpeed = 10.5f;
        [SerializeField, Min(0.01f)] private float retreatSpeed = 6.5f;

        [Header("Difficulty Scaling")]
        [SerializeField, Min(1)] private int difficultyLevel = 1;
        [SerializeField, Min(0f)] private float healthPerLevel = 0.15f;
        [SerializeField, Min(0f)] private float damagePerLevel = 0.1f;
        [SerializeField, Min(0f)] private float speedPerLevel = 0.025f;
        [SerializeField, Min(0.01f)] private float healthScalar = 1f;
        [SerializeField, Min(0f)] private float damageScalar = 1f;
        [SerializeField, Min(0.01f)] private float speedScalar = 1f;

        [Header("Authored Presentation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string stateParameter = "State";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string deathTrigger = "Death";
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip alertClip;
        [SerializeField] private AudioClip rushClip;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip deathClip;
        [SerializeField] private Collider[] combatColliders;

        [Header("Configured Loot Hooks")]
        [SerializeField] private GameObject[] lootPrefabs;
        [SerializeField] private Transform lootDropPoint;
        [SerializeField, Min(0f)] private float lootScatterRadius = 0.75f;

        [Header("Authored Events")]
        [SerializeField] private WereBoarStateEvent onStateChanged = new WereBoarStateEvent();
        [SerializeField] private WereBoarControllerEvent onDamaged = new WereBoarControllerEvent();
        [SerializeField] private WereBoarControllerEvent onDied = new WereBoarControllerEvent();
        [SerializeField] private UnityEvent onRushStarted = new UnityEvent();

        private readonly RaycastHit[] lineOfSightHits = new RaycastHit[PhysicsHitBufferCapacity];
        private WereBoarState state = WereBoarState.Patrol;
        private Vector3 homePosition;
        private Vector3 lastKnownTargetPosition;
        private bool hasLastKnownTargetPosition;
        private bool couldSeeTargetLastFrame;
        private bool lineOfSightLostSinceAcquisition;
        private float lastSeenTime = float.NegativeInfinity;
        private float nextPerceptionAt;
        private float nextScentUpdateAt;
        private int scentUpdatesRemaining;
        private int scentUpdatesCommitted;
        private float stateDeadline;
        private float nextDestinationAt;
        private int patrolIndex;
        private int circleDirection = 1;
        private bool rushVictimDamaged;
        private bool navMeshWarningIssued;
        private bool targetWarningIssued;
        private bool isDead;
        private int currentHealth;
        private int scaledMaxHealth;
        private int scaledRushDamage;
        private float scaledSpeedMultiplier = 1f;
        private bool cachedCanSeeTarget;
        private readonly Dictionary<int, AnimatorControllerParameterType> animatorParameters = new();
        private Animator cachedParameterAnimator;
        private RuntimeAnimatorController cachedParameterController;

        private const int PhysicsHitBufferCapacity = 64;
        private const float PerceptionUpdateInterval = 0.1f;

        public event Action<WereBoarState> StateChanged;
        public event Action<WereBoarController> Damaged;
        public event Action<WereBoarController> Died;

        public WereBoarState State => state;
        public Transform Target => target;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => scaledMaxHealth;
        public int RushDamage => scaledRushDamage;
        public bool IsDead => isDead;
        public bool HasLineOfSight => cachedCanSeeTarget;
        public bool HasLastKnownTargetPosition => hasLastKnownTargetPosition;
        public Vector3 LastKnownTargetPosition => lastKnownTargetPosition;
        public int ScentUpdatesRemaining => scentUpdatesRemaining;
        public int ScentUpdatesCommitted => scentUpdatesCommitted;

        private void Awake()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            homePosition = transform.position;
            CacheCollidersIfNeeded();
            RecalculateScaledValues(true);
            ApplyStateSpeed();
        }

        private void Start()
        {
            ResolveTargetIfNeeded();
            nextDestinationAt = Time.time;
            nextPerceptionAt = Time.time;
        }

        private void Update()
        {
            if (isDead)
            {
                UpdateAnimatorSpeed(false);
                return;
            }

            ResolveTargetIfNeeded();
            if (target == null)
            {
                if (!targetWarningIssued)
                {
                    targetWarningIssued = true;

                }

                FailClosed();
                return;
            }

            targetWarningIssued = false;
            bool canNavigate = CanNavigate();
            if (!canNavigate && !navMeshWarningIssued)
            {
                navMeshWarningIssued = true;

            }

            if (!canNavigate)
            {
                FailClosed();
                return;
            }

            navMeshWarningIssued = false;

            bool canSeeTarget = cachedCanSeeTarget;
            if (Time.time >= nextPerceptionAt)
            {
                canSeeTarget = CanSeeTarget();
                cachedCanSeeTarget = canSeeTarget;
                nextPerceptionAt = Time.time + PerceptionUpdateInterval;
                UpdatePerception(canSeeTarget);
            }

            if (!canSeeTarget)
            {
                TryUpdateScentTrail();
            }

            switch (state)
            {
                case WereBoarState.Patrol:
                    UpdatePatrol();
                    break;
                case WereBoarState.Investigate:
                    UpdateInvestigate();
                    break;
                case WereBoarState.Circle:
                    UpdateCircle(canSeeTarget);
                    break;
                case WereBoarState.Rush:
                    UpdateRush();
                    break;
                case WereBoarState.Retreat:
                    UpdateRetreat();
                    break;
                case WereBoarState.ReEntry:
                    UpdateReEntry();
                    break;
            }

            UpdateAnimatorSpeed(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (state == WereBoarState.Rush && CanNavigate() && IsTargetCollider(other))
            {
                DamageRushTarget();
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            couldSeeTargetLastFrame = false;
            cachedCanSeeTarget = false;
            lineOfSightLostSinceAcquisition = false;
            hasLastKnownTargetPosition = false;
            scentUpdatesRemaining = 0;
            scentUpdatesCommitted = 0;
            nextPerceptionAt = float.NegativeInfinity;
            if (newTarget == null)
            {
                FailClosed();
            }
        }

        public void AlertToPosition(Vector3 position)
        {
            if (isDead)
            {
                return;
            }

            lastKnownTargetPosition = position;
            hasLastKnownTargetPosition = true;
            lastSeenTime = Time.time;
            EnterInvestigate();
        }

        public void ApplyDifficulty(
            int level,
            float healthMultiplier,
            float damageMultiplier,
            float movementSpeedMultiplier,
            bool restoreHealth = true)
        {
            difficultyLevel = Mathf.Max(1, level);
            healthScalar = Mathf.Max(0.01f, healthMultiplier);
            damageScalar = Mathf.Max(0f, damageMultiplier);
            speedScalar = Mathf.Max(0.01f, movementSpeedMultiplier);
            RecalculateScaledValues(restoreHealth);
            ApplyStateSpeed();
        }

        public void TakeDamage(int amount)
        {
            if (isDead || amount <= 0)
            {
                return;
            }

            currentHealth = Mathf.Max(0, currentHealth - amount);
            TriggerAnimator(hitTrigger, "Hit1", "Hit");
            PlayClip(hitClip);
            AlphaEnemyEventUtility.Invoke(onDamaged, this, this, nameof(onDamaged));
            AlphaEnemyEventUtility.Invoke(Damaged, this, this, nameof(Damaged));
            if (currentHealth == 0)
            {
                Die();
            }
        }

        public void onDeath(bool dead)
        {
            if (dead)
            {
                Die();
            }
        }

        private void UpdatePerception(bool canSeeTarget)
        {
            cachedCanSeeTarget = canSeeTarget;
            if (canSeeTarget)
            {
                lastKnownTargetPosition = target.position;
                hasLastKnownTargetPosition = true;
                lastSeenTime = Time.time;
                if (!couldSeeTargetLastFrame)
                {
                    scentUpdatesRemaining = UnityEngine.Random.Range(minimumScentUpdates, maximumScentUpdates + 1);
                    scentUpdatesCommitted = 0;
                    PlayClip(alertClip);
                }

                lineOfSightLostSinceAcquisition = false;

                if (state == WereBoarState.Patrol || state == WereBoarState.Investigate)
                {
                    EnterCircle();
                }
            }
            else
            {
                if (couldSeeTargetLastFrame)
                {
                    lineOfSightLostSinceAcquisition = true;
                    nextScentUpdateAt = Time.time + scentUpdateInterval;
                }
            }

            couldSeeTargetLastFrame = canSeeTarget;
        }

        private void TryUpdateScentTrail()
        {
            if (!CanUpdateScentTrail())
            {
                return;
            }

            Vector3 scentPosition = target.position;
            if (!TrySetDestination(scentPosition))
            {
                return;
            }

            TryCommitScentDestination(scentPosition, true);
        }

        private bool CanUpdateScentTrail()
        {
            return state == WereBoarState.Investigate &&
                   lineOfSightLostSinceAcquisition &&
                   !couldSeeTargetLastFrame &&
                   target != null &&
                   hasLastKnownTargetPosition &&
                   scentUpdatesRemaining > 0 &&
                   Time.time >= nextScentUpdateAt &&
                   Vector3.Distance(transform.position, target.position) <= scentRange;
        }

        private bool TryCommitScentDestination(Vector3 scentPosition, bool destinationAccepted)
        {
            if (!destinationAccepted || !CanUpdateScentTrail())
            {
                return false;
            }

            lastKnownTargetPosition = scentPosition;
            lastSeenTime = Time.time;
            scentUpdatesRemaining--;
            scentUpdatesCommitted++;
            nextScentUpdateAt = Time.time + scentUpdateInterval;
            stateDeadline = Time.time + investigateWait;
            return true;
        }

        private void UpdatePatrol()
        {
            if (Time.time < nextDestinationAt)
            {
                return;
            }

            if (!HasReachedDestination())
            {
                return;
            }

            if (TryChoosePatrolDestination())
            {
                nextDestinationAt = Time.time + patrolPause;
            }
        }

        private void UpdateInvestigate()
        {
            if (!hasLastKnownTargetPosition)
            {
                EnterPatrol();
                return;
            }

            if (HasReachedDestination())
            {
                if (Time.time >= stateDeadline)
                {
                    if (CanCommitAnotherScentUpdate())
                    {
                        stateDeadline = Mathf.Max(
                            Time.time + investigateWait,
                            nextScentUpdateAt + investigateWait);
                        return;
                    }

                    hasLastKnownTargetPosition = false;
                    EnterPatrol();
                }

                return;
            }

            if (Time.time - lastSeenTime > sightMemoryDuration + investigateWait && scentUpdatesRemaining <= 0)
            {
                hasLastKnownTargetPosition = false;
                EnterPatrol();
            }
        }

        private bool CanCommitAnotherScentUpdate()
        {
            if (!lineOfSightLostSinceAcquisition ||
                couldSeeTargetLastFrame ||
                target == null ||
                scentUpdatesRemaining <= 0 ||
                Vector3.Distance(transform.position, target.position) > scentRange ||
                !CanNavigate())
            {
                return false;
            }

            return NavMesh.SamplePosition(
                target.position,
                out _,
                navMeshSampleRadius,
                agent.areaMask);
        }

        private void UpdateCircle(bool canSeeTarget)
        {
            if (!canSeeTarget && Time.time - lastSeenTime > sightMemoryDuration)
            {
                EnterInvestigate();
                return;
            }

            if (Time.time >= stateDeadline)
            {
                EnterRush();
                return;
            }

            if (Time.time >= nextDestinationAt)
            {
                SetCircleDestination();
                nextDestinationAt = Time.time + circleDestinationRefresh;
            }
        }

        private void UpdateRush()
        {
            if (!rushVictimDamaged && target != null &&
                Vector3.Distance(transform.position, target.position) <= rushDamageRadius)
            {
                DamageRushTarget();
            }

            if (HasReachedDestination() || Time.time >= stateDeadline)
            {
                EnterRetreat();
            }
        }

        private void UpdateRetreat()
        {
            if (HasReachedDestination() || Time.time >= stateDeadline)
            {
                EnterReEntry();
            }
        }

        private void UpdateReEntry()
        {
            if (HasReachedDestination() || Time.time >= stateDeadline)
            {
                if (target != null && (cachedCanSeeTarget || Time.time - lastSeenTime <= sightMemoryDuration))
                {
                    EnterCircle();
                }
                else
                {
                    EnterInvestigate();
                }
            }
        }

        private void EnterPatrol()
        {
            SetState(WereBoarState.Patrol);
            nextDestinationAt = Time.time;
        }

        private void EnterInvestigate()
        {
            if (!hasLastKnownTargetPosition)
            {
                EnterPatrol();
                return;
            }

            SetState(WereBoarState.Investigate);
            TrySetDestination(lastKnownTargetPosition);
            stateDeadline = Time.time + investigateWait;
        }

        private void EnterCircle()
        {
            SetState(WereBoarState.Circle);
            circleDirection = UnityEngine.Random.value < 0.5f ? -1 : 1;
            stateDeadline = Time.time + circleDuration;
            nextDestinationAt = Time.time;
        }

        private void EnterRush()
        {
            if (!TryGetThreatPosition(out Vector3 threatPosition))
            {
                EnterPatrol();
                return;
            }

            Vector3 rushDestination = CalculateRushDestination(threatPosition);
            SetState(WereBoarState.Rush);
            rushVictimDamaged = false;
            stateDeadline = Time.time + maximumRushDuration;
            TrySetDestination(rushDestination);
            TriggerAnimator("Attack1", "Attack");
            PlayClip(rushClip);
            AlphaEnemyEventUtility.Invoke(onRushStarted, this, nameof(onRushStarted));
        }

        private void EnterRetreat()
        {
            Vector3 threatPosition = TryGetThreatPosition(out Vector3 trackedThreatPosition)
                ? trackedThreatPosition
                : homePosition;

            SetState(WereBoarState.Retreat);
            stateDeadline = Time.time + retreatTimeout;
            TrySetDestination(CalculateRetreatDestination(threatPosition));
        }

        private void EnterReEntry()
        {
            if (!TryGetThreatPosition(out Vector3 threatPosition))
            {
                EnterPatrol();
                return;
            }

            SetState(WereBoarState.ReEntry);
            stateDeadline = Time.time + reEntryTimeout;
            TrySetDestination(CalculateReEntryDestination(threatPosition));
        }

        private Vector3 CalculateRushDestination(Vector3 threatPosition)
        {
            Vector3 throughDirection = threatPosition - transform.position;
            throughDirection.y = 0f;
            if (throughDirection.sqrMagnitude <= 0.001f)
            {
                throughDirection = transform.forward;
            }

            return threatPosition + throughDirection.normalized * rushThroughDistance;
        }

        private Vector3 CalculateRetreatDestination(Vector3 threatPosition)
        {
            Vector3 away = transform.position - threatPosition;
            away.y = 0f;
            if (away.sqrMagnitude <= 0.001f)
            {
                away = -transform.forward;
            }

            return transform.position + away.normalized * retreatDistance;
        }

        private Vector3 CalculateReEntryDestination(Vector3 threatPosition)
        {
            Vector3 radial = transform.position - threatPosition;
            radial.y = 0f;
            if (radial.sqrMagnitude <= 0.001f)
            {
                radial = transform.right;
            }

            Vector3 tangent = Vector3.Cross(Vector3.up, radial.normalized) * circleDirection;
            return threatPosition + (radial.normalized + tangent).normalized * circleRadius;
        }

        private void SetState(WereBoarState newState)
        {
            if (state == newState)
            {
                ApplyStateSpeed();
                return;
            }

            state = newState;
            ApplyStateSpeed();
            TrySetAnimatorInteger(stateParameter, (int)state);

            AlphaEnemyEventUtility.Invoke(onStateChanged, state, this, nameof(onStateChanged));
            AlphaEnemyEventUtility.Invoke(StateChanged, state, this, nameof(StateChanged));
        }

        private void SetCircleDestination()
        {
            if (!TryGetThreatPosition(out Vector3 threatPosition))
            {
                return;
            }

            Vector3 radial = transform.position - threatPosition;
            radial.y = 0f;
            if (radial.sqrMagnitude <= 0.001f)
            {
                radial = transform.right;
            }

            Vector3 tangent = Vector3.Cross(Vector3.up, radial.normalized) * circleDirection;
            Vector3 desired = threatPosition + (radial.normalized + tangent * 0.9f).normalized * circleRadius;
            TrySetDestination(desired);
        }

        private bool TryChoosePatrolDestination()
        {
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                for (int checkedCount = 0; checkedCount < patrolPoints.Length; checkedCount++)
                {
                    Transform patrolPoint = patrolPoints[patrolIndex % patrolPoints.Length];
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                    if (patrolPoint != null && TrySetDestination(patrolPoint.position))
                    {
                        return true;
                    }
                }
            }

            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * randomPatrolRadius;
            return TrySetDestination(homePosition + new Vector3(randomOffset.x, 0f, randomOffset.y));
        }

        private bool TrySetDestination(Vector3 desiredPosition)
        {
            if (!CanNavigate())
            {
                return false;
            }

            if (!NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, navMeshSampleRadius, agent.areaMask))
            {
                return false;
            }

            agent.isStopped = false;
            return agent.SetDestination(hit.position);
        }

        private bool HasReachedDestination()
        {
            if (!CanNavigate() || agent.pathPending)
            {
                return false;
            }

            if (!agent.hasPath)
            {
                return true;
            }

            return agent.remainingDistance <= Mathf.Max(destinationTolerance, agent.stoppingDistance);
        }

        private bool CanNavigate()
        {
            return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        }

        private void FailClosed()
        {
            cachedCanSeeTarget = false;
            couldSeeTargetLastFrame = false;
            lineOfSightLostSinceAcquisition = false;
            hasLastKnownTargetPosition = false;
            scentUpdatesRemaining = 0;
            scentUpdatesCommitted = 0;
            rushVictimDamaged = true;

            if (CanNavigate())
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            if (!isDead && state != WereBoarState.Patrol)
            {
                SetState(WereBoarState.Patrol);
            }

            UpdateAnimatorSpeed(false);
        }

        private bool CanSeeTarget()
        {
            if (target == null)
            {
                return false;
            }

            Vector3 origin = eyePoint != null ? eyePoint.position : transform.position + Vector3.up;
            Vector3 destination = target.position + targetAimOffset;
            Vector3 toTarget = destination - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f || distance > sightRange)
            {
                return false;
            }

            Vector3 facing = eyePoint != null ? eyePoint.forward : transform.forward;
            if (Vector3.Angle(facing, toTarget) > fieldOfView * 0.5f)
            {
                return false;
            }

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                toTarget / distance,
                lineOfSightHits,
                distance,
                lineOfSightMask,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            Transform nearestTransform = null;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = lineOfSightHits[index];
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

            return nearestTransform == null || AlphaEnemyEventUtility.IsSameHierarchy(nearestTransform, target);
        }

        private bool TryGetThreatPosition(out Vector3 position)
        {
            if (target != null && couldSeeTargetLastFrame)
            {
                position = target.position;
                return true;
            }

            position = lastKnownTargetPosition;
            return hasLastKnownTargetPosition;
        }

        private bool IsTargetCollider(Collider other)
        {
            return other != null && target != null &&
                   AlphaEnemyEventUtility.IsSameHierarchy(other.transform, target);
        }

        private void DamageRushTarget()
        {
            if (state != WereBoarState.Rush || !CanNavigate() || rushVictimDamaged || target == null)
            {
                return;
            }

            global::IDamage receiver = AlphaEnemyEventUtility.FindDamageReceiver(target);
            if (receiver == null)
            {
                return;
            }

            rushVictimDamaged = true;
            if (scaledRushDamage > 0)
            {
                receiver.TakeDamage(scaledRushDamage);
            }
        }

        private void ResolveTargetIfNeeded()
        {
            if (target != null || !resolveTargetFromGameManager || global::gameManager.instance == null ||
                global::gameManager.instance.player == null)
            {
                return;
            }

            SetTarget(global::gameManager.instance.player.transform);
        }

        private void RecalculateScaledValues(bool restoreHealth)
        {
            float oldMaximum = Mathf.Max(1, scaledMaxHealth);
            float oldRatio = currentHealth > 0 ? currentHealth / oldMaximum : 1f;
            int levelOffset = Mathf.Max(0, difficultyLevel - 1);
            // Preserve the authored one-health beta test value even when a
            // difficulty scalar is supplied by a spawner.
            scaledMaxHealth = baseMaxHealth <= 1
                ? 1
                : Mathf.Max(1, Mathf.RoundToInt(
                    baseMaxHealth * healthScalar *
                    (1f + healthPerLevel * levelOffset)));
            scaledRushDamage = Mathf.Max(0, Mathf.RoundToInt(
                baseRushDamage * damageScalar * (1f + damagePerLevel * levelOffset)));
            scaledSpeedMultiplier = Mathf.Max(0.01f,
                speedScalar * (1f + speedPerLevel * levelOffset));
            currentHealth = restoreHealth
                ? scaledMaxHealth
                : Mathf.Clamp(Mathf.RoundToInt(scaledMaxHealth * oldRatio), 1, scaledMaxHealth);
        }

        private void ApplyStateSpeed()
        {
            if (agent == null)
            {
                return;
            }

            float baseSpeed = state switch
            {
                WereBoarState.Circle => circleSpeed,
                WereBoarState.Rush => rushSpeed,
                WereBoarState.Retreat => retreatSpeed,
                WereBoarState.ReEntry => retreatSpeed,
                _ => patrolSpeed
            };
            agent.speed = baseSpeed * scaledSpeedMultiplier;
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            currentHealth = 0;
            SetState(WereBoarState.Dead);
            if (CanNavigate())
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            SetCollidersEnabled(false);
            TriggerAnimator(deathTrigger, "Death1", "Death");
            UpdateAnimatorSpeed(false);
            PlayClip(deathClip);
            SpawnConfiguredLoot();
            AlphaEnemyEventUtility.Invoke(onDied, this, this, nameof(onDied));
            AlphaEnemyEventUtility.Invoke(Died, this, this, nameof(Died));
            if (Application.isPlaying)
            {
                Destroy(gameObject, deathDestroyDelay);
            }
        }

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

        private void SetCollidersEnabled(bool enabled)
        {
            CacheCollidersIfNeeded();
            foreach (Collider combatCollider in combatColliders)
            {
                if (combatCollider != null)
                {
                    combatCollider.enabled = enabled;
                }
            }
        }

        private void TriggerAnimator(params string[] triggerNames)
        {
            if (animator == null || triggerNames == null)
            {
                return;
            }

            RefreshAnimatorParameterCache();
            for (int index = 0; index < triggerNames.Length; index++)
            {
                string triggerName = triggerNames[index];
                if (!TryGetAnimatorParameter(triggerName, AnimatorControllerParameterType.Trigger, out int triggerHash))
                {
                    continue;
                }

                animator.SetTrigger(triggerHash);
                return;
            }
        }

        private void TrySetAnimatorInteger(string parameterName, int value)
        {
            if (animator == null)
            {
                return;
            }

            RefreshAnimatorParameterCache();
            if (TryGetAnimatorParameter(parameterName, AnimatorControllerParameterType.Int, out int parameterHash))
            {
                animator.SetInteger(parameterHash, value);
            }
        }

        private void UpdateAnimatorSpeed(bool canMove)
        {
            if (animator == null)
            {
                return;
            }

            RefreshAnimatorParameterCache();
            if (!TryGetAnimatorParameter("Speed", AnimatorControllerParameterType.Float, out int speedHash))
            {
                return;
            }

            float normalizedSpeed = 0f;
            if (canMove && CanNavigate() && !agent.isStopped)
            {
                normalizedSpeed = Mathf.Clamp01(
                    agent.velocity.magnitude / Mathf.Max(0.01f, agent.speed));
            }

            animator.SetFloat(speedHash, normalizedSpeed, 0.08f, Time.deltaTime);
        }

        private void RefreshAnimatorParameterCache()
        {
            RuntimeAnimatorController controller = animator != null
                ? animator.runtimeAnimatorController
                : null;
            if (animator == cachedParameterAnimator && controller == cachedParameterController)
            {
                return;
            }

            cachedParameterAnimator = animator;
            cachedParameterController = controller;
            animatorParameters.Clear();
            if (animator == null || controller == null)
            {
                return;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                animatorParameters[parameter.nameHash] = parameter.type;
            }
        }

        private bool TryGetAnimatorParameter(
            string parameterName,
            AnimatorControllerParameterType expectedType,
            out int parameterHash)
        {
            parameterHash = 0;
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            parameterHash = Animator.StringToHash(parameterName);
            return animatorParameters.TryGetValue(parameterHash, out AnimatorControllerParameterType actualType) &&
                   actualType == expectedType;
        }

        private void PlayClip(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void OnValidate()
        {
            baseMaxHealth = Mathf.Max(1, baseMaxHealth);
            baseRushDamage = Mathf.Max(0, baseRushDamage);
            deathDestroyDelay = Mathf.Max(0f, deathDestroyDelay);
            sightRange = Mathf.Max(0.1f, sightRange);
            fieldOfView = Mathf.Clamp(fieldOfView, 1f, 360f);
            sightMemoryDuration = Mathf.Max(0f, sightMemoryDuration);
            scentRange = Mathf.Max(0.1f, scentRange);
            scentUpdateInterval = Mathf.Max(0.05f, scentUpdateInterval);
            minimumScentUpdates = Mathf.Clamp(minimumScentUpdates, 1, 3);
            maximumScentUpdates = Mathf.Clamp(maximumScentUpdates, minimumScentUpdates, 3);
            randomPatrolRadius = Mathf.Max(0.5f, randomPatrolRadius);
            destinationTolerance = Mathf.Max(0.05f, destinationTolerance);
            patrolPause = Mathf.Max(0f, patrolPause);
            investigateWait = Mathf.Max(0.1f, investigateWait);
            navMeshSampleRadius = Mathf.Max(0.5f, navMeshSampleRadius);
            circleDuration = Mathf.Max(0.1f, circleDuration);
            circleRadius = Mathf.Max(0.1f, circleRadius);
            circleDestinationRefresh = Mathf.Max(0.05f, circleDestinationRefresh);
            rushThroughDistance = Mathf.Max(0.1f, rushThroughDistance);
            rushDamageRadius = Mathf.Max(0.1f, rushDamageRadius);
            maximumRushDuration = Mathf.Max(0.1f, maximumRushDuration);
            retreatDistance = Mathf.Max(0.1f, retreatDistance);
            retreatTimeout = Mathf.Max(0.1f, retreatTimeout);
            reEntryTimeout = Mathf.Max(0.1f, reEntryTimeout);
            patrolSpeed = Mathf.Max(0.01f, patrolSpeed);
            circleSpeed = Mathf.Max(0.01f, circleSpeed);
            rushSpeed = Mathf.Max(0.01f, rushSpeed);
            retreatSpeed = Mathf.Max(0.01f, retreatSpeed);
            difficultyLevel = Mathf.Max(1, difficultyLevel);
            healthPerLevel = Mathf.Max(0f, healthPerLevel);
            damagePerLevel = Mathf.Max(0f, damagePerLevel);
            speedPerLevel = Mathf.Max(0f, speedPerLevel);
            healthScalar = Mathf.Max(0.01f, healthScalar);
            damageScalar = Mathf.Max(0f, damageScalar);
            speedScalar = Mathf.Max(0.01f, speedScalar);
            lootScatterRadius = Mathf.Max(0f, lootScatterRadius);
        }
    }
}
