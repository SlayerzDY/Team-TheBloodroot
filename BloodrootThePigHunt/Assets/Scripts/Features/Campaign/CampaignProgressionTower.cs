using System.Collections;
using Bloodroot.Features.WorldMissions;
using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// The single, obvious interaction for one campaign area. The same
    /// cylinder owns the collider hit by Safety's Interact raycast, this
    /// adapter, and its one-step mission objective.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CampaignProgressionTower : MonoBehaviour, IInteract
    {
        private const int DurableReconciliationFrames = 120;

        [Header("Campaign")]
        [SerializeField] private CampaignStateService stateService;
        [SerializeField] private CampaignAreaId area;
        [SerializeField] private WorldMissionInteractionObjective objective;

        [Header("Interaction")]
        [SerializeField] private Collider interactionCollider;

        [Header("Hollow Finale")]
        [SerializeField] private HollowWitchSpawner hollowWitchSpawner;

        private CampaignStateService boundStateService;
        private Coroutine reconciliationRoutine;
        private bool isActivated;
        private string lastRejectionReason = string.Empty;

        public CampaignAreaId Area => area;
        public WorldMissionInteractionObjective Objective => objective;
        public Collider InteractionCollider => interactionCollider;
        public CampaignStateService StateService => ResolveStateService();
        public bool IsActivated =>
            isActivated || (objective != null && objective.IsComplete);
        public string LastRejectionReason => lastRejectionReason;

        private void Awake()
        {
            if (interactionCollider == null)
                interactionCollider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            BindStateService();
            if (objective != null)
                objective.StateChanged += HandleObjectiveStateChanged;
            RefreshInteractionPresentation();
            QueueReconciliation();
        }

        private void OnDisable()
        {
            if (objective != null)
                objective.StateChanged -= HandleObjectiveStateChanged;
            UnbindStateService();

            if (reconciliationRoutine != null)
            {
                StopCoroutine(reconciliationRoutine);
                reconciliationRoutine = null;
            }
        }

        public void Configure(
            CampaignStateService campaignState,
            CampaignAreaId campaignArea,
            WorldMissionInteractionObjective authoredObjective,
            Collider authoredInteractionCollider,
            HollowWitchSpawner authoredHollowSpawner = null)
        {
            if (isActiveAndEnabled && objective != null)
                objective.StateChanged -= HandleObjectiveStateChanged;

            stateService = campaignState;
            area = campaignArea;
            objective = authoredObjective;
            interactionCollider = authoredInteractionCollider;
            hollowWitchSpawner = authoredHollowSpawner;

            if (isActiveAndEnabled)
            {
                BindStateService();
                if (objective != null)
                    objective.StateChanged += HandleObjectiveStateChanged;
                QueueReconciliation();
            }
        }

        public void SendInteract(Collider target)
        {
            if (interactionCollider == null ||
                target != interactionCollider ||
                target.gameObject != gameObject)
            {
                Reject("The progression cylinder must receive its own authored collider.");
                return;
            }

            gameManager manager = gameManager.instance;
            if (manager == null || manager.player == null ||
                !manager.player.CompareTag("Player"))
            {
                Reject("The authoritative Player is unavailable.");
                return;
            }

            TryActivate();
        }

        public bool TryActivate()
        {
            if (IsActivated)
            {
                CommitActivatedPresentation();
                return true;
            }

            if (!ValidateRuntimeContract(out string contractError))
                return Reject(contractError);

            CampaignStateService state = ResolveStateService();
            BindStateService();

            if (!state.IsAreaUnlocked(area))
                return Reject("This campaign area is still locked.");

            if (!objective.IsAvailable || objective.Director == null ||
                objective.Director.CurrentObjective != objective)
            {
                return Reject(
                    "This progression cylinder is not the current campaign objective.");
            }

            if (area == CampaignAreaId.BloodrootHollow &&
                !state.TryActivateHollowTower())
            {
                return Reject(
                    "The Hollow progression cylinder could not save its activation.");
            }

            if (!objective.TryRegisterInteraction(
                    null,
                    interactionCollider,
                    out string objectiveRejection))
            {
                // Hollow's durable cylinder activation may already be saved.
                // A bounded reconciliation completes the transient objective
                // once the mission director reaches the authored state.
                QueueReconciliation();
                return Reject(string.IsNullOrWhiteSpace(objectiveRejection)
                    ? "The campaign objective rejected this activation."
                    : objectiveRejection);
            }

            CommitActivatedPresentation();
            QueueReconciliation();
            return true;
        }

        public bool ValidateRuntimeContract(out string error)
        {
            error = string.Empty;
            CampaignStateService state = ResolveStateService();
            if (state == null)
            {
                error = "The progression cylinder has no campaign state authority.";
                return false;
            }

            if (!System.Enum.IsDefined(typeof(CampaignAreaId), area))
            {
                error = "The progression cylinder has an invalid campaign area.";
                return false;
            }

            if (objective == null || objective.gameObject != gameObject ||
                objective.InteractionKind != WorldMissionInteractionKind.Generic ||
                objective.RequiredAmount != 1)
            {
                error =
                    "The cylinder must own one generic, one-step interaction objective.";
                return false;
            }

            Collider[] colliders = GetComponents<Collider>();
            if (interactionCollider == null ||
                interactionCollider.gameObject != gameObject ||
                colliders.Length != 1 || colliders[0] != interactionCollider ||
                !interactionCollider.enabled || interactionCollider.isTrigger)
            {
                error =
                    "The cylinder must own exactly one enabled solid interaction collider.";
                return false;
            }

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer < 0 || gameObject.layer != interactableLayer ||
                !CompareTag("Interact"))
            {
                error =
                    "The cylinder must use the Interact tag and Interactable layer.";
                return false;
            }

            bool isHollow = area == CampaignAreaId.BloodrootHollow;
            if (isHollow != (hollowWitchSpawner != null))
            {
                error = isHollow
                    ? "The Hollow cylinder requires its exact witch spawner."
                    : "Only the Hollow cylinder may reference a witch spawner.";
                return false;
            }

            return true;
        }

        private void HandleObjectiveStateChanged(WorldMissionObjective _)
        {
            RefreshInteractionPresentation();
            QueueReconciliation();
        }

        private void HandleProgressChanged(CampaignProgressSnapshot _)
        {
            RefreshInteractionPresentation();
            QueueReconciliation();
        }

        private void BindStateService()
        {
            CampaignStateService resolved = ResolveStateService();
            if (boundStateService == resolved)
                return;

            UnbindStateService();
            boundStateService = resolved;
            if (boundStateService != null)
            {
                boundStateService.ProgressLoaded += HandleProgressChanged;
                boundStateService.ProgressChanged += HandleProgressChanged;
            }
        }

        private void UnbindStateService()
        {
            if (boundStateService == null)
                return;

            boundStateService.ProgressLoaded -= HandleProgressChanged;
            boundStateService.ProgressChanged -= HandleProgressChanged;
            boundStateService = null;
        }

        private CampaignStateService ResolveStateService()
        {
            return CampaignStateService.Instance != null
                ? CampaignStateService.Instance
                : stateService;
        }

        private void QueueReconciliation()
        {
            if (!Application.isPlaying || !isActiveAndEnabled ||
                reconciliationRoutine != null)
                return;

            reconciliationRoutine = StartCoroutine(ReconcileDurableState());
        }

        private IEnumerator ReconcileDurableState()
        {
            yield return null;

            for (int frame = 0; frame < DurableReconciliationFrames; frame++)
            {
                BindStateService();
                CampaignStateService state = ResolveStateService();
                if (state == null || objective == null)
                {
                    yield return null;
                    continue;
                }

                bool hollowTowerSaved =
                    area == CampaignAreaId.BloodrootHollow &&
                    state.Current.HollowTowerActivated;

                if (!objective.IsComplete && hollowTowerSaved &&
                    objective.IsAvailable && objective.Director != null &&
                    objective.Director.CurrentObjective == objective)
                {
                    objective.TryRegisterInteraction(
                        null,
                        interactionCollider,
                        out _);
                }

                if (objective.IsComplete)
                {
                    CommitActivatedPresentation();
                    break;
                }

                yield return null;
            }

            reconciliationRoutine = null;
        }

        private void CommitActivatedPresentation()
        {
            isActivated = true;
            lastRejectionReason = string.Empty;
            RefreshInteractionPresentation();
        }

        private void RefreshInteractionPresentation()
        {
            if (!Application.isPlaying)
                return;

            bool canInteract = objective != null &&
                objective.IsAvailable && !IsActivated;
            int targetLayer = canInteract
                ? LayerMask.NameToLayer("Interactable")
                : LayerMask.NameToLayer("Default");
            if (targetLayer >= 0)
                gameObject.layer = targetLayer;

            gameObject.tag = canInteract ? "Interact" : "Untagged";
        }

        private bool Reject(string reason)
        {
            lastRejectionReason = string.IsNullOrWhiteSpace(reason)
                ? "The progression cylinder could not be activated."
                : reason.Trim();
            return false;
        }
    }
}
