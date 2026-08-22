using System;
using System.Collections;
using System.Collections.Generic;
using Bloodroot.Features.AlphaEnemies;
using UnityEngine;
using UnityEngine.AI;

namespace Bloodroot.Features.FarmPrologue
{
    /// <summary>
    /// Presentation-only listener for enemies spawned by the prologue wave.
    /// It temporarily gates only the spawned enemy's movement components while
    /// raising it from below its authored NavMesh spawn position. Enemy AI and
    /// encounter ownership remain with their existing systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FarmEnemyEmergencePresenter : MonoBehaviour
    {
        [Header("Existing Wave Hook")]
        [SerializeField] private waveManager waveEncounter;

        [Header("Ground Emergence")]
        [SerializeField] private bool animateGroundEmergence = true;
        [SerializeField, Min(0.01f)] private float emergenceDepth = 1.75f;
        [SerializeField, Min(0f)] private float rendererHeightDepthMultiplier = 0.9f;
        [SerializeField, Min(0.01f)] private float maximumEmergenceDepth = 4f;
        [SerializeField, Min(0.01f)] private float emergenceDuration = 1.1f;
        [SerializeField, Min(0f)] private float emergenceStaggerSeconds = 0.08f;
        [Tooltip("Maximum local distance used to validate the enemy's authored surface against its own NavMesh agent type and area mask.")]
        [SerializeField, Min(0.01f)]
        private float navMeshSurfaceSampleRadius = 3f;
        [SerializeField] private AnimationCurve riseCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Authored Emergence Presentation")]
        [SerializeField] private Animator[] emergenceAnimators =
            Array.Empty<Animator>();
        [SerializeField] private string emergenceTrigger = "Emerge";
        [SerializeField] private ParticleSystem[] emergenceEffects =
            Array.Empty<ParticleSystem>();

        [Header("Authored Extension Hook")]
        [SerializeField] private FarmGameObjectUnityEvent enemyEmergencePresented = new();

        private readonly Dictionary<GameObject, EmergenceState>
            activeEmergences = new();
        private static bool warnedAboutSafetyEnemyContract;
        private bool isBound;
        private int emergenceTriggerHash;
        private float nextEmergenceStartTime;

        public event Action<GameObject> EnemyEmergencePresented;

        private sealed class EmergenceState
        {
            public GameObject Enemy;
            public Transform EnemyTransform;
            public Vector3 SurfacePosition;
            public float Depth;
            public NavMeshAgent Agent;
            public bool AgentWasEnabled;
            public bool AgentWasOnNavMesh;
            public bool AgentWasStopped;
            public Behaviour[] MovementBehaviours = Array.Empty<Behaviour>();
            public bool[] MovementBehaviourStates = Array.Empty<bool>();
            public RigidbodyState[] Rigidbodies = Array.Empty<RigidbodyState>();
            public ColliderState[] Colliders = Array.Empty<ColliderState>();
        }

        private readonly struct RigidbodyState
        {
            public readonly Rigidbody Body;
            public readonly bool WasKinematic;
            public readonly bool UsedGravity;
            public readonly bool DetectedCollisions;
            public readonly bool WasSleeping;
            public readonly Vector3 LinearVelocity;
            public readonly Vector3 AngularVelocity;

            public RigidbodyState(Rigidbody body)
            {
                Body = body;
                WasKinematic = body != null && body.isKinematic;
                UsedGravity = body != null && body.useGravity;
                DetectedCollisions = body != null && body.detectCollisions;
                WasSleeping = body != null && body.IsSleeping();
                LinearVelocity = body != null
                    ? body.linearVelocity
                    : Vector3.zero;
                AngularVelocity = body != null
                    ? body.angularVelocity
                    : Vector3.zero;
            }
        }

        private readonly struct ColliderState
        {
            public readonly Collider Collider;
            public readonly bool WasEnabled;

            public ColliderState(Collider collider)
            {
                Collider = collider;
                WasEnabled = collider != null && collider.enabled;
            }
        }

        private void Awake()
        {
            RefreshTriggerHash();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
            CancelAllEmergences();
        }

        private void OnDestroy()
        {
            CancelAllEmergences();
        }

        private void OnValidate()
        {
            emergenceDepth = Mathf.Max(0.01f, emergenceDepth);
            rendererHeightDepthMultiplier =
                Mathf.Max(0f, rendererHeightDepthMultiplier);
            maximumEmergenceDepth =
                Mathf.Max(emergenceDepth, maximumEmergenceDepth);
            emergenceDuration = Mathf.Max(0.01f, emergenceDuration);
            emergenceStaggerSeconds = Mathf.Max(0f, emergenceStaggerSeconds);
            navMeshSurfaceSampleRadius =
                SanitizeSampleRadius(navMeshSurfaceSampleRadius);
            RefreshTriggerHash();
        }

        public void Configure(
            waveManager manager,
            Animator[] authoredAnimators,
            ParticleSystem[] authoredEffects,
            string animatorTrigger = "Emerge")
        {
            Unbind();
            CancelAllEmergences();
            waveEncounter = manager;
            emergenceAnimators =
                authoredAnimators ?? Array.Empty<Animator>();
            emergenceEffects =
                authoredEffects ?? Array.Empty<ParticleSystem>();
            emergenceTrigger = animatorTrigger ?? string.Empty;
            RefreshTriggerHash();

            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

        public void ConfigureGroundEmergence(
            bool enabled,
            float depth,
            float duration,
            float staggerSeconds)
        {
            animateGroundEmergence = enabled;
            emergenceDepth = Mathf.Max(0.01f, depth);
            maximumEmergenceDepth =
                Mathf.Max(emergenceDepth, maximumEmergenceDepth);
            emergenceDuration = Mathf.Max(0.01f, duration);
            emergenceStaggerSeconds = Mathf.Max(0f, staggerSeconds);
        }

        public void ConfigureDepthScaling(
            float heightMultiplier,
            float maximumDepth)
        {
            rendererHeightDepthMultiplier = Mathf.Max(0f, heightMultiplier);
            maximumEmergenceDepth =
                Mathf.Max(emergenceDepth, maximumDepth);
        }

        public void ConfigureNavMeshProjection(float maximumSampleRadius)
        {
            navMeshSurfaceSampleRadius =
                SanitizeSampleRadius(maximumSampleRadius);
        }

        private void Bind()
        {
            if (isBound || waveEncounter == null)
                return;

            waveEncounter.EncounterEnemySpawned += PresentEnemyEmergence;
            isBound = true;
        }

        private void Unbind()
        {
            if (isBound && waveEncounter != null)
            {
                waveEncounter.EncounterEnemySpawned -= PresentEnemyEmergence;
            }

            isBound = false;
        }

        private void PresentEnemyEmergence(GameObject spawnedEnemy)
        {
            PreparePrologueWaveJuggernaut(spawnedEnemy);
            PresentExternalEnemy(spawnedEnemy);
        }

        private void PreparePrologueWaveJuggernaut(GameObject spawnedEnemy)
        {
            if (spawnedEnemy == null ||
                !CampaignSafetyEnemyRuntimeAdapter
                    .TryGetExactAllowedController(
                        spawnedEnemy,
                        out Component controller,
                        out _) ||
                controller.GetType() !=
                    typeof(global::juggernautEnemyAI))
            {
                return;
            }

            if (!CampaignSafetyEnemyRuntimeAdapter.TryPrepare(
                    spawnedEnemy,
                    out string preparationError))
            {
                Debug.LogError(
                    "The prologue Juggernaut failed its campaign spawn " +
                    $"contract. {preparationError}",
                    spawnedEnemy);
                return;
            }

            if (!CampaignSafetyEnemyRuntimeAdapter.TryInitialize(
                    controller,
                    Mathf.Max(1, waveEncounter.currentWave),
                    out string initializationError))
            {
                Debug.LogError(
                    "The prologue Juggernaut could not apply wave " +
                    $"difficulty. {initializationError}",
                    spawnedEnemy);
            }
        }

        /// <summary>
        /// Reuses the authored Farm emergence presentation for an enemy owned
        /// by a campaign emergence other than the prologue WaveManager.
        /// Calls are idempotent for an enemy already being presented.
        /// </summary>
        public void PresentExternalEnemy(GameObject spawnedEnemy)
        {
            PresentExternalEnemy(spawnedEnemy, null);
        }

        /// <summary>
        /// Presents an externally owned enemy and, when this authored
        /// presenter is inactive in Hub state, can run the rise sequence on
        /// the active owning director without moving or duplicating the
        /// presentation component.
        /// </summary>
        public void PresentExternalEnemy(
            GameObject spawnedEnemy,
            MonoBehaviour coroutineHost)
        {
            if (spawnedEnemy == null)
                return;

            if (activeEmergences.ContainsKey(spawnedEnemy))
                return;

            PrepareSafetyEnemyCompatibility(spawnedEnemy);
            PlayAuthoredPresentation();

            if (animateGroundEmergence)
            {
                MonoBehaviour host = isActiveAndEnabled &&
                                     gameObject.activeInHierarchy
                    ? this
                    : coroutineHost;
                if (host != null && host.isActiveAndEnabled &&
                    host.gameObject.activeInHierarchy)
                {
                    BeginGroundEmergence(spawnedEnemy, host);
                }
            }

            FarmPrologueEventUtility.Invoke(
                enemyEmergencePresented,
                spawnedEnemy,
                this);
            FarmPrologueEventUtility.Invoke(
                EnemyEmergencePresented,
                spawnedEnemy,
                this);
        }

        private static void PrepareSafetyEnemyCompatibility(
            GameObject spawnedEnemy)
        {
            if (!CampaignSafetyEnemyRuntimeAdapter.TryPrepare(
                    spawnedEnemy,
                    out string problem) &&
                !warnedAboutSafetyEnemyContract)
            {
                warnedAboutSafetyEnemyContract = true;
                Debug.LogWarning(
                    "Farm safety-enemy compatibility failed closed. " +
                    problem);
            }
        }

        private void BeginGroundEmergence(
            GameObject spawnedEnemy,
            MonoBehaviour coroutineHost)
        {
            var state = new EmergenceState
            {
                Enemy = spawnedEnemy,
                EnemyTransform = spawnedEnemy.transform,
                SurfacePosition = spawnedEnemy.transform.position,
                Depth = CalculateEmergenceDepth(spawnedEnemy),
                Agent = spawnedEnemy.GetComponent<NavMeshAgent>()
            };

            CaptureMovementState(state, spawnedEnemy);
            GatePhysics(state);

            if (!TryPrepareValidatedSurface(state))
            {
                RestoreMovement(state);
                Debug.LogWarning(
                    $"{name}: skipped ground-emergence movement gating for " +
                    $"'{spawnedEnemy.name}' because no valid nearby NavMesh " +
                    "surface could be resolved. The enemy remains active.",
                    spawnedEnemy);
                return;
            }

            RefreshSafetyEnemyOrigin(spawnedEnemy);
            GateMovement(state);
            activeEmergences.Add(spawnedEnemy, state);

            state.EnemyTransform.position =
                state.SurfacePosition + Vector3.down * state.Depth;

            float now = Time.time;
            float scheduledStart = Mathf.Max(now, nextEmergenceStartTime);
            float delay = scheduledStart - now;
            nextEmergenceStartTime =
                scheduledStart + emergenceStaggerSeconds;

            coroutineHost.StartCoroutine(RunGroundEmergence(state, delay));
        }

        private static void CaptureMovementState(
            EmergenceState state,
            GameObject spawnedEnemy)
        {
            if (state.Agent != null)
            {
                state.AgentWasEnabled = state.Agent.enabled;
                state.AgentWasOnNavMesh =
                    state.AgentWasEnabled && state.Agent.isOnNavMesh;
                state.AgentWasStopped =
                    state.AgentWasOnNavMesh && state.Agent.isStopped;

            }

            var movementBehaviours = new List<Behaviour>();
            var uniqueBehaviours = new HashSet<Behaviour>();

            foreach (enemyAI movementAI in
                     spawnedEnemy.GetComponentsInChildren<enemyAI>(true))
            {
                if (movementAI != null && uniqueBehaviours.Add(movementAI))
                {
                    movementBehaviours.Add(movementAI);
                }
            }

            foreach (BoarBruteAI chargeAI in
                     spawnedEnemy.GetComponentsInChildren<BoarBruteAI>(true))
            {
                if (chargeAI != null && uniqueBehaviours.Add(chargeAI))
                {
                    movementBehaviours.Add(chargeAI);
                }
            }

            foreach (juggernautEnemyAI juggernaut in
                     spawnedEnemy.GetComponentsInChildren<juggernautEnemyAI>(
                         true))
            {
                if (juggernaut != null &&
                    uniqueBehaviours.Add(juggernaut))
                {
                    movementBehaviours.Add(juggernaut);
                }
            }

            state.MovementBehaviours = movementBehaviours.ToArray();
            state.MovementBehaviourStates =
                new bool[state.MovementBehaviours.Length];

            for (int index = 0;
                 index < state.MovementBehaviours.Length;
                 index++)
            {
                Behaviour behaviour = state.MovementBehaviours[index];
                state.MovementBehaviourStates[index] = behaviour.enabled;
            }

            Rigidbody[] rigidbodies =
                spawnedEnemy.GetComponentsInChildren<Rigidbody>(true);
            state.Rigidbodies = new RigidbodyState[rigidbodies.Length];
            for (int index = 0; index < rigidbodies.Length; index++)
            {
                state.Rigidbodies[index] =
                    new RigidbodyState(rigidbodies[index]);
            }

            Collider[] colliders =
                spawnedEnemy.GetComponentsInChildren<Collider>(true);
            state.Colliders = new ColliderState[colliders.Length];
            for (int index = 0; index < colliders.Length; index++)
            {
                state.Colliders[index] = new ColliderState(colliders[index]);
            }
        }

        private static void GateMovement(EmergenceState state)
        {
            if (state.Agent != null &&
                state.AgentWasEnabled &&
                state.Agent.enabled)
            {
                state.Agent.enabled = false;
            }

            for (int index = 0;
                 index < state.MovementBehaviours.Length;
                 index++)
            {
                Behaviour behaviour = state.MovementBehaviours[index];

                if (behaviour != null)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void GatePhysics(EmergenceState state)
        {
            foreach (ColliderState colliderState in state.Colliders)
            {
                if (colliderState.Collider != null)
                    colliderState.Collider.enabled = false;
            }

            foreach (RigidbodyState rigidbodyState in state.Rigidbodies)
            {
                Rigidbody body = rigidbodyState.Body;
                if (body == null)
                    continue;

                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.useGravity = false;
                body.detectCollisions = false;
                body.isKinematic = true;
            }
        }

        private bool TryPrepareValidatedSurface(EmergenceState state)
        {
            if (state.Agent == null ||
                !TrySampleSurface(
                    state.Agent,
                    state.SurfacePosition,
                    out Vector3 projectedSurface))
            {
                return false;
            }

            if (state.AgentWasEnabled)
            {
                if (!state.Agent.Warp(projectedSurface))
                {
                    return false;
                }
            }
            else
            {
                state.EnemyTransform.position = projectedSurface;
            }

            state.SurfacePosition = projectedSurface;
            return true;
        }

        private bool TrySampleSurface(
            NavMeshAgent agent,
            Vector3 requestedPosition,
            out Vector3 sampledPosition)
        {
            sampledPosition = requestedPosition;

            if (agent == null || !IsFinite(requestedPosition))
                return false;

            var queryFilter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };

            if (!NavMesh.SamplePosition(
                    requestedPosition,
                    out NavMeshHit hit,
                    SanitizeSampleRadius(navMeshSurfaceSampleRadius),
                    queryFilter) ||
                !IsFinite(hit.position))
            {
                return false;
            }

            sampledPosition = hit.position;
            return true;
        }

        private IEnumerator RunGroundEmergence(
            EmergenceState state,
            float delay)
        {
            float delayed = 0f;

            while (delayed < delay)
            {
                if (!IsEnemyAlive(state))
                {
                    ForgetEmergence(state);
                    yield break;
                }

                delayed += Time.deltaTime;
                yield return null;
            }

            if (emergenceDuration <= 0f)
            {
                CompleteEmergence(state);
                yield break;
            }

            float elapsed = 0f;
            Vector3 buriedPosition =
                state.SurfacePosition + Vector3.down * state.Depth;

            while (elapsed < emergenceDuration)
            {
                if (!IsEnemyAlive(state))
                {
                    ForgetEmergence(state);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float normalized =
                    Mathf.Clamp01(elapsed / emergenceDuration);
                float curved = riseCurve == null
                    ? normalized
                    : Mathf.Clamp01(riseCurve.Evaluate(normalized));

                state.EnemyTransform.position =
                    Vector3.LerpUnclamped(
                        buriedPosition,
                        state.SurfacePosition,
                        curved);
                yield return null;
            }

            CompleteEmergence(state);
        }

        private void CompleteEmergence(EmergenceState state)
        {
            RestoreMovement(state);
            activeEmergences.Remove(state.Enemy);
        }

        private void RestoreMovement(EmergenceState state)
        {
            if (!IsEnemyAlive(state))
                return;

            Vector3 restoreSurface = state.SurfacePosition;

            if (state.Agent != null &&
                TrySampleSurface(
                    state.Agent,
                    state.SurfacePosition,
                    out Vector3 projectedSurface))
            {
                restoreSurface = projectedSurface;
                state.SurfacePosition = projectedSurface;
            }

            // SurfacePosition was validated before movement was gated. If a
            // dynamic NavMesh update temporarily prevents the second sample,
            // restoring to that last valid point still fails presentation
            // open and releases the enemy's original components.
            state.EnemyTransform.position = restoreSurface;
            RefreshSafetyEnemyOrigin(state.Enemy);

            if (state.Agent != null)
            {
                if (state.AgentWasEnabled && !state.Agent.enabled)
                {
                    state.Agent.enabled = true;
                }

                if (state.AgentWasEnabled && state.Agent.enabled &&
                    state.Agent.isOnNavMesh)
                {
                    state.Agent.Warp(restoreSurface);

                    if (state.AgentWasOnNavMesh)
                    {
                        state.Agent.isStopped = state.AgentWasStopped;
                    }
                }
                else if (!state.AgentWasEnabled && state.Agent.enabled)
                {
                    state.Agent.enabled = false;
                }
            }

            RestorePhysics(state);

            int behaviourCount = Mathf.Min(
                state.MovementBehaviours.Length,
                state.MovementBehaviourStates.Length);

            for (int index = 0; index < behaviourCount; index++)
            {
                Behaviour behaviour = state.MovementBehaviours[index];

                if (behaviour != null)
                {
                    behaviour.enabled =
                        state.MovementBehaviourStates[index];
                }
            }
        }

        private static void RestorePhysics(EmergenceState state)
        {
            foreach (RigidbodyState rigidbodyState in state.Rigidbodies)
            {
                Rigidbody body = rigidbodyState.Body;
                if (body == null)
                    continue;

                body.isKinematic = rigidbodyState.WasKinematic;
                body.useGravity = rigidbodyState.UsedGravity;
                body.detectCollisions = rigidbodyState.DetectedCollisions;
                if (!rigidbodyState.WasKinematic)
                {
                    body.linearVelocity = rigidbodyState.LinearVelocity;
                    body.angularVelocity = rigidbodyState.AngularVelocity;
                    if (rigidbodyState.WasSleeping)
                        body.Sleep();
                    else
                        body.WakeUp();
                }
            }

            foreach (ColliderState colliderState in state.Colliders)
            {
                if (colliderState.Collider != null)
                {
                    colliderState.Collider.enabled =
                        colliderState.WasEnabled;
                }
            }
        }

        private static void RefreshSafetyEnemyOrigin(GameObject enemy)
        {
            if (enemy == null)
                return;

            if (!CampaignSafetyEnemyRuntimeAdapter.TryPrepare(
                    enemy,
                    out string problem))
            {
                Debug.LogWarning(
                    $"Could not refresh the emerged Safety enemy's authored " +
                    $"roaming origin. {problem}",
                    enemy);
            }
        }

        private void ForgetEmergence(EmergenceState state)
        {
            activeEmergences.Remove(state.Enemy);
        }

        private void CancelAllEmergences()
        {
            StopAllCoroutines();

            if (activeEmergences.Count > 0)
            {
                EmergenceState[] states =
                    new EmergenceState[activeEmergences.Count];
                activeEmergences.Values.CopyTo(states, 0);

                foreach (EmergenceState state in states)
                {
                    RestoreMovement(state);
                }

                activeEmergences.Clear();
            }

            nextEmergenceStartTime = 0f;
        }

        private void PlayAuthoredPresentation()
        {
            if (emergenceTriggerHash != 0)
            {
                foreach (Animator animator in emergenceAnimators)
                {
                    if (animator == null)
                        continue;

                    try
                    {
                        animator.ResetTrigger(emergenceTriggerHash);
                        animator.SetTrigger(emergenceTriggerHash);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, animator);
                    }
                }
            }

            foreach (ParticleSystem effect in emergenceEffects)
            {
                if (effect == null)
                    continue;

                try
                {
                    effect.Play(true);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, effect);
                }
            }
        }

        private static bool IsEnemyAlive(EmergenceState state)
        {
            return state != null &&
                   state.Enemy != null &&
                   state.EnemyTransform != null;
        }

        private float CalculateEmergenceDepth(GameObject spawnedEnemy)
        {
            Renderer[] renderers =
                spawnedEnemy.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds combinedBounds = default;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null ||
                    renderer is ParticleSystemRenderer ||
                    renderer is TrailRenderer)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }

            float heightScaledDepth = hasBounds
                ? combinedBounds.size.y * rendererHeightDepthMultiplier
                : emergenceDepth;

            return Mathf.Clamp(
                Mathf.Max(emergenceDepth, heightScaledDepth),
                emergenceDepth,
                maximumEmergenceDepth);
        }

        private void RefreshTriggerHash()
        {
            emergenceTriggerHash = string.IsNullOrWhiteSpace(emergenceTrigger)
                ? 0
                : Animator.StringToHash(emergenceTrigger);
        }

        private static float SanitizeSampleRadius(float radius)
        {
            return float.IsNaN(radius) || float.IsInfinity(radius)
                ? 3f
                : Mathf.Max(0.01f, radius);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.z);
        }
    }
}
