//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
//==============================================================================================
// Declare Item Stats
//==============================================================================================
[System.Serializable]
public class ItemStats {
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    [SerializeField] public string itemName;
    [SerializeField] public string itemDescription;
    [SerializeField] public Sprite icon;
    [Range(0.1f, 1000f)] public float weight;
    [Range(1, 99)] public int quantity;
    [Range(1, 99)] public int stackSize;
    [SerializeField] public GameObject itemMesh;
    [SerializeField] public AudioClip[] pickupSound;
    [SerializeField] public ItemHeroStats itemIncreases;
    //==========================================================================================
}
//==============================================================================================
// End of Item Stats CS
//==============================================================================================