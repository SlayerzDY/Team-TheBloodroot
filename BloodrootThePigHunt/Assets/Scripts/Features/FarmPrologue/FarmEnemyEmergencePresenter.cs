using System;
using System.Collections;
using System.Collections.Generic;
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
            if (spawnedEnemy == null)
                return;

            if (activeEmergences.ContainsKey(spawnedEnemy))
                return;

            PlayAuthoredPresentation();

            if (animateGroundEmergence)
            {
                BeginGroundEmergence(spawnedEnemy);
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

        private void BeginGroundEmergence(GameObject spawnedEnemy)
        {
            var state = new EmergenceState
            {
                Enemy = spawnedEnemy,
                EnemyTransform = spawnedEnemy.transform,
                SurfacePosition = spawnedEnemy.transform.position,
                Depth = CalculateEmergenceDepth(spawnedEnemy),
                Agent = spawnedEnemy.GetComponent<NavMeshAgent>()
            };

            CaptureAndGateMovement(state, spawnedEnemy);
            activeEmergences.Add(spawnedEnemy, state);

            state.EnemyTransform.position =
                state.SurfacePosition + Vector3.down * state.Depth;

            float now = Time.unscaledTime;
            float scheduledStart = Mathf.Max(now, nextEmergenceStartTime);
            float delay = scheduledStart - now;
            nextEmergenceStartTime =
                scheduledStart + emergenceStaggerSeconds;

            StartCoroutine(RunGroundEmergence(state, delay));
        }

        private static void CaptureAndGateMovement(
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

                if (state.AgentWasEnabled)
                {
                    state.Agent.enabled = false;
                }
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

            foreach (ScreecherAI screecherAI in
                     spawnedEnemy.GetComponentsInChildren<ScreecherAI>(true))
            {
                if (screecherAI != null &&
                    uniqueBehaviours.Add(screecherAI))
                {
                    movementBehaviours.Add(screecherAI);
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
                behaviour.enabled = false;
            }
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

                delayed += Time.unscaledDeltaTime;
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

                elapsed += Time.unscaledDeltaTime;
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

        private static void RestoreMovement(EmergenceState state)
        {
            if (!IsEnemyAlive(state))
                return;

            state.EnemyTransform.position = state.SurfacePosition;

            if (state.Agent != null)
            {
                if (state.AgentWasEnabled && !state.Agent.enabled)
                {
                    state.Agent.enabled = true;
                }

                if (state.AgentWasEnabled && state.Agent.enabled &&
                    state.Agent.isOnNavMesh)
                {
                    state.Agent.Warp(state.SurfacePosition);

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
    }
}
