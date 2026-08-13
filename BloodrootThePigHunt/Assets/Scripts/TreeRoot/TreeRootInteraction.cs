using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TreeRootInteraction : MonoBehaviour, IInteract
{
    //public variables
    public GameObject interactionUIPanel;
    public Image uiItemIcon;
    public TextMeshProUGUI uiCostText;
    public Sprite itemSprite;
    public string requiredItemName = "CursedItem";
    public int itemsRequiredToFeed = 1;
    //private variables
    private Inventory playerInventory;
    private bool isFeeding = false;


    private void Start()
    {
        interactionUIPanel = GameObject.Find("TreeInteractionUI");
        if (interactionUIPanel != null)
        {

            uiItemIcon = interactionUIPanel.transform.Find("TreeIcon").GetComponent<Image>();
            uiCostText = interactionUIPanel.transform.Find("Cost").GetComponent<TextMeshProUGUI>();

        }
        else
        {
            Debug.LogError("TreeRootInteraction: Could not automatically find 'TreeInteractionUI' in the scene hierarchy");
        }

        HideTreeUI();

        playerInventory = FindAnyObjectByType<Inventory>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {

            if (gameManager.instance != null && !gameManager.instance.isDefenseActive)
            {
                UpdateAndShowTreeUI();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HideTreeUI();
        }
    }

    public void SendInteract(Collider target)
    {
        if (gameManager.instance.isDefenseActive)
        {
            Debug.Log("Tree is locked during base defense");
            return;
        }

        TryFeedTree();
    }

    private void TryFeedTree()
    {
        if (isFeeding) { return; }

        if (playerInventory == null || playerInventory.inventoryItems == null)
        {
            Debug.LogError("Cannot feed tree: Inventory component is missing reference");
            return;
        }

        int currentCount = GetCurrentItemCount();
        if (currentCount >= itemsRequiredToFeed)
        {
            isFeeding = true;
            int itemsRemoved = 0;

            for (int i = playerInventory.inventoryItems.Length - 1; i >= 0; i--)
            {
                ItemStats currentSlotItem = playerInventory.inventoryItems[i];

                if (currentSlotItem != null && currentSlotItem.itemName == requiredItemName)
                {
                    playerInventory.inventoryItems[i] = null;
                    itemsRemoved++;

                    if (itemsRemoved >= itemsRequiredToFeed)
                    {
                        break;
                    }
                }
            }

            gameManager.instance.AddTreeItem();
            if (!gameManager.instance.isDefenseActive)
            {
                UpdateAndShowTreeUI();
            }
            Invoke(nameof(ResetFeeding), 0.5f);

            Debug.Log($"Manually fed {itemsRequiredToFeed}x {requiredItemName} to the tree!");
        }
        else
        {
            Debug.Log($"Cannot feed: You only have {currentCount}/{itemsRequiredToFeed} {requiredItemName}s.");
        }
    }

    private int GetCurrentItemCount()
    {
        if (playerInventory == null || playerInventory.inventoryItems == null) return 0;

        int count = 0;
        foreach (ItemStats item in playerInventory.inventoryItems)
        {
            if (item != null && item.itemName == requiredItemName)
            {
                count++;
            }
        }
        return count;
    }

    public void UpdateAndShowTreeUI()
    {
        if (interactionUIPanel != null)
        {
            if (uiItemIcon != null && itemSprite != null) uiItemIcon.sprite = itemSprite;

            if (uiCostText != null)
            {
                int currentCount = GetCurrentItemCount();
                uiCostText.text = $"Feed Tree {currentCount}/{itemsRequiredToFeed}";
            }

            interactionUIPanel.SetActive(true);
        }
    }

    public void HideTreeUI()
    {
        if (interactionUIPanel != null)
        {
            interactionUIPanel.SetActive(false);
        }
    }

    private void ResetFeeding()
    {

        isFeeding = false;

    }

}
