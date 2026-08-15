using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class inventoryItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI")]
    public Image image;
    [HideInInspector] public Transform parentAfterDrag;

    [Header("Item")]
    [SerializeField] public ItemStats itemStats;
    [SerializeField] public int index;
    [SerializeField] public Inventory inventory;
    [SerializeField] public Sprite defaultImage;
    [SerializeField] public GameObject dropToRemoveTarget;

    // Set true when this item was removed via dropToRemoveTarget - skips the
    // reparent below since there's no slot to snap back into.
    [HideInInspector] public bool consumed = false;

    [HideInInspector] private Canvas canvas;
    [HideInInspector] private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (image == null) image = GetComponent<Image>();
        canvas = GetComponentInParent<Canvas>();
        RefreshIcon();
    }

    // Call this after itemStats/index change (initial spawn, or after a slot
    // move/swap) so the displayed sprite always matches the inventory data.
    public void RefreshIcon()
    {
        if (itemStats == null || string.IsNullOrEmpty(itemStats.itemName)) { return; }
        image.sprite = itemStats.icon != null ? itemStats.icon : defaultImage;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (image != null) { image.raycastTarget = false; }
        parentAfterDrag = transform.parent;
        if (canvas != null)
        {
            transform.SetParent(canvas.transform);
            transform.SetAsLastSibling();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (consumed) { return; }

        if (dropToRemoveTarget != null && eventData.pointerCurrentRaycast.gameObject == dropToRemoveTarget)
        {
            RemoveFromInventory();
            return;
        }

        if (image != null) { image.raycastTarget = true; }
        transform.SetParent(parentAfterDrag);
        // SetParent keeps the old world position, so re-center inside whichever
        // slot we landed in (origin slot if the drop target rejected it, or the
        // new slot if InventorySlot.OnDrop accepted it).
        if (rectTransform != null) { rectTransform.anchoredPosition = Vector2.zero; }
    }

    private void RemoveFromInventory()
    {
        if (inventory != null && itemStats != null)
        {
            gameManager.instance.player.GetComponent<Inventory>().RemoveItem(
                itemStats.quantity, itemStats.itemName, true, index
                );
        }
        consumed = true;
        Destroy(gameObject);
    }
}
