using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace Bloodroot.Features.AlphaEnemies
{
    /// <summary>
    /// Narrow compatibility boundary for campaign-owned instances of the
    /// three enemies approved for level population. The online Safety Boar
    /// controllers and Juggernaut do not share a base controller, and both
    /// keep roaming state private, so campaign spawners use this adapter
    /// instead of modifying Safety code or prefabs.
    /// </summary>
    public static class CampaignSafetyEnemyRuntimeAdapter
    {
        private const BindingFlags SafetyFieldFlags =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags SafetyMethodFlags =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private const int WalkableAreaMask = 1;

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
        /// True only for the exact regular Boar, Root Boar, or Juggernaut
        /// controller. Derived or legacy enemy controllers fail closed.
        /// </summary>
        public static bool IsExactAllowedController(Component controller)
        {
            if (controller == null)
                return false;

            Type controllerType = controller.GetType();
            return controllerType == typeof(global::BoarBruteAI) ||
                   controllerType == typeof(global::BoarBruteRootAI) ||
                   controllerType == typeof(global::juggernautEnemyAI);
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
            if (boarFamily.Length + juggernauts.Length != 1)
            {
                error =
                    $"'{enemy.name}' must contain exactly one approved campaign enemy controller.";
                return false;
            }

            controller = boarFamily.Length == 1
                ? boarFamily[0]
                : juggernauts[0];
            if (!IsExactAllowedController(controller))
            {
                error =
                    $"'{enemy.name}' is not the exact regular Boar, Root Boar, or Juggernaut controller.";
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

            NavMeshAgent[] agents =
                prefab.GetComponentsInChildren<NavMeshAgent>(true);
            Animator[] animators =
                prefab.GetComponentsInChildren<Animator>(true);
            agent = agents.Length == 1 ? agents[0] : null;
            animator = animators.Length == 1 ? animators[0] : null;

            bool disabledApprovedSourceController =
                controller.GetType() == typeof(global::juggernautEnemyAI);
            Behaviour controllerBehaviour = controller as Behaviour;
            if (controller.transform != prefab.transform ||
                controllerBehaviour == null ||
                (!controllerBehaviour.enabled &&
                 !disabledApprovedSourceController) ||
                agent == null || !agent.enabled ||
                agent.transform != prefab.transform ||
                agent.agentTypeID != 0 ||
                (agent.areaMask & WalkableAreaMask) == 0 ||
                Mathf.Abs(agent.radius - 0.5f) > 0.001f ||
                Mathf.Abs(agent.height - 2f) > 0.001f ||
                animator == null || !animator.enabled)
            {
                error =
                    $"'{prefab.name}' must retain one exact approved root controller, enabled Humanoid Walkable NavMeshAgent, and enabled Animator.";
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

            if (controller is global::enemyAI boar)
            {
                agent = boar.agent != null
                    ? boar.agent
                    : boar.GetComponent<NavMeshAgent>();
            }
            else if (controller is global::juggernautEnemyAI juggernaut)
            {
                agent = juggernaut.agent != null
                    ? juggernaut.agent
                    : juggernaut.GetComponent<NavMeshAgent>();
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
                    boar.agent ??= boar.GetComponent<NavMeshAgent>();
                    boar.animator ??=
                        boar.GetComponentInChildren<Animator>(true);
                    if (boar.agent == null || !boar.agent.enabled ||
                        boar.animator == null || !boar.animator.enabled)
                    {
                        error =
                            $"'{boar.name}' requires an enabled root NavMeshAgent and enabled Animator.";
                        return false;
                    }

                    BoarStartingPositionField.SetValue(
                        boar,
                        boar.transform.position);
                    BoarStoppingDistanceField.SetValue(
                        boar,
                        boar.agent.stoppingDistance);
                    boar.enabled = true;
                }
                else if (controller is global::juggernautEnemyAI juggernaut)
                {
                    juggernaut.agent ??=
                        juggernaut.GetComponent<NavMeshAgent>();
                    juggernaut.animator ??=
                        juggernaut.GetComponentInChildren<Animator>(true);
                    if (juggernaut.agent == null ||
                        !juggernaut.agent.enabled ||
                        juggernaut.animator == null ||
                        !juggernaut.animator.enabled)
                    {
                        error =
                            $"'{juggernaut.name}' requires an enabled root NavMeshAgent and enabled Animator.";
                        return false;
                    }

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

            try
            {
                if (controller is global::enemyAI boar)
                    boar.InitializeEnemy(difficultyLevel);
                else
                    ((global::juggernautEnemyAI)controller)
                        .InitializeEnemy(difficultyLevel);
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

            try
            {
                if (controller is global::enemyAI boar)
                {
                    boar.Alert(playerPosition);
                }
                else
                {
                    JuggernautAlertMethod.Invoke(
                        controller,
                        new object[] { playerPosition });
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
    }
}
