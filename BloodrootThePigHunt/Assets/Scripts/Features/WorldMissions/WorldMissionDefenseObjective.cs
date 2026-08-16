using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.WorldMissions
{
    /// <summary>
    /// A timer and/or kill-count objective. Encounter code reports lifecycle
    /// through the public hooks; this component does not inspect or modify AI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMissionDefenseObjective :
        WorldMissionObjective
    {
        [Header("Defense Requirements")]
        [SerializeField] private WorldMissionDefenseCompletionMode
            completionMode =
                WorldMissionDefenseCompletionMode.DurationAndKillCount;
        [SerializeField, Min(0.1f)] private float requiredDuration = 30f;
        [SerializeField, Min(1)] private int requiredKills = 1;
        [SerializeField] private bool beginWhenObjectiveActivates = true;
        [SerializeField] private bool requireSpawnRegistrationForKills = false;

        [Header("Authored Defense Hooks")]
        [SerializeField] private UnityEvent defenseStarted = new();
        [SerializeField] private WorldMissionDefenseProgressUnityEvent
            defenseProgressChanged = new();
        [SerializeField] private UnityEvent defenseStopped = new();
        [SerializeField] private UnityEvent defenseSatisfied = new();

        private readonly HashSet<GameObject> registeredEnemies = new();
        private readonly HashSet<GameObject> defeatedEnemies = new();

        private bool defenseRunning;
        private float elapsedDuration;
        private int defeatedCount;
        private int lastPublishedSecond = -1;

        public WorldMissionDefenseCompletionMode CompletionMode =>
            completionMode;
        public bool IsDefenseRunning => defenseRunning;
        public float ElapsedDuration => elapsedDuration;
        public float RequiredDuration => Mathf.Max(0.1f, requiredDuration);
        public int DefeatedCount => defeatedCount;
        public int RequiredKills => Mathf.Max(1, requiredKills);

        public override int CurrentAmount => UsesKillProgress
            ? defeatedCount
            : Mathf.Min(
                Mathf.FloorToInt(elapsedDuration),
                Mathf.CeilToInt(RequiredDuration));

        public override int RequiredAmount => UsesKillProgress
            ? RequiredKills
            : Mathf.CeilToInt(RequiredDuration);

        public event Action DefenseStarted;
        public event Action<float, float, int, int> DefenseProgressChanged;
        public event Action DefenseStopped;

        private bool UsesKillProgress =>
            completionMode != WorldMissionDefenseCompletionMode.DurationOnly;

        private void OnValidate()
        {
            requiredDuration = Mathf.Max(0.1f, requiredDuration);
            requiredKills = Mathf.Max(1, requiredKills);
        }

        private void Update()
        {
            if (!defenseRunning || !IsAvailable)
                return;

            elapsedDuration = Mathf.Min(
                elapsedDuration + Time.deltaTime,
                RequiredDuration);

            int elapsedSecond = Mathf.FloorToInt(elapsedDuration);

            if (elapsedSecond != lastPublishedSecond)
            {
                lastPublishedSecond = elapsedSecond;
                PublishDefenseProgress();
            }

            TryCompleteWhenReady();
        }

        public void ConfigureDefense(
            WorldMissionDefenseCompletionMode mode,
            float durationSeconds,
            int killCount,
            bool autoBegin)
        {
            completionMode = mode;
            requiredDuration = Mathf.Max(0.1f, durationSeconds);
            requiredKills = Mathf.Max(1, killCount);
            beginWhenObjectiveActivates = autoBegin;
        }

        public void BeginDefense()
        {
            if (!IsAvailable)
            {
                RejectAction("The defense objective is not currently available.");
                return;
            }

            if (defenseRunning)
                return;

            defenseRunning = true;
            WorldMissionEventUtility.Invoke(defenseStarted, this);
            WorldMissionEventUtility.Invoke(DefenseStarted, this);
            PublishDefenseProgress();
            TryCompleteWhenReady();
        }

        public void StopDefense()
        {
            if (!defenseRunning)
                return;

            defenseRunning = false;
            WorldMissionEventUtility.Invoke(defenseStopped, this);
            WorldMissionEventUtility.Invoke(DefenseStopped, this);
        }

        /// <summary>
        /// Public spawn hook for an authored encounter or spawner adapter.
        /// Registering enables strict filtering when configured but never
        /// modifies the enemy object.
        /// </summary>
        public void NotifyEnemySpawned(GameObject enemy)
        {
            if (enemy != null && IsAvailable)
            {
                registeredEnemies.Add(enemy);
            }
        }

        /// <summary>
        /// Public defeat hook for authored encounter code. Passing the enemy
        /// reference prevents the same death from being counted twice.
        /// </summary>
        public void NotifyEnemyDefeated(GameObject enemy)
        {
            if (!CanCountKill())
                return;

            if (enemy == null)
            {
                RegisterKills(1);
                return;
            }

            if (requireSpawnRegistrationForKills &&
                !registeredEnemies.Contains(enemy))
            {
                RejectAction(
                    "The defeated enemy was not registered with this defense.");
                return;
            }

            if (!defeatedEnemies.Add(enemy))
                return;

            RegisterKills(1);
        }

        /// <summary>
        /// UnityEvent-friendly defeat hook for systems that cannot pass an
        /// enemy reference. Prefer NotifyEnemyDefeated(GameObject) when
        /// possible so duplicate notifications can be rejected.
        /// </summary>
        public void ReportEnemyDefeated()
        {
            RegisterKills(1);
        }

        public void RegisterKills(int amount)
        {
            if (!CanCountKill())
                return;

            if (amount <= 0)
            {
                RejectAction("Kill progress must be greater than zero.");
                return;
            }

            defeatedCount = Mathf.Min(
                defeatedCount + amount,
                RequiredKills);
            PublishDefenseProgress();
            TryCompleteWhenReady();
        }

        protected override bool CanComplete(out string reason)
        {
            bool durationMet = elapsedDuration >= RequiredDuration;
            bool killsMet = defeatedCount >= RequiredKills;
            bool complete;

            switch (completionMode)
            {
                case WorldMissionDefenseCompletionMode.DurationOnly:
                    complete = durationMet;
                    break;

                case WorldMissionDefenseCompletionMode.KillCountOnly:
                    complete = killsMet;
                    break;

                case WorldMissionDefenseCompletionMode.DurationOrKillCount:
                    complete = durationMet || killsMet;
                    break;

                default:
                    complete = durationMet && killsMet;
                    break;
            }

            reason = complete
                ? string.Empty
                : BuildRemainingRequirement(durationMet, killsMet);
            return complete;
        }

        protected override void ResetObjectiveProgress()
        {
            defenseRunning = false;
            elapsedDuration = 0f;
            defeatedCount = 0;
            lastPublishedSecond = -1;
            registeredEnemies.Clear();
            defeatedEnemies.Clear();
        }

        protected override void OnObjectiveActivated()
        {
            if (beginWhenObjectiveActivates)
            {
                BeginDefense();
            }
            else
            {
                PublishDefenseProgress();
            }
        }

        protected override void OnObjectiveCompleted()
        {
            defenseRunning = false;
            WorldMissionEventUtility.Invoke(defenseSatisfied, this);
        }

        protected override void OnObjectiveDeactivated()
        {
            StopDefense();
        }

        private bool CanCountKill()
        {
            if (!IsAvailable || !defenseRunning)
            {
                RejectAction("The defense is not currently running.");
                return false;
            }

            return true;
        }

        private void PublishDefenseProgress()
        {
            NotifyProgressChanged();
            WorldMissionEventUtility.Invoke(
                defenseProgressChanged,
                elapsedDuration,
                RequiredDuration,
                defeatedCount,
                RequiredKills,
                this);
            WorldMissionEventUtility.Invoke(
                DefenseProgressChanged,
                elapsedDuration,
                RequiredDuration,
                defeatedCount,
                RequiredKills,
                this);
        }

        private string BuildRemainingRequirement(
            bool durationMet,
            bool killsMet)
        {
            float secondsRemaining = Mathf.Max(
                0f,
                RequiredDuration - elapsedDuration);
            int killsRemaining = Mathf.Max(0, RequiredKills - defeatedCount);

            switch (completionMode)
            {
                case WorldMissionDefenseCompletionMode.DurationOnly:
                    return $"Defend for {secondsRemaining:0.0} more seconds.";

                case WorldMissionDefenseCompletionMode.KillCountOnly:
                    return $"Defeat {killsRemaining} more enemies.";

                case WorldMissionDefenseCompletionMode.DurationOrKillCount:
                    return
                        $"Defend for {secondsRemaining:0.0} seconds or " +
                        $"defeat {killsRemaining} more enemies.";

                default:
                    if (!durationMet && !killsMet)
                    {
                        return
                            $"Defend for {secondsRemaining:0.0} more seconds " +
                            $"and defeat {killsRemaining} more enemies.";
                    }

                    return !durationMet
                        ? $"Defend for {secondsRemaining:0.0} more seconds."
                        : $"Defeat {killsRemaining} more enemies.";
            }
        }
    }
}
