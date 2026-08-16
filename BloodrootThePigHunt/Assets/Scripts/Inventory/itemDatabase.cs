//==============================================================================================
// Using Unity Engine
//==============================================================================================
using System.Collections.Generic;
using UnityEngine;
//==============================================================================================
// Declare Item Database
//==============================================================================================
// Holds a direct reference to every item's ItemStats, keyed by itemID.
// Drag every item prefab's Item component into allItemPrefabs in the Inspector.
// This never mutates at runtime - it's a lookup table, not live state.
//==============================================================================================
public class ItemDatabase : MonoBehaviour
{
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] public Item[] allItemPrefabs;
    private Dictionary<string, ItemStats> lookup;
    //==========================================================================================
    // Declare Functions
    //==========================================================================================
    // Function, Awake
    //------------------------------------------------------------------------------------------
    private void Awake()
    {
        BuildLookup();
    }
    //==========================================================================================
    // Function, Build Lookup
    //------------------------------------------------------------------------------------------
    private void BuildLookup()
    {
        lookup = new Dictionary<string, ItemStats>();
        if (allItemPrefabs == null) { return; }
        foreach (Item prefab in allItemPrefabs)
        {
            if (prefab == null || prefab.item == null || string.IsNullOrEmpty(prefab.item.itemID)) { continue; }
            lookup[prefab.item.itemID] = prefab.item;
        }
    }
    //==========================================================================================
    // Function, Get By ID
    //------------------------------------------------------------------------------------------
    public ItemStats GetByID(string id)
    {
        if (string.IsNullOrEmpty(id)) { return null; }
        if (lookup == null) { BuildLookup(); }
        return lookup.TryGetValue(id, out ItemStats item) ? item : null;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Item Database CS
//==============================================================================================
