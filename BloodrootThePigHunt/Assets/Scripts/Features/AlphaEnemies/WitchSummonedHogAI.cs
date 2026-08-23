using System;
using UnityEngine;
using UnityEngine.AI;

namespace Bloodroot.Features.AlphaEnemies
{
    /// <summary>
    /// Lightweight, witch-owned meat-shield AI. It intentionally has no
    /// MobSpawner, waveManager, RegularHog, or production enemyAI dependency.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class WitchSummonedHogAI : MonoBehaviour, global::IDamage
    {
        [Header("Required Runtime References")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Transform target;
        [SerializeField] private bool resolveTargetFromGameManager = true;

        [Header("Vitals and Attack")]
        [SerializeField, Min(1)] private int baseMaxHealth = 110;
        [SerializeField, Min(0)] private int baseBiteDamage = 14;
        [SerializeField, Min(0.1f)] private float attackRange = 1.25f;
        [SerializeField, Min(0.05f)] private float attackCooldown = 1.1f;
        [SerializeField, Min(0f)] private float deathDestroyDelay = 1.5f;

        [Header("Movement")]
        [SerializeField, Min(0.01f)] private float baseMoveSpeed = 4.5f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.2f;
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 3f;

        [Header("Difficulty Scaling")]
        [SerializeField, Min(1)] private int difficultyLevel = 1;
        [SerializeField, Min(0f)] private float healthPerLevel = 0.12f;
        [SerializeField, Min(0f)] private float damagePerLevel = 0.08f;
        [SerializeField, Min(0f)] private float speedPerLevel = 0.02f;
        [SerializeField, Min(0.01f)] private float healthScalar = 1f;
        [SerializeField, Min(0f)] private float damageScalar = 1f;
        [SerializeField, Min(0.01f)] private float speedScalar = 1f;

        [Header("Authored Presentation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string biteTrigger = "Attack";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string deathTrigger = "Death";
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip biteClip;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip deathClip;
        [SerializeField] private Collider[] combatColliders;

        [Header("Authored Events")]
        [SerializeField] private WitchSummonedHogEvent onDied =
            new WitchSummonedHogEvent();

        private int currentHealth;
        private int scaledMaxHealth;
        private int scaledBiteDamage;
        private float scaledMoveSpeed;
        private float nextAttackAt;
        private float nextRepathAt;
        private bool isDead;
        private bool navMeshWarningIssued;

        public event Action<WitchSummonedHogAI> Died;

        public Transform Target => target;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => scaledMaxHealth;
        public int BiteDamage => scaledBiteDamage;
        public bool IsDead => isDead;

        private void Awake()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            CacheCollidersIfNeeded();
            RecalculateScaledValues(true);
        }

        private void Start()
        {
            ResolveTargetIfNeeded();
            nextRepathAt = Time.time;
        }

        private void Update()
        {
            if (isDead)
            {
                return;
            }

            ResolveTargetIfNeeded();
            if (target == null)
            {
                return;
            }

            if (!CanNavigate())
            {
                if (!navMeshWarningIssued)
                {
                    navMeshWarningIssued = true;

                }

                return;
            }

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance <= attackRange)
            {
                agent.isStopped = true;
                if (agent.hasPath)
                {
                    agent.ResetPath();
                }

                RotateTowardTarget();
                TryBiteTarget();
                return;
            }

            if (Time.time >= nextRepathAt)
            {
                agent.isStopped = false;
                if (NavMesh.SamplePosition(
                        target.position,
                        out NavMeshHit hit,
                        navMeshSampleRadius,
                        agent.areaMask))
                {
                    agent.SetDestination(hit.position);
                }

                nextRepathAt = Time.time + repathInterval;
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            nextRepathAt = float.NegativeInfinity;
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
        }

        public void TakeDamage(int amount)
        {
            if (isDead || amount <= 0)
            {
                return;
            }

            currentHealth = Mathf.Max(0, currentHealth - amount);
            TriggerAnimator(hitTrigger);
            PlayClip(hitClip);
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

        private void TryBiteTarget()
        {
            if (Time.time < nextAttackAt)
            {
                return;
            }

            global::IDamage receiver =
                AlphaEnemyEventUtility.FindDamageReceiver(target);
            if (receiver == null)
            {
                return;
            }

            nextAttackAt = Time.time + attackCooldown;
            if (scaledBiteDamage > 0)
            {
                receiver.TakeDamage(scaledBiteDamage);
            }

            TriggerAnimator(biteTrigger);
            PlayClip(biteClip);
        }

        private void RotateTowardTarget()
        {
            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(direction.normalized, Vector3.up),
                    agent.angularSpeed * Time.deltaTime);
            }
        }

        private bool CanNavigate()
        {
            return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        }

        private void ResolveTargetIfNeeded()
        {
            if (target != null || !resolveTargetFromGameManager ||
                global::gameManager.instance == null ||
                global::gameManager.instance.player == null)
            {
                return;
            }

            target = global::gameManager.instance.player.transform;
        }

        private void RecalculateScaledValues(bool restoreHealth)
        {
            float oldMaximum = Mathf.Max(1, scaledMaxHealth);
            float oldRatio = currentHealth > 0
                ? currentHealth / oldMaximum
                : 1f;
            int levelOffset = Mathf.Max(0, difficultyLevel - 1);
            scaledMaxHealth = Mathf.Max(1, Mathf.RoundToInt(
                baseMaxHealth * healthScalar *
                (1f + healthPerLevel * levelOffset)));
            scaledBiteDamage = Mathf.Max(0, Mathf.RoundToInt(
                baseBiteDamage * damageScalar *
                (1f + damagePerLevel * levelOffset)));
            scaledMoveSpeed = Mathf.Max(0.01f,
                baseMoveSpeed * speedScalar *
                (1f + speedPerLevel * levelOffset));
            currentHealth = restoreHealth
                ? scaledMaxHealth
                : Mathf.Clamp(
                    Mathf.RoundToInt(scaledMaxHealth * oldRatio),
                    1,
                    scaledMaxHealth);
            if (agent != null)
            {
                agent.speed = scaledMoveSpeed;
            }
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            currentHealth = 0;
            if (CanNavigate())
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            SetCollidersEnabled(false);
            TriggerAnimator(deathTrigger);
            PlayClip(deathClip);
            AlphaEnemyEventUtility.Invoke(onDied, this, this, nameof(onDied));
            AlphaEnemyEventUtility.Invoke(Died, this, this, nameof(Died));
            Destroy(gameObject, deathDestroyDelay);
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
                audioSource.PlayOneShot(clip);
            }
        }

        private void OnValidate()
        {
            baseMaxHealth = Mathf.Max(1, baseMaxHealth);
            baseBiteDamage = Mathf.Max(0, baseBiteDamage);
            attackRange = Mathf.Max(0.1f, attackRange);
            attackCooldown = Mathf.Max(0.05f, attackCooldown);
            deathDestroyDelay = Mathf.Max(0f, deathDestroyDelay);
            baseMoveSpeed = Mathf.Max(0.01f, baseMoveSpeed);
            repathInterval = Mathf.Max(0.05f, repathInterval);
            navMeshSampleRadius = Mathf.Max(0.1f, navMeshSampleRadius);
            difficultyLevel = Mathf.Max(1, difficultyLevel);
            healthPerLevel = Mathf.Max(0f, healthPerLevel);
            damagePerLevel = Mathf.Max(0f, damagePerLevel);
            speedPerLevel = Mathf.Max(0f, speedPerLevel);
            healthScalar = Mathf.Max(0.01f, healthScalar);
            damageScalar = Mathf.Max(0f, damageScalar);
            speedScalar = Mathf.Max(0.01f, speedScalar);
        }
    }
}
