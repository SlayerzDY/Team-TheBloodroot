using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace Bloodroot.Features.AlphaEnemies
{
    /// <summary>
    /// Narrow compatibility boundary for campaign-owned instances of the
    /// approved level-population enemies. The online Safety controllers and
    /// the campaign Wereboar do not share one runtime contract, so campaign
    /// spawners use this adapter instead of modifying Safety code or prefabs.
    /// </summary>
    public static class CampaignSafetyEnemyRuntimeAdapter
    {
        private const BindingFlags SafetyFieldFlags =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags SafetyMethodFlags =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private const int WalkableAreaMask = 1;
        private const float MinimumNavMeshSampleRadius = 0.01f;
        private const float GroundProbeStartHeight = 4f;
        private const float GroundProbeDistance = 8f;
        private const float MaximumGroundVerticalGap = 0.15f;
        private const float MaximumGroundSurfaceAboveNavMesh = 0.05f;
        private const float MinimumGroundNormalY = 0.15f;

        private static readonly FieldInfo BoarStartingPositionField =
            typeof(global::enemyAI).GetField(
                "startingPos",
                SafetyFieldFlags);
        private static readonly FieldInfo BoarStoppingDistanceField =
            typeof(global::enemyAI).GetField(
                "stoppingDistanceOrig",
                SafetyFieldFlags);

        private static readonly FieldInfo JuggernautStartingPositionField =
            typeof(global::juggernautEnemyAI).GetField(
                "startingPos",
                SafetyFieldFlags);
        private static readonly FieldInfo JuggernautAuthoredStoppingDistanceField =
            typeof(global::juggernautEnemyAI).GetField(
                "stoppingDistance",
                SafetyFieldFlags);
        private static readonly FieldInfo JuggernautStoppingDistanceField =
            typeof(global::juggernautEnemyAI).GetField(
                "stoppingDistanceOrig",
                SafetyFieldFlags);
        private static readonly MethodInfo JuggernautAlertMethod =
            typeof(global::juggernautEnemyAI).GetMethod(
                "Alert",
                SafetyMethodFlags,
                null,
                new[] { typeof(Vector3) },
                null);

        public static bool ValidateSafetyContract(out string error)
        {
            if (!IsVectorField(BoarStartingPositionField) ||
                !IsFloatField(BoarStoppingDistanceField))
            {
                error =
                    "Online Safety enemyAI no longer exposes the exact private Boar roaming compatibility fields.";
                return false;
            }

            if (!IsVectorField(JuggernautStartingPositionField) ||
                !IsIntField(JuggernautAuthoredStoppingDistanceField) ||
                !IsFloatField(JuggernautStoppingDistanceField) ||
                JuggernautAlertMethod == null ||
                JuggernautAlertMethod.ReturnType != typeof(void))
            {
                error =
                    "Online Safety juggernautEnemyAI no longer exposes the exact private roaming and alert compatibility contract.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// True only for an exact regular Boar, Root Boar, Juggernaut,
        /// raw Safety Screecher, or campaign Wereboar controller. Derived and
        /// legacy enemy controllers fail closed.
        /// </summary>
        public static bool IsExactAllowedController(Component controller)
        {
            if (controller == null)
                return false;

            Type controllerType = controller.GetType();
            return controllerType == typeof(global::BoarBruteAI) ||
                   controllerType == typeof(global::BoarBruteRootAI) ||
                   controllerType == typeof(global::juggernautEnemyAI) ||
                   controllerType == typeof(WereBoarController) ||
                   IsExactRawScreecherController(controller);
        }

        public static bool TryGetExactAllowedController(
            GameObject enemy,
            out Component controller,
            out string error)
        {
            controller = null;
            if (enemy == null)
            {
                error = "The campaign enemy is missing.";
                return false;
            }

            global::enemyAI[] boarFamily =
                enemy.GetComponentsInChildren<global::enemyAI>(true);
            global::juggernautEnemyAI[] juggernauts =
                enemy.GetComponentsInChildren<global::juggernautEnemyAI>(true);
            WereBoarController[] wereBoars =
                enemy.GetComponentsInChildren<WereBoarController>(true);
            if (boarFamily.Length + juggernauts.Length + wereBoars.Length != 1)
            {
                error =
                    $"'{enemy.name}' must contain exactly one approved campaign enemy controller.";
                return false;
            }

            controller = boarFamily.Length == 1
                ? boarFamily[0]
                : juggernauts.Length == 1
                    ? juggernauts[0]
                    : wereBoars[0];
            if (!IsExactAllowedController(controller))
            {
                error =
                    $"'{enemy.name}' is not an exact approved Boar, Root Boar, Juggernaut, raw Screecher, or Wereboar controller.";
                controller = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Shared authored-prefab validation used by every campaign spawner.
        /// The Safety Juggernaut is intentionally accepted while its controller
        /// is disabled on the source asset; TryPrepare enables only the spawned
        /// campaign instance.
        /// </summary>
        public static bool TryValidatePrefab(
            GameObject prefab,
            out Component controller,
            out NavMeshAgent agent,
            out Animator animator,
            out string error)
        {
            controller = null;
            agent = null;
            animator = null;

            if (prefab == null)
            {
                error = "The campaign enemy prefab is missing.";
                return false;
            }

            if (prefab.scene.IsValid())
            {
                error = $"'{prefab.name}' must be a prefab asset, not a scene object.";
                return false;
            }

            if (!TryGetExactAllowedController(
                    prefab,
                    out controller,
                    out error))
            {
                return false;
            }

            if (!TryResolveSingletonComponents(
                    controller,
                    out agent,
                    out animator,
                    out error))
            {
                return false;
            }

            bool disabledApprovedSourceController =
                controller.GetType() == typeof(global::juggernautEnemyAI);
            bool isWereBoar = controller.GetType() ==
                              typeof(WereBoarController);
            float requiredAgentRadius = isWereBoar ? 0.78f : 0.5f;
            float requiredAgentHeight = isWereBoar ? 2.35f : 2f;
            Behaviour controllerBehaviour = controller as Behaviour;
            if (controller.transform != prefab.transform ||
                controllerBehaviour == null ||
                (!controllerBehaviour.enabled &&
                 !disabledApprovedSourceController) ||
                agent == null || !agent.enabled ||
                agent.agentTypeID != 0 ||
                (agent.areaMask & WalkableAreaMask) == 0 ||
                Mathf.Abs(agent.radius - requiredAgentRadius) > 0.001f ||
                Mathf.Abs(agent.height - requiredAgentHeight) > 0.001f ||
                animator == null || !animator.enabled)
            {
                error =
                    $"'{prefab.name}' must retain one exact approved root controller, its approved enabled Walkable NavMeshAgent envelope, and one enabled Animator.";
                return false;
            }

            if (IsExactRawScreecherController(controller))
            {
                global::ScreecherAI screecher =
                    controller.GetComponent<global::ScreecherAI>();
                if (screecher == null || !screecher.enabled ||
                    screecher.agent != agent)
                {
                    error =
                        $"'{prefab.name}' must retain the raw Safety ScreecherAI on the controller root and bind it to the root NavMeshAgent.";
                    return false;
                }
            }

            if (isWereBoar &&
                controller.GetComponent<WereBoarController>() != controller)
            {
                error =
                    $"'{prefab.name}' must retain its exact Wereboar controller on the NavMeshAgent root.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool TryGetAgent(
            GameObject enemy,
            out NavMeshAgent agent,
            out string error)
        {
            agent = null;
            if (!TryGetExactAllowedController(
                    enemy,
                    out Component controller,
                    out error))
            {
                return false;
            }

            if (!TryResolveSingletonComponents(
                    controller,
                    out agent,
                    out _,
                    out error))
            {
                return false;
            }

            if (agent == null || !agent.enabled)
            {
                error =
                    $"'{enemy.name}' requires an enabled root NavMeshAgent.";
                agent = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Resolves an authored spawn marker to a point that is compatible with
        /// the exact enemy agent and physically supported by a non-trigger
        /// collider. NavMesh alone is not sufficient: a baked surface without
        /// ground under it is rejected so enemies cannot appear in the air or
        /// below terrain.
        /// </summary>
        public static bool TryResolveGroundedSpawnPosition(
            GameObject enemyPrefabOrInstance,
            Vector3 requestedPosition,
            float navMeshSampleRadius,
            out NavMeshHit groundedPosition,
            out string error)
        {
            groundedPosition = default;
            if (enemyPrefabOrInstance == null ||
                !IsFinite(requestedPosition))
            {
                error = "A grounded campaign spawn requires a finite enemy and marker position.";
                return false;
            }

            if (!IsFinite(navMeshSampleRadius) ||
                navMeshSampleRadius < MinimumNavMeshSampleRadius)
            {
                error = "A grounded campaign spawn requires a positive NavMesh sample radius.";
                return false;
            }

            if (!TryGetExactAllowedController(
                    enemyPrefabOrInstance,
                    out Component controller,
                    out error) ||
                !TryResolveSingletonComponents(
                    controller,
                    out NavMeshAgent agent,
                    out _,
                    out error))
            {
                return false;
            }

            int areaMask = agent.areaMask & WalkableAreaMask;
            if (areaMask == 0)
            {
                error =
                    $"'{enemyPrefabOrInstance.name}' cannot use the authored Walkable NavMesh area.";
                return false;
            }

            NavMeshQueryFilter queryFilter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = areaMask
            };
            if (!NavMesh.SamplePosition(
                    requestedPosition,
                    out groundedPosition,
                    navMeshSampleRadius,
                    queryFilter) ||
                !IsFinite(groundedPosition.position))
            {
                error =
                    $"'{enemyPrefabOrInstance.name}' has no compatible baked NavMesh within {navMeshSampleRadius:0.##}m of its spawn marker.";
                return false;
            }

            if (!TryValidateSolidGround(
                    groundedPosition.position,
                    null,
                    out error))
            {
                error =
                    $"'{enemyPrefabOrInstance.name}' resolved to an unsupported NavMesh point. {error}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Binds a freshly instantiated campaign enemy to an already validated
        /// grounded NavMesh point before its AI is enabled. The helper fails
        /// closed when the root agent cannot warp onto the exact supported
        /// location.
        /// </summary>
        public static bool TryPrepareGroundedSpawn(
            GameObject spawnedEnemy,
            NavMeshHit groundedPosition,
            out Component controller,
            out string error)
        {
            controller = null;
            if (spawnedEnemy == null ||
                !IsFinite(groundedPosition.position))
            {
                error = "A grounded campaign spawn requires a finite live enemy and NavMesh position.";
                return false;
            }

            if (!TryGetExactAllowedController(
                    spawnedEnemy,
                    out controller,
                    out error) ||
                !TryResolveSingletonComponents(
                    controller,
                    out NavMeshAgent agent,
                    out Animator animator,
                    out error))
            {
                controller = null;
                return false;
            }

            if (!TryValidateSolidGround(
                    groundedPosition.position,
                    spawnedEnemy.transform,
                    out error))
            {
                controller = null;
                return false;
            }

            Behaviour controllerBehaviour = controller as Behaviour;
            if (controllerBehaviour == null)
            {
                error =
                    $"'{spawnedEnemy.name}' has no enabled-behaviour campaign controller.";
                controller = null;
                return false;
            }

            try
            {
                // An instantiated Safety prefab can arrive with its AI already
                // enabled. Freeze it before touching the agent so no Update can
                // call SetDestination between creation and the validated warp.
                controllerBehaviour.enabled = false;
                if (IsExactRawScreecherController(controller))
                {
                    global::ScreecherAI screecher =
                        controller.GetComponent<global::ScreecherAI>();
                    if (screecher != null)
                    {
                        screecher.enabled = false;
                    }
                }

                agent.enabled = true;
                animator.enabled = true;
                if (!spawnedEnemy.activeInHierarchy ||
                    !agent.isActiveAndEnabled || !animator.isActiveAndEnabled)
                {
                    error =
                        $"'{spawnedEnemy.name}' could not activate its root agent and Animator for grounded placement.";
                    controller = null;
                    return false;
                }

                spawnedEnemy.transform.position = groundedPosition.position;
                if (!agent.Warp(groundedPosition.position) ||
                    !agent.isOnNavMesh)
                {
                    error =
                        $"'{spawnedEnemy.name}' could not bind its active root NavMeshAgent to the validated ground point.";
                    controller = null;
                    return false;
                }
            }
            catch (Exception exception)
            {
                error =
                    $"Could not place '{spawnedEnemy.name}' on its grounded NavMesh point: {exception.Message}";
                controller = null;
                return false;
            }

            if (!TryPrepare(spawnedEnemy, out error) ||
                !TryGetAgent(spawnedEnemy, out agent, out error) ||
                !agent.isOnNavMesh)
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? $"'{spawnedEnemy.name}' lost its root NavMeshAgent binding during preparation."
                    : error;
                controller = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool TryPrepare(
            GameObject spawnedEnemy,
            out string error)
        {
            if (!ValidateSafetyContract(out error) ||
                !TryGetExactAllowedController(
                    spawnedEnemy,
                    out Component controller,
                    out error))
            {
                return false;
            }

            try
            {
                if (controller is global::enemyAI boar)
                {
                    if (!TryBindSpawnedComponents(
                            boar,
                            out NavMeshAgent agent,
                            out Animator animator,
                            out error))
                    {
                        return false;
                    }

                    // The Safety Boar visual Animator lives below the
                    // gameplay root. Bind the components from this live clone
                    // instead of retaining a cached serialized reference.
                    boar.agent = agent;
                    boar.animator = animator;

                    BoarStartingPositionField.SetValue(
                        boar,
                        boar.transform.position);
                    BoarStoppingDistanceField.SetValue(
                        boar,
                        agent.stoppingDistance);

                    if (IsExactRawScreecherController(boar))
                    {
                        global::ScreecherAI screecher =
                            boar.GetComponent<global::ScreecherAI>();
                        screecher.agent = agent;
                        screecher.enabled = true;
                    }

                    boar.enabled = true;
                }
                else if (controller is global::juggernautEnemyAI juggernaut)
                {
                    if (!TryBindSpawnedComponents(
                            juggernaut,
                            out NavMeshAgent agent,
                            out Animator animator,
                            out error))
                    {
                        return false;
                    }

                    juggernaut.agent = agent;
                    juggernaut.animator = animator;

                    int authoredStoppingDistance =
                        (int)JuggernautAuthoredStoppingDistanceField.GetValue(
                            juggernaut);
                    JuggernautStartingPositionField.SetValue(
                        juggernaut,
                        juggernaut.transform.position);
                    JuggernautStoppingDistanceField.SetValue(
                        juggernaut,
                        (float)authoredStoppingDistance);
                    juggernaut.enabled = true;
                }
                else if (controller is WereBoarController wereBoar)
                {
                    GameObject player = global::gameManager.instance != null
                        ? global::gameManager.instance.player
                        : null;
                    if (player == null)
                    {
                        error =
                            $"'{wereBoar.name}' requires an authoritative Player.";
                        return false;
                    }

                    if (!TryBindSpawnedComponents(
                            wereBoar,
                            out _,
                            out _,
                            out error))
                    {
                        return false;
                    }

                    wereBoar.SetTarget(player.transform);
                    wereBoar.enabled = true;
                }
            }
            catch (Exception exception)
            {
                error =
                    $"Could not initialize '{spawnedEnemy.name}' against the exact Safety roaming contract: {exception.Message}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool TryInitialize(
            Component controller,
            int difficultyLevel,
            out string error)
        {
            if (!IsExactAllowedController(controller) || difficultyLevel < 1)
            {
                error =
                    "Campaign enemy initialization requires an approved controller and a positive difficulty level.";
                return false;
            }

            if (!TryRequireActiveAgentOnNavMesh(controller, out error))
            {
                return false;
            }

            try
            {
                if (controller is global::enemyAI boar)
                {
                    boar.InitializeEnemy(difficultyLevel);
                }
                else if (controller is global::juggernautEnemyAI juggernaut)
                {
                    juggernaut.InitializeEnemy(difficultyLevel);
                }
                else
                {
                    ((WereBoarController)controller).ApplyDifficulty(
                        difficultyLevel,
                        1f,
                        1f,
                        1f,
                        true);
                }
            }
            catch (Exception exception)
            {
                error =
                    $"Could not initialize '{controller.name}' difficulty: {exception.Message}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool TryAlert(
            Component controller,
            Vector3 playerPosition,
            out string error)
        {
            if (!IsExactAllowedController(controller))
            {
                error = "Only an approved campaign enemy can be alerted.";
                return false;
            }

            if (!TryRequireActiveAgentOnNavMesh(controller, out error))
            {
                return false;
            }

            try
            {
                if (controller is global::enemyAI boar)
                {
                    boar.Alert(playerPosition);
                }
                else if (controller is global::juggernautEnemyAI)
                {
                    JuggernautAlertMethod.Invoke(
                        controller,
                        new object[] { playerPosition });
                }
                else
                {
                    ((WereBoarController)controller)
                        .AlertToPosition(playerPosition);
                }
            }
            catch (Exception exception)
            {
                error =
                    $"Could not alert '{controller.name}' through the exact Safety contract: {exception.Message}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsVectorField(FieldInfo field)
        {
            return field != null && field.FieldType == typeof(Vector3);
        }

        private static bool IsFloatField(FieldInfo field)
        {
            return field != null && field.FieldType == typeof(float);
        }

        private static bool IsIntField(FieldInfo field)
        {
            return field != null && field.FieldType == typeof(int);
        }

        private static bool TryRequireActiveAgentOnNavMesh(
            Component controller,
            out string error)
        {
            error = string.Empty;
            if (!TryResolveSingletonComponents(
                    controller,
                    out NavMeshAgent agent,
                    out _,
                    out error))
            {
                return false;
            }

            if (!agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                error =
                    $"'{controller.name}' requires an active NavMeshAgent that is placed on the NavMesh before it can act.";
                return false;
            }

            return true;
        }

        private static bool TryValidateSolidGround(
            Vector3 navMeshPosition,
            Transform ignoredRoot,
            out string error)
        {
            error = string.Empty;
            if (!IsFinite(navMeshPosition))
            {
                error = "The NavMesh point is not finite.";
                return false;
            }

            Vector3 rayOrigin = navMeshPosition +
                                Vector3.up * GroundProbeStartHeight;
            RaycastHit[] hits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                GroundProbeDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float nearestVerticalGap = float.PositiveInfinity;
            Collider supportingCollider = null;
            for (int index = 0; index < hits.Length; index++)
            {
                RaycastHit hit = hits[index];
                Collider collider = hit.collider;
                if (collider == null || !collider.enabled ||
                    collider.isTrigger ||
                    !collider.gameObject.activeInHierarchy ||
                    (ignoredRoot != null &&
                     (collider.transform == ignoredRoot ||
                      collider.transform.IsChildOf(ignoredRoot))) ||
                    hit.normal.y < MinimumGroundNormalY ||
                    !IsFinite(hit.point))
                {
                    continue;
                }

                float verticalGap = Mathf.Abs(hit.point.y - navMeshPosition.y);
                if (hit.point.y > navMeshPosition.y +
                    MaximumGroundSurfaceAboveNavMesh ||
                    verticalGap > MaximumGroundVerticalGap ||
                    verticalGap >= nearestVerticalGap)
                {
                    continue;
                }

                nearestVerticalGap = verticalGap;
                supportingCollider = collider;
            }

            if (supportingCollider == null)
            {
                error =
                    $"No solid ground collider exists directly beneath the NavMesh point within {MaximumGroundVerticalGap:0.##}m.";
                return false;
            }

            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool TryBindSpawnedComponents(
            Component controller,
            out NavMeshAgent agent,
            out Animator animator,
            out string error)
        {
            agent = null;
            animator = null;
            if (controller == null)
            {
                error = "The spawned campaign enemy controller is missing.";
                return false;
            }

            if (!TryResolveSingletonComponents(
                    controller,
                    out agent,
                    out animator,
                    out error))
            {
                return false;
            }

            // Campaign spawn preparation owns the initial enabled state. A
            // disabled component is valid here as long as this live clone can
            // enable it before it is warped and initialized.
            agent.enabled = true;
            animator.enabled = true;
            if (!agent.enabled || !animator.enabled)
            {
                error =
                    $"'{controller.name}' could not enable its spawned NavMeshAgent or Animator.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryResolveSingletonComponents(
            Component controller,
            out NavMeshAgent agent,
            out Animator animator,
            out string error)
        {
            agent = null;
            animator = null;
            if (controller == null)
            {
                error = "The campaign enemy controller is missing.";
                return false;
            }

            // The exact controller remains on the gameplay root, but Safety
            // visuals can keep both movement and animation below it. Every
            // approved source is required to expose one of each, so use the
            // same singleton resolution in prefab validation, preparation,
            // and post-prepare agent retrieval.
            NavMeshAgent[] agents =
                controller.GetComponentsInChildren<NavMeshAgent>(true);
            Animator[] animators =
                controller.GetComponentsInChildren<Animator>(true);
            if (agents.Length != 1 || agents[0] == null ||
                animators.Length != 1 || animators[0] == null)
            {
                error =
                    $"'{controller.name}' requires exactly one NavMeshAgent and one Animator in its controller hierarchy.";
                return false;
            }

            agent = agents[0];
            animator = animators[0];
            error = string.Empty;
            return true;
        }

        private static bool IsExactRawScreecherController(
            Component controller)
        {
            if (controller == null ||
                controller.GetType() != typeof(global::enemyAI))
            {
                return false;
            }

            global::ScreecherAI[] screechers =
                controller.GetComponents<global::ScreecherAI>();
            return screechers.Length == 1 &&
                   screechers[0] != null &&
                   screechers[0].GetType() == typeof(global::ScreecherAI);
        }
    }
}
