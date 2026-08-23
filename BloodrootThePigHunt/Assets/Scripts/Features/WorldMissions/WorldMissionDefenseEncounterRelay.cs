using UnityEngine;

namespace Bloodroot.Features.WorldMissions
{
    /// <summary>
    /// Stable authored bridge between encounter/enemy UnityEvents and a
    /// defense objective. It forwards public lifecycle calls only and has no
    /// dependency on any enemy implementation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMissionDefenseEncounterRelay : MonoBehaviour
    {
        [SerializeField] private WorldMissionDefenseObjective defenseObjective;

        public WorldMissionDefenseObjective DefenseObjective =>
            defenseObjective;

        public void Configure(WorldMissionDefenseObjective objective)
        {
            defenseObjective = objective;
        }

        public void BeginDefense()
        {
            if (TryGetObjective(out WorldMissionDefenseObjective objective))
            {
                objective.BeginDefense();
            }
        }

        public void StopDefense()
        {
            if (TryGetObjective(out WorldMissionDefenseObjective objective))
            {
                objective.StopDefense();
            }
        }

        public void ReportEnemySpawned(GameObject enemy)
        {
            if (TryGetObjective(out WorldMissionDefenseObjective objective))
            {
                objective.NotifyEnemySpawned(enemy);
            }
        }

        public void ReportEnemyDefeated(GameObject enemy)
        {
            if (TryGetObjective(out WorldMissionDefenseObjective objective))
            {
                objective.NotifyEnemyDefeated(enemy);
            }
        }

        /// <summary>
        /// Use for an enemy UnityEvent whose dynamic argument type is not a
        /// GameObject. This is intentionally a distinct method name so Unity's
        /// persistent-event menu cannot select an ambiguous overload.
        /// </summary>
        public void ReportDefeatWithoutReference()
        {
            if (TryGetObjective(out WorldMissionDefenseObjective objective))
            {
                objective.ReportEnemyDefeated();
            }
        }

        public void ReportKillCount(int amount)
        {
            if (TryGetObjective(out WorldMissionDefenseObjective objective))
            {
                objective.RegisterKills(amount);
            }
        }

        private bool TryGetObjective(
            out WorldMissionDefenseObjective objective)
        {
            objective = defenseObjective;

            if (objective != null)
                return true;


            return false;
        }
    }
}
