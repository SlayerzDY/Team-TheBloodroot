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
            if (prefab == null || prefab.gun == null)
            {
                Debug.LogWarning(
                    "WeaponDatabase ignored a pickup without a gun definition.",
                    this);
                continue;
            }

            string itemID = prefab.gun.itemID;
            if (string.IsNullOrWhiteSpace(itemID))
            {
                Debug.LogError(
                    "WeaponDatabase ignored '" + prefab.name +
                    "' because its gun definition has no itemID.",
                    this);
                continue;
            }

            if (lookup.ContainsKey(itemID))
            {
                Debug.LogError(
                    "WeaponDatabase ignored duplicate itemID '" + itemID + "'.",
                    this);
                continue;
            }

            lookup.Add(itemID, prefab.gun);
        }
    }
    //==========================================================================================
    // Function, Get By ID
    //------------------------------------------------------------------------------------------
    public gunStats GetByID(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) { return null; }
        if (lookup == null) { BuildLookup(); }
        return lookup.TryGetValue(id, out gunStats item) ? item : null;
    }

    //==========================================================================================
    // Function, Try Resolve Registered Weapon
    //------------------------------------------------------------------------------------------
    // Accept either the authored definition itself or an exact runtime copy.
    // An arbitrary ScriptableObject must not become saveable simply by claiming
    // the same itemID as a registered weapon.
    //==========================================================================================
    public bool TryResolveRegisteredWeapon(
        gunStats candidate,
        out gunStats definition)
    {
        definition = null;
        if (candidate == null || string.IsNullOrWhiteSpace(candidate.itemID))
        {
            return false;
        }

        definition = GetByID(candidate.itemID);
        if (definition == null)
        {
            return false;
        }

        return ReferenceEquals(candidate, definition) ||
               (candidate.gunModel == definition.gunModel &&
                candidate.bullet == definition.bullet &&
                candidate.ammoMax == definition.ammoMax &&
                candidate.gunType == definition.gunType);
    }
    //==========================================================================================
}
//==============================================================================================
// End of Item Database CS
//==============================================================================================
