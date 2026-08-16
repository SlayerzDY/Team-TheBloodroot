using System;
using Bloodroot.Campaign;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.AlphaEnemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class AlphaLootPickup : MonoBehaviour, global::IInteract
    {
        [Header("Authored Item")]
        [Tooltip("Authored campaign token carrying ItemStats. The runtime passes a cloned ItemStats value to Safety's public Inventory API.")]
        [SerializeField] private GameObject inventoryPickupObject;
        [SerializeField] private global::Inventory playerInventory;
        [SerializeField] private bool resolveInventoryFromGameManager = true;

        [Header("Accepted Pickup Lifetime")]
        [SerializeField] private bool destroyAfterAcceptance = true;
        [SerializeField, Min(0f)] private float destroyDelay;
        [SerializeField] private Collider[] interactionColliders;

        [Header("Authored Presentation")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip acceptedClip;

        [Header("Authored Events")]
        [SerializeField] private UnityEvent onAccepted = new UnityEvent();
        [SerializeField] private UnityEvent onRejected = new UnityEvent();

        private bool accepted;

        public global::ItemStats Item => ResolveItemStats();
        public bool WasAccepted => accepted;

        private void Awake()
        {
            CacheCollidersIfNeeded();
        }

        public void SendInteract(Collider target)
        {
            global::Inventory inventory = ResolveInventory();
            if (!TryCollect(inventory))
            {
                AlphaEnemyEventUtility.Invoke(onRejected, this, nameof(onRejected));
            }
        }

        public bool TryCollect(global::Inventory inventory)
        {
            if (accepted || inventory == null)
            {
                return false;
            }

            global::ItemStats item = ResolveItemStats();
            if (item == null || inventoryPickupObject == null)
            {
                return false;
            }

            int quantityBefore = inventory.FindItem(item).Value;
            if (!HasCapacityForWholePickup(inventory, item, quantityBefore))
            {
                return false;
            }

            try
            {
                inventory.AddItem(
                    CampaignInventoryTokenUtility.CloneItemStats(
                        item,
                        item.quantity));
            }
            catch (Exception exception)
            {
                bool rolledBack = RollBackUnexpectedGrant(
                    inventory,
                    item,
                    quantityBefore);
                Debug.LogError(
                    rolledBack
                        ? $"{name}: Inventory.AddItem threw; any observable partial grant was rolled back and the loot pickup was left in the world."
                        : $"{name}: Inventory.AddItem threw and the approved public hooks could not verify a full rollback. The loot pickup remains active for diagnosis.",
                    this);
                Debug.LogException(exception, this);
                return false;
            }

            int acceptedQuantity = inventory.FindItem(item).Value - quantityBefore;
            if (acceptedQuantity != item.quantity)
            {
                bool rolledBack = RollBackUnexpectedGrant(
                    inventory,
                    item,
                    quantityBefore);
                Debug.LogError(
                    rolledBack
                        ? $"{name}: Inventory accepted {Mathf.Max(0, acceptedQuantity)} of {item.quantity} configured items after a conservative public-API capacity preflight. The unexpected grant was rolled back and the pickup remains active."
                        : $"{name}: Inventory accepted {Mathf.Max(0, acceptedQuantity)} of {item.quantity} configured items and the approved public hooks could not restore the starting quantity. The pickup remains active for diagnosis.",
                    this);
                return false;
            }

            accepted = true;
            SetCollidersEnabled(false);
            if (audioSource != null && acceptedClip != null)
            {
                audioSource.PlayOneShot(acceptedClip);
            }

            AlphaEnemyEventUtility.Invoke(onAccepted, this, nameof(onAccepted));
            if (destroyAfterAcceptance)
            {
                Destroy(gameObject, destroyDelay);
            }
            else
            {
                gameObject.SetActive(false);
            }

            return true;
        }

        private global::Inventory ResolveInventory()
        {
            if (playerInventory != null)
            {
                return playerInventory;
            }

            if (!resolveInventoryFromGameManager || global::gameManager.instance == null ||
                global::gameManager.instance.player == null)
            {
                return null;
            }

            playerInventory = global::gameManager.instance.player.GetComponent<global::Inventory>();
            return playerInventory;
        }

        private bool HasCapacityForWholePickup(
            global::Inventory inventory,
            global::ItemStats item,
            int currentQuantity)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.itemName) ||
                item.quantity <= 0 || item.stackSize <= 0)
            {
                return false;
            }

            // Inventory.AddItem drops an item into the world when
            // every slot is occupied before it attempts stacking. Requiring
            // enough empty slots for the remainder after the packed-stack
            // capacity reported by FindItem is conservative, but guarantees
            // this interaction cannot duplicate drops or leave a partial add
            // while using only the approved public hooks. At least one empty
            // slot is mandatory because AddItem checks fullness before it
            // attempts to top off an existing stack.
            int usedInLastStack = Mathf.Max(0, currentQuantity) % item.stackSize;
            int freeInLastStack = usedInLastStack == 0
                ? 0
                : item.stackSize - usedInLastStack;
            int quantityNeedingNewSlots = Mathf.Max(
                0,
                item.quantity - freeInLastStack);
            int requiredEmptySlots = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    quantityNeedingNewSlots / (float)item.stackSize));
            int emptySlots = 0;
            for (int index = 0;
                 index < 4096 && inventory.IsValidIndex(index);
                 index++)
            {
                if (inventory.IsSlotEmpty(index))
                {
                    emptySlots++;
                    if (emptySlots >= requiredEmptySlots)
                        return true;
                }
            }

            return false;
        }

        private bool RollBackUnexpectedGrant(
            global::Inventory inventory,
            global::ItemStats item,
            int startingQuantity)
        {
            if (inventory == null || item == null ||
                string.IsNullOrWhiteSpace(item.itemName))
            {
                return false;
            }

            try
            {
                int currentQuantity = inventory.FindItem(item).Value;
                int unexpectedQuantity = Mathf.Max(
                    0,
                    currentQuantity - Mathf.Max(0, startingQuantity));
                if (unexpectedQuantity > 0)
                {
                    inventory.RemoveItem(
                        item.itemName.Trim(),
                        unexpectedQuantity,
                        false);
                }

                return inventory.FindItem(item).Value == Mathf.Max(
                    0,
                    startingQuantity);
            }
            catch (Exception rollbackException)
            {
                Debug.LogError(
                    $"{name}: Inventory rollback threw: {rollbackException.Message}",
                    this);
                return false;
            }
        }

        private global::ItemStats ResolveItemStats()
        {
            return CampaignInventoryTokenUtility.GetItemStats(
                inventoryPickupObject);
        }

        private void CacheCollidersIfNeeded()
        {
            if (interactionColliders == null || interactionColliders.Length == 0)
            {
                interactionColliders = GetComponentsInChildren<Collider>(true);
            }
        }

        private void SetCollidersEnabled(bool enabledValue)
        {
            CacheCollidersIfNeeded();
            foreach (Collider interactionCollider in interactionColliders)
            {
                if (interactionCollider != null)
                {
                    interactionCollider.enabled = enabledValue;
                }
            }
        }

        private void OnValidate()
        {
            destroyDelay = Mathf.Max(0f, destroyDelay);
        }
    }
}
