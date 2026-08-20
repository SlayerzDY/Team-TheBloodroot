using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace Bloodroot.Features.AlphaEnemies
{
    /// <summary>
    /// Narrow compatibility boundary for campaign-owned instances of the
    /// online Safety enemyAI. Safety leaves its private roaming origin and
    /// original stopping distance uninitialized, so campaign spawners set the
    /// two exact fields without modifying the protected controller or prefabs.
    /// </summary>
    public static class CampaignSafetyEnemyRuntimeAdapter
    {
        private const BindingFlags SafetyFieldFlags =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo StartingPositionField =
            typeof(global::enemyAI).GetField(
                "startingPos",
                SafetyFieldFlags);
        private static readonly FieldInfo StoppingDistanceField =
            typeof(global::enemyAI).GetField(
                "stoppingDistanceOrig",
                SafetyFieldFlags);

        public static bool ValidateSafetyContract(out string error)
        {
            if (StartingPositionField == null ||
                StartingPositionField.FieldType != typeof(Vector3) ||
                StoppingDistanceField == null ||
                StoppingDistanceField.FieldType != typeof(float))
            {
                error =
                    "Online Safety enemyAI no longer exposes the exact private roaming compatibility fields.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool TryPrepare(
            GameObject spawnedEnemy,
            out string error)
        {
            if (spawnedEnemy == null)
            {
                error = "The spawned Safety enemy is missing.";
                return false;
            }

            if (!ValidateSafetyContract(out error))
                return false;

            global::enemyAI[] safetyEnemies = spawnedEnemy
                .GetComponentsInChildren<global::enemyAI>(true);
            if (safetyEnemies.Length == 0)
            {
                error =
                    $"'{spawnedEnemy.name}' contains no online Safety enemyAI to prepare.";
                return false;
            }

            foreach (global::enemyAI safetyEnemy in safetyEnemies)
            {
                if (safetyEnemy.agent == null)
                {
                    safetyEnemy.agent = safetyEnemy.GetComponent<NavMeshAgent>();
                }

                if (safetyEnemy.animator == null)
                {
                    safetyEnemy.animator = safetyEnemy
                        .GetComponentInChildren<Animator>(true);
                }

                if (safetyEnemy.agent == null || !safetyEnemy.agent.enabled ||
                    safetyEnemy.animator == null || !safetyEnemy.animator.enabled)
                {
                    error =
                        $"'{safetyEnemy.name}' requires an enabled root NavMeshAgent and enabled Animator.";
                    return false;
                }

                try
                {
                    StartingPositionField.SetValue(
                        safetyEnemy,
                        safetyEnemy.transform.position);
                    StoppingDistanceField.SetValue(
                        safetyEnemy,
                        safetyEnemy.agent.stoppingDistance);
                }
                catch (Exception exception)
                {
                    error =
                        $"Could not initialize '{safetyEnemy.name}' against the exact Safety roaming contract: {exception.Message}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
