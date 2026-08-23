using System;
using System.Collections;
using Bloodroot.Campaign;
using Bloodroot.Features.AlphaEnemies;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.WorldMissions
{
    [Serializable]
    public sealed class HollowWitchSpawnerStringEvent : UnityEvent<string>
    {
    }

    /// <summary>
    /// One-shot threshold adapter behind the Hollow thorn veil. It activates
    /// authored encounter content and starts the existing scene-authored
    /// defense/director; it never instantiates witches at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class HollowWitchSpawner : MonoBehaviour
    {
        [Header("Campaign Gate")]
        [SerializeField] private CampaignStateService stateService;
        [SerializeField] private CampaignThornVeilGate thornVeilGate;

        [Header("Scene-Authored Encounter")]
        [SerializeField] private WorldMissionInteractionObjective
            crossingObjective;
        [SerializeField] private GameObject encounterContent;
        [SerializeField] private WorldMissionDefenseObjective defenseObjective;
        [SerializeField] private WorldMissionInteractionObjective
            heartrootObjective;
        [SerializeField] private WitchEncounterDirector encounterDirector;

        [Header("Threshold")]
        [SerializeField] private Collider triggerCollider;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool disableTriggerAfterStart = true;

        [Header("Authored Events")]
        [SerializeField] private UnityEvent encounterStarted = new UnityEvent();
        [SerializeField] private HollowWitchSpawnerStringEvent startRejected =
            new HollowWitchSpawnerStringEvent();

        private bool hasStarted;
        private string lastRejectionReason = string.Empty;
        private Coroutine completedMissionResumeRoutine;

        public bool HasStarted => hasStarted;
        public string LastRejectionReason => lastRejectionReason;
        public GameObject EncounterContent => encounterContent;
        public WorldMissionInteractionObjective CrossingObjective =>
            crossingObjective;
        public WorldMissionDefenseObjective DefenseObjective =>
            defenseObjective;
        public WitchEncounterDirector EncounterDirector => encounterDirector;
        public CampaignThornVeilGate ThornVeilGate => thornVeilGate;

        private void Awake()
        {
            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryStartEncounter(other);
        }

        public bool TryStartEncounter(Collider enteringCollider)
        {
            return TryStartOrResumeEncounter(
                enteringCollider,
                durableResume: false);
        }

        public bool TryResumeDurableEncounter()
        {
            CampaignStateService state = ResolveStateService();
            if (state == null || !state.Current.HollowVeilCrossed)
                return false;

            return TryStartOrResumeEncounter(
                null,
                durableResume: true);
        }

        private bool TryStartOrResumeEncounter(
            Collider enteringCollider,
            bool durableResume)
        {
            if (hasStarted)
                return false;

            if (!durableResume && !IsPlayerCollider(enteringCollider))
            {
                return Reject("Only the Player can start the Hollow encounter.");
            }

            CampaignStateService state = ResolveStateService();
            if (state == null || !state.Current.CanEnterHollow)
            {
                return Reject(
                    "Activate the final progression cylinder after Harrow before entering the Hollow.");
            }

            bool finaleAlreadyRecovered =
                state.Current.HeartrootCarried;
            if (!durableResume && (state.Current.HeartrootCarried ||
                state.Current.HeartrootBurned ||
                state.Current.CampaignCompleted))
            {
                return Reject(
                    "The exposed Heartroot has already left the Hollow encounter.");
            }

            if (thornVeilGate == null || !thornVeilGate.RefreshGate())
            {
                return Reject("The Hollow thorn veil is still closed.");
            }

            if (triggerCollider == null || !triggerCollider.isTrigger)
            {
                return Reject(
                    "The Hollow witch spawner requires an authored trigger collider.");
            }

            if (crossingObjective == null || encounterContent == null ||
                defenseObjective == null || encounterDirector == null)
            {
                return Reject(
                    "The Hollow witch spawner is missing authored encounter references.");
            }

            if (!encounterDirector.ValidateRuntimeContract(
                    out string encounterContractError))
            {
                return Reject(encounterContractError);
            }

            WorldMissionDirector missionDirector = crossingObjective.Director;
            if (missionDirector == null)
            {
                return Reject(
                    "The thorn-veil crossing objective has no mission director.");
            }

            bool crossingIsCurrent =
                crossingObjective.IsAvailable &&
                missionDirector.CurrentObjective == crossingObjective;
            bool crossingAlreadyAdvanced =
                crossingObjective.IsComplete &&
                missionDirector.CurrentObjective == defenseObjective &&
                defenseObjective.IsAvailable;
            if (!crossingIsCurrent && !crossingAlreadyAdvanced)
            {
                return Reject(
                    "Crossing the thorn veil is not the current Hollow objective.");
            }

            // The durable crossing is the authority for encounter resume.
            // Save it before content activation, objective mutation, or the
            // director's first witch can become visible.
            if (!state.TryMarkHollowVeilCrossed())
            {
                return Reject(
                    "The thorn-veil crossing could not be saved; no witch was activated.");
            }

            bool contentWasActive = encounterContent.activeSelf;
            bool defenseWasRunning = defenseObjective.IsDefenseRunning;
            encounterContent.SetActive(true);

            if (crossingIsCurrent &&
                !crossingObjective.TryRegisterInteraction(
                    null,
                    enteringCollider,
                    out string crossingRejection))
            {
                if (!contentWasActive)
                {
                    encounterContent.SetActive(false);
                }

                return Reject(string.IsNullOrWhiteSpace(crossingRejection)
                    ? "The thorn-veil crossing objective rejected the Player."
                    : crossingRejection);
            }

            if (!crossingObjective.IsComplete ||
                missionDirector.CurrentObjective != defenseObjective ||
                !defenseObjective.IsAvailable)
            {
                if (!contentWasActive)
                {
                    encounterContent.SetActive(false);
                }

                return Reject(
                    "The Hollow mission did not advance from the crossing objective to witch defense.");
            }

            if (!defenseObjective.IsDefenseRunning)
            {
                defenseObjective.BeginDefense();
            }
            if (!finaleAlreadyRecovered &&
                encounterDirector.State == WitchEncounterState.Idle)
            {
                encounterDirector.BeginEncounter();
            }

            int durableDefeats = Mathf.Clamp(
                state.Current.DefeatedWitchCount,
                0,
                3);
            if (defenseObjective.IsDefenseRunning &&
                defenseObjective.DefeatedCount < durableDefeats)
            {
                defenseObjective.RegisterKills(
                    durableDefeats - defenseObjective.DefeatedCount);
            }

            bool started = finaleAlreadyRecovered
                ? durableDefeats == 3
                : durableDefeats < 3
                ? defenseObjective.IsDefenseRunning &&
                  encounterDirector.State == WitchEncounterState.Defending
                : state.Current.HeartrootExposed &&
                  encounterDirector.State ==
                  WitchEncounterState.AwaitingExtraction;
            if (!started)
            {
                if (!defenseWasRunning &&
                    defenseObjective.IsDefenseRunning)
                {
                    defenseObjective.StopDefense();
                }

                if (!contentWasActive)
                {
                    encounterContent.SetActive(false);
                }

                return Reject(
                    "The authored Hollow defense and witch director did not start together.");
            }

            hasStarted = true;
            lastRejectionReason = string.Empty;
            if (disableTriggerAfterStart && triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }

            if (finaleAlreadyRecovered)
            {
                if (completedMissionResumeRoutine != null)
                    StopCoroutine(completedMissionResumeRoutine);
                completedMissionResumeRoutine = StartCoroutine(
                    ResumeCompletedHollowMission());
            }
            else
            {
                encounterStarted.Invoke();
            }

            return true;
        }

        private IEnumerator ResumeCompletedHollowMission()
        {
            float timeoutAt = Time.realtimeSinceStartup + 5f;
            WorldMissionDirector missionDirector = crossingObjective != null
                ? crossingObjective.Director
                : null;
            while (missionDirector != null && heartrootObjective != null &&
                   missionDirector.CurrentObjective != heartrootObjective &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            if (missionDirector != null && heartrootObjective != null &&
                missionDirector.CurrentObjective == heartrootObjective &&
                heartrootObjective.IsAvailable &&
                !heartrootObjective.IsComplete)
            {
                heartrootObjective.TryRegisterInteraction(
                    null,
                    null,
                    out _);
            }

            if (encounterContent != null)
                encounterContent.SetActive(false);
            completedMissionResumeRoutine = null;
        }

        public void Configure(
            CampaignStateService campaignState,
            CampaignThornVeilGate veilGate,
            WorldMissionInteractionObjective authoredCrossingObjective,
            GameObject authoredEncounterContent,
            WorldMissionDefenseObjective authoredDefenseObjective,
            WitchEncounterDirector authoredEncounterDirector,
            Collider authoredTriggerCollider)
        {
            Configure(
                campaignState,
                veilGate,
                authoredCrossingObjective,
                authoredEncounterContent,
                authoredDefenseObjective,
                null,
                authoredEncounterDirector,
                authoredTriggerCollider);
        }

        public void Configure(
            CampaignStateService campaignState,
            CampaignThornVeilGate veilGate,
            WorldMissionInteractionObjective authoredCrossingObjective,
            GameObject authoredEncounterContent,
            WorldMissionDefenseObjective authoredDefenseObjective,
            WorldMissionInteractionObjective authoredHeartrootObjective,
            WitchEncounterDirector authoredEncounterDirector,
            Collider authoredTriggerCollider)
        {
            stateService = campaignState;
            thornVeilGate = veilGate;
            crossingObjective = authoredCrossingObjective;
            encounterContent = authoredEncounterContent;
            defenseObjective = authoredDefenseObjective;
            heartrootObjective = authoredHeartrootObjective;
            encounterDirector = authoredEncounterDirector;
            triggerCollider = authoredTriggerCollider != null
                ? authoredTriggerCollider
                : GetComponent<Collider>();
            hasStarted = false;
            lastRejectionReason = string.Empty;
        }

        private CampaignStateService ResolveStateService()
        {
            CampaignStateService persistent =
                CampaignStateService.Instance;
            if (persistent != null && persistent != stateService)
                stateService = persistent;

            return stateService;
        }

        private bool IsPlayerCollider(Collider candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(playerTag))
                return false;

            if (global::gameManager.instance != null &&
                global::gameManager.instance.player != null &&
                (candidate.gameObject == global::gameManager.instance.player ||
                 candidate.transform.IsChildOf(
                     global::gameManager.instance.player.transform)))
            {
                return true;
            }

            Transform current = candidate.transform;
            while (current != null)
            {
                try
                {
                    if (current.CompareTag(playerTag))
                        return true;
                }
                catch (UnityException exception)
                {
                    exception = exception;
                    return false;
                }

                current = current.parent;
            }

            return false;
        }

        private bool Reject(string reason)
        {
            lastRejectionReason = string.IsNullOrWhiteSpace(reason)
                ? "The Hollow witch encounter could not start."
                : reason.Trim();
            startRejected.Invoke(lastRejectionReason);
            return false;
        }

        private void OnValidate()
        {
            playerTag = playerTag?.Trim() ?? string.Empty;
            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider>();
            }
        }
    }
}
