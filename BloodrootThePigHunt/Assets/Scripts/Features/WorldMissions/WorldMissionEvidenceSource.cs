using System;
using System.Collections;
using Bloodroot.Campaign;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.WorldMissions
{
    /// <summary>
    /// Durable, story-aware IInteract bridge for one authored evidence group.
    /// Campaign progress is committed before mission credit.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WorldMissionEvidenceSource : MonoBehaviour, IInteract
    {
        [Header("Mission Evidence")]
        [SerializeField] private WorldMissionInteractionObjective objective;
        [SerializeField] private CampaignAreaId area = CampaignAreaId.BlackPines;
        [SerializeField] private string evidenceId = string.Empty;
        [SerializeField] private string storyTitle = string.Empty;
        [SerializeField, TextArea(3, 8)] private string storyBody = string.Empty;
        [SerializeField] private WorldMissionObjectivePresenter presenter;

        [Header("Durable Presentation")]
        [SerializeField] private GameObject[] visibleBeforeCollectionRoots =
            Array.Empty<GameObject>();
        [SerializeField] private GameObject[] visibleAfterCollectionRoots =
            Array.Empty<GameObject>();
        [SerializeField] private Collider[] interactionColliders =
            Array.Empty<Collider>();

        [Header("Retired Award Migration")]
        [SerializeField] private string nameStoneId = string.Empty;
        [SerializeField] private GameObject nameStonePickupObject;
        [SerializeField] private Inventory inventory;

        [Header("Authored Feedback Hooks")]
        [SerializeField] private UnityEvent evidenceCollected = new();
        [SerializeField] private WorldMissionStringUnityEvent
            evidenceRejected = new();

        private CampaignStateService boundState;
        private WorldMissionInteractionObjective boundObjective;
        private bool durablyCollected;
        private bool objectiveCredited;

        // Inventory has no transaction API. If a save rejection cannot undo
        // the provisional token immediately, block further collection and
        // retry that exact rollback instead of compounding the discrepancy.
        private Inventory rollbackInventory;
        private ItemStats rollbackItem;
        private int rollbackTargetQuantity;
        private bool inventoryRollbackPending;

        public WorldMissionInteractionObjective Objective => objective;
        public CampaignAreaId Area => area;
        public string EvidenceId => evidenceId?.Trim() ?? string.Empty;
        public string StoryTitle => storyTitle?.Trim() ?? string.Empty;
        public string StoryBody => storyBody?.Trim() ?? string.Empty;
        public WorldMissionObjectivePresenter Presenter => presenter;
        public string NameStoneId => nameStoneId?.Trim() ?? string.Empty;
        public GameObject NameStonePickupObject => nameStonePickupObject;
        public bool IsDurablyCollected => durablyCollected;
        public bool IsObjectiveCredited => objectiveCredited;
        public bool HasPendingInventoryRollback => inventoryRollbackPending;

        private void OnEnable()
        {
            Bind();
            RefreshFromCampaignState();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private IEnumerator Start()
        {
            // Inventory.Start owns its backing-array initialization. A
            // no-snapshot editor start therefore retries durable-token repair
            // after every scene component has received Start.
            yield return null;
            if (isActiveAndEnabled)
            {
                Bind();
                RefreshFromCampaignState();
            }
        }

        private void OnValidate()
        {
            evidenceId = evidenceId?.Trim() ?? string.Empty;
            storyTitle = storyTitle?.Trim() ?? string.Empty;
            storyBody = storyBody?.Trim() ?? string.Empty;
            nameStoneId = string.Empty;
            nameStonePickupObject = null;
            inventory = null;
            visibleBeforeCollectionRoots ??= Array.Empty<GameObject>();
            visibleAfterCollectionRoots ??= Array.Empty<GameObject>();
            interactionColliders ??= Array.Empty<Collider>();
        }

        /// <summary>
        /// Exact editor-builder contract for one evidence group. Visual roots
        /// are toggled as a durable before/after pair; the source GameObject
        /// itself is deliberately never disabled.
        /// </summary>
        public void Configure(
            WorldMissionInteractionObjective missionObjective,
            CampaignAreaId campaignArea,
            string stableEvidenceId,
            string title,
            string body,
            WorldMissionObjectivePresenter objectivePresenter,
            GameObject[] visibleBeforeRoots,
            GameObject[] visibleAfterRoots,
            Collider[] authoredInteractionColliders,
            string awardedNameStoneId = null,
            GameObject authoredNameStonePickup = null,
            Inventory authoredInventory = null)
        {
            Unbind();
            objective = missionObjective;
            area = campaignArea;
            evidenceId = stableEvidenceId?.Trim() ?? string.Empty;
            storyTitle = title?.Trim() ?? string.Empty;
            storyBody = body?.Trim() ?? string.Empty;
            presenter = objectivePresenter;
            visibleBeforeCollectionRoots = visibleBeforeRoots ??
                                             Array.Empty<GameObject>();
            visibleAfterCollectionRoots = visibleAfterRoots ??
                                            Array.Empty<GameObject>();
            interactionColliders = authoredInteractionColliders ??
                                   Array.Empty<Collider>();
            nameStoneId = string.Empty;
            nameStonePickupObject = null;
            inventory = null;
            durablyCollected = false;
            objectiveCredited = false;
            ClearRollbackState();

            if (isActiveAndEnabled)
            {
                Bind();
                RefreshFromCampaignState();
            }
            else
            {
                ApplyCollectedPresentation(false);
            }
        }

        public void SendInteract(Collider target)
        {
            Bind();

            if (!TryRepairPendingInventoryRollback())
            {
                Reject(
                    "Evidence recovery is waiting for an inventory rollback. " +
                    "Free inventory space or reload the mission before retrying.");
                return;
            }

            CampaignStateService state = boundState;
            if (state == null)
            {
                Reject("Campaign progress is not available.");
                return;
            }

            RefreshFromCampaignState();
            if (durablyCollected)
            {
                TryCreditDurableObjective(target);
                return;
            }

            if (!ValidateAuthoredContract(state, out string validationError))
            {
                Reject(validationError);
                return;
            }

            if (objective == null)
            {
                Reject("This evidence has no mission objective assigned.");
                return;
            }

            if (objective.IsComplete)
            {
                Reject("This evidence objective is already complete.");
                return;
            }

            if (!objective.IsAvailable)
            {
                Reject("This evidence is not part of the current objective.");
                return;
            }

            if (!state.TryRecordEvidence(EvidenceId, area))
            {
                Reject("Evidence could not be saved. Please try again.");
                return;
            }

            // ProgressChanged may already have credited the objective while
            // the durable save completed. This call is intentionally
            // idempotent and covers integrations without that notification.
            RefreshFromCampaignState();
            TryCreditDurableObjective(target);

            if (presenter != null)
            {
                presenter.ShowEvidenceCollected(StoryTitle, StoryBody);
            }

            WorldMissionEventUtility.Invoke(evidenceCollected, this);
        }

        private void Bind()
        {
            CampaignStateService currentState = CampaignStateService.Instance;
            if (boundState != currentState)
            {
                if (boundState != null)
                {
                    boundState.ProgressLoaded -= HandleProgressChanged;
                    boundState.ProgressChanged -= HandleProgressChanged;
                }

                boundState = currentState;
                if (boundState != null)
                {
                    boundState.ProgressLoaded += HandleProgressChanged;
                    boundState.ProgressChanged += HandleProgressChanged;
                }
            }

            if (boundObjective == objective)
                return;

            if (boundObjective != null)
            {
                boundObjective.StateChanged -= HandleObjectiveStateChanged;
            }

            boundObjective = objective;
            if (boundObjective != null)
            {
                boundObjective.StateChanged += HandleObjectiveStateChanged;
            }
        }

        private void Unbind()
        {
            if (boundState != null)
            {
                boundState.ProgressLoaded -= HandleProgressChanged;
                boundState.ProgressChanged -= HandleProgressChanged;
            }

            if (boundObjective != null)
            {
                boundObjective.StateChanged -= HandleObjectiveStateChanged;
            }

            boundState = null;
            boundObjective = null;
        }

        private void HandleProgressChanged(CampaignProgressSnapshot snapshot)
        {
            RefreshFromCampaignState();
        }

        private void HandleObjectiveStateChanged(
            WorldMissionObjective changedObjective)
        {
            if (changedObjective == null)
                return;

            if (changedObjective.State == WorldMissionObjectiveState.Inactive)
            {
                objectiveCredited = false;
                return;
            }

            if (changedObjective.State == WorldMissionObjectiveState.Available)
            {
                TryCreditDurableObjective(PrimaryInteractionCollider());
            }
        }

        private void RefreshFromCampaignState()
        {
            CampaignStateService state = boundState ??
                                         CampaignStateService.Instance;
            string id = EvidenceId;
            bool evidenceSaved = state != null &&
                                  state.IsEvidenceCollected(id);
            durablyCollected = evidenceSaved;
            ApplyCollectedPresentation(durablyCollected);

            if (!durablyCollected || state == null)
                return;

            TryCreditDurableObjective(PrimaryInteractionCollider());
        }

        private bool ValidateAuthoredContract(
            CampaignStateService state,
            out string error)
        {
            string id = EvidenceId;
            if (!Enum.IsDefined(typeof(CampaignAreaId), area) ||
                !CampaignEvidenceIds.TryGetArea(
                    id,
                    out CampaignAreaId canonicalArea) ||
                canonicalArea != area)
            {
                error =
                    "This evidence source does not have a canonical ID for its area.";
                return false;
            }

            if (!state.IsAreaUnlocked(area))
            {
                error = "This campaign area is still locked.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryGrantNameStoneToken(
            out Inventory targetInventory,
            out ItemStats item,
            out int startingQuantity,
            out string error)
        {
            targetInventory = ResolveInventory();
            startingQuantity = 0;
            if (!TryGetValidNameStoneItem(out item, out error))
                return false;

            if (targetInventory == null || !targetInventory.IsValidIndex(0))
            {
                error = "Player inventory is not initialized.";
                return false;
            }

            startingQuantity = Mathf.Max(
                0,
                targetInventory.FindItem(item).Value);
            bool hasEmptySlot = false;
            for (int index = 0;
                 index < 4096 && targetInventory.IsValidIndex(index);
                 index++)
            {
                if (targetInventory.IsSlotEmpty(index))
                {
                    hasEmptySlot = true;
                    break;
                }
            }

            if (!hasEmptySlot)
            {
                error = "Inventory is full. Free one slot for the Name Stone.";
                return false;
            }

            try
            {
                targetInventory.AddItem(
                    CampaignInventoryTokenUtility.CloneItemStats(
                        item,
                        1));

                int awardedQuantity = Mathf.Max(
                    0,
                    targetInventory.FindItem(item).Value);
                if (awardedQuantity != startingQuantity + 1)
                {
                    TryRollbackToken(
                        targetInventory,
                        item,
                        startingQuantity);
                    error =
                        "Inventory could not accept exactly one Name Stone.";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                TryRollbackToken(targetInventory, item, startingQuantity);

                error = "Inventory could not accept the Name Stone.";
                return false;
            }
        }

        private bool TryGetValidNameStoneItem(
            out ItemStats item,
            out string error)
        {
            item = CampaignInventoryTokenUtility.GetItemStats(
                nameStonePickupObject);
            if (item == null ||
                !string.Equals(
                    item.itemName?.Trim(),
                    "CursedItem",
                    StringComparison.Ordinal) ||
                item.quantity != 1 ||
                item.stackSize != 1)
            {
                error =
                    "Name Stone evidence requires the authored CursedItem " +
                    "pickup with quantity 1 and stack size 1.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private Inventory ResolveInventory()
        {
            if (inventory != null)
                return inventory;

            GameObject player = gameManager.instance != null
                ? gameManager.instance.player
                : null;
            return player != null ? player.GetComponent<Inventory>() : null;
        }

        private void TryReconcileDurableNameStoneToken(
            CampaignStateService state)
        {
            if (NameStoneId.Length == 0 ||
                !string.IsNullOrEmpty(state.PendingNameStoneOfferId) ||
                state.IsNameStoneOffered(NameStoneId) ||
                !TryGetValidNameStoneItem(out _, out _))
            {
                return;
            }

            Inventory targetInventory = ResolveInventory();
            CampaignInventoryCarryover carryover =
                state.GetComponent<CampaignInventoryCarryover>();
            if (targetInventory != null && carryover != null &&
                !carryover.HasSnapshot)
            {
                // A durable travel snapshot is reconciled by carryover only
                // after its exact destination restore. Skipping it here avoids
                // racing that empty-inventory prerequisite during scene load.
                carryover.TryReconcileExtractedNameStoneTokens(
                    nameStonePickupObject,
                    targetInventory);
            }
        }

        private bool TryCreditDurableObjective(Collider sourceCollider)
        {
            if (!durablyCollected || objectiveCredited || objective == null)
                return false;

            if (objective.IsComplete)
            {
                // A matching objective count should normally credit every
                // durable source. Treat completion as terminal to avoid noisy
                // repeat rejection if an authored count is too small.
                objectiveCredited = true;
                return true;
            }

            if (!objective.IsAvailable)
                return false;

            if (!objective.TryRegisterInteraction(
                    null,
                    sourceCollider,
                    out _))
            {
                return false;
            }

            objectiveCredited = true;
            return true;
        }

        private bool TryRollbackToken(
            Inventory targetInventory,
            ItemStats item,
            int targetQuantity)
        {
            if (targetInventory == null || item == null)
                return false;

            try
            {
                int current = Mathf.Max(
                    0,
                    targetInventory.FindItem(item).Value);
                if (current > targetQuantity)
                {
                    targetInventory.RemoveItem(
                        item.itemName,
                        current - targetQuantity,
                        false);
                }

                current = Mathf.Max(
                    0,
                    targetInventory.FindItem(item).Value);
                if (current == targetQuantity)
                {
                    ClearRollbackState();
                    return true;
                }
            }
            catch (Exception exception)
            {

            }

            rollbackInventory = targetInventory;
            rollbackItem = item;
            rollbackTargetQuantity = Mathf.Max(0, targetQuantity);
            inventoryRollbackPending = true;
            return false;
        }

        private bool TryRepairPendingInventoryRollback()
        {
            if (!inventoryRollbackPending)
                return true;

            return TryRollbackToken(
                rollbackInventory,
                rollbackItem,
                rollbackTargetQuantity);
        }

        private void ClearRollbackState()
        {
            rollbackInventory = null;
            rollbackItem = null;
            rollbackTargetQuantity = 0;
            inventoryRollbackPending = false;
        }

        private void ApplyCollectedPresentation(bool collected)
        {
            SetRootsActive(visibleBeforeCollectionRoots, !collected);
            SetRootsActive(visibleAfterCollectionRoots, collected);

            Collider[] colliders = interactionColliders;
            bool appliedAuthoredCollider = false;
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider authoredCollider = colliders[index];
                if (authoredCollider == null)
                    continue;

                authoredCollider.enabled = !collected;
                appliedAuthoredCollider = true;
            }

            if (!appliedAuthoredCollider)
            {
                Collider fallback = GetComponent<Collider>();
                if (fallback != null)
                {
                    fallback.enabled = !collected;
                }
            }
        }

        private void SetRootsActive(GameObject[] roots, bool active)
        {
            if (roots == null)
                return;

            for (int index = 0; index < roots.Length; index++)
            {
                GameObject root = roots[index];
                if (root != null && root != gameObject &&
                    root.activeSelf != active)
                {
                    root.SetActive(active);
                }
            }
        }

        private Collider PrimaryInteractionCollider()
        {
            if (interactionColliders != null)
            {
                for (int index = 0;
                     index < interactionColliders.Length;
                     index++)
                {
                    if (interactionColliders[index] != null)
                    {
                        return interactionColliders[index];
                    }
                }
            }

            return GetComponent<Collider>();
        }

        private void Reject(string reason)
        {
            string message = string.IsNullOrWhiteSpace(reason)
                ? "That evidence cannot be collected right now."
                : reason.Trim();

            if (presenter != null && presenter.isActiveAndEnabled)
            {
                presenter.ShowRejectedStatus(message);
            }

            WorldMissionEventUtility.Invoke(
                evidenceRejected,
                message,
                this);
        }
    }
}
