//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
//==============================================================================================
// Declare Inventory Item
//==============================================================================================
public class inventoryItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [Header("UI")]
    public Image image;
    [HideInInspector] public Transform parentAfterDrag;
    [Header("Item")]
    [SerializeField] public ItemStats itemStats;
    [SerializeField] public int index;
    [SerializeField] public Inventory inventory;
    [SerializeField] public Sprite defaultImage;
    [HideInInspector] public bool consumed = false;
    [HideInInspector] private Canvas canvas;
    [HideInInspector] private RectTransform rectTransform;
    //==========================================================================================
    // Declare Public Functions
    //==========================================================================================
    // Function, Awake
    //------------------------------------------------------------------------------------------
    private void Awake() {
        rectTransform = GetComponent<RectTransform>();
        if (image == null) image = GetComponent<Image>();
        canvas = GetComponentInParent<Canvas>();
        RefreshIcon();
    }
    //==========================================================================================
    // Function, Refresh Icon
    //------------------------------------------------------------------------------------------
    public void RefreshIcon() {
        if (itemStats == null || string.IsNullOrEmpty(itemStats.itemName)) { return; }
        image.sprite = itemStats.icon != null ? itemStats.icon : defaultImage;
    }
    //==========================================================================================
    // Function, On Begin Drag
    //------------------------------------------------------------------------------------------
    public void OnBeginDrag(PointerEventData eventData) {
        if (image != null) { image.raycastTarget = false; }
        parentAfterDrag = transform.parent;
        if (canvas != null) {
            transform.SetParent(canvas.transform);
            transform.SetAsLastSibling();
        }
    }
    //==========================================================================================
    // Function, On Drag
    //------------------------------------------------------------------------------------------
    public void OnDrag(PointerEventData eventData) {
        transform.position = eventData.position;
    }
    //==========================================================================================
    // Function, On End Drag
    //------------------------------------------------------------------------------------------
    public void OnEndDrag(PointerEventData eventData) {
        if (consumed) { return; }
        if (image != null) { image.raycastTarget = true; }
        transform.SetParent(parentAfterDrag);
        if (rectTransform != null) { rectTransform.anchoredPosition = Vector2.zero; }
    }
    //==========================================================================================
}
//==============================================================================================
// End of Inventory Item CS
//==============================================================================================