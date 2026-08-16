using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Campaign-owned, serializable inventory-token data. Safety's legacy Item
    /// MonoScript cannot be cold-loaded from its protected prefabs in the
    /// current Unity version, so campaign assets serialize this plain owned
    /// component and adapt through Safety's public ItemStats API only at
    /// mutation time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampaignInventoryItemCarrier : MonoBehaviour
    {
        [SerializeField] public global::ItemStats item;
        [SerializeField] public bool canInteract = true;

        private void OnValidate()
        {
            canInteract = true;
        }
    }

    public static class CampaignInventoryTokenUtility
    {
        public static global::ItemStats GetItemStats(GameObject source)
        {
            if (source == null)
                return null;

            CampaignInventoryItemCarrier owned =
                source.GetComponent<CampaignInventoryItemCarrier>();
            if (owned?.item != null)
                return owned.item;

            return source.GetComponent<global::Item>()?.item;
        }

        public static global::ItemStats CloneItemStats(
            global::ItemStats source,
            int quantity)
        {
            if (source == null)
                return null;

            return new global::ItemStats
            {
                itemID = source.itemID,
                itemName = source.itemName,
                itemDescription = source.itemDescription,
                icon = source.icon,
                weight = source.weight,
                quantity = quantity,
                stackSize = source.stackSize,
                itemMesh = source.itemMesh,
                pickupSound = source.pickupSound,
                itemIncreases = source.itemIncreases
            };
        }
    }
}
