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

    // Set true by a drop target (e.g. ItemTossZone) that already removed this
    // item from the inventory and is about to Destroy() it - skips the
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
        if (image != null) { image.raycastTarget = true; }
        transform.SetParent(parentAfterDrag);
        // SetParent keeps the old world position, so re-center inside whichever
        // slot we landed in (origin slot if the drop target rejected it, or the
        // new slot if InventorySlot.OnDrop or ItemTossZone.OnDrop accepted it).
        if (rectTransform != null) { rectTransform.anchoredPosition = Vector2.zero; }
    }
}
