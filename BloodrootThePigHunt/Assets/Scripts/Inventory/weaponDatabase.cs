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
public class WeaponDatabase : MonoBehaviour
{
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] public pickupGun[] allItemPrefabs;
    private Dictionary<string, gunStats> lookup;
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
        lookup = new Dictionary<string, gunStats>();
        if (allItemPrefabs == null) { return; }
        foreach (pickupGun prefab in allItemPrefabs)
        {
            if (prefab == null || prefab.gun == null || string.IsNullOrEmpty(prefab.gun.itemID)) { continue; }
            lookup[prefab.gun.itemID] = prefab.gun;
        }
    }
    //==========================================================================================
    // Function, Get By ID
    //------------------------------------------------------------------------------------------
    public gunStats GetByID(string id)
    {
        if (string.IsNullOrEmpty(id)) { return null; }
        if (lookup == null) { BuildLookup(); }
        return lookup.TryGetValue(id, out gunStats item) ? item : null;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Item Database CS
//==============================================================================================
