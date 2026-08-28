//==============================================================================================
// Using Unity Engine
//==============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
//==============================================================================================
// Declare Item Save Data
//==============================================================================================
[Serializable]
public class ItemSaveData
{
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    public string itemID;
    public int quantity;
    //==========================================================================================
}
//==============================================================================================
// Declare Weapon Save Data
//==============================================================================================
[Serializable]
public class WeaponSaveData
{
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    public string itemID;
    //==========================================================================================
}
//==============================================================================================
// Restore runtime guns from Safety's serializable weapon identifiers.
//==============================================================================================
internal static class SafetyWeaponSaveUtility
{
    public static bool TryRestoreRuntimeInventory(
        WeaponSaveData[] savedWeapons,
        int savedSelectionIndex,
        WeaponDatabase weaponDatabase,
        out List<gunStats> restoredWeapons,
        out int restoredSelectionIndex,
        out string error)
    {
        restoredWeapons = new List<gunStats>();
        restoredSelectionIndex = 0;

        if (savedWeapons == null || savedWeapons.Length == 0)
        {
            error = string.Empty;
            return true;
        }

        if (weaponDatabase == null)
        {
            error =
                "Safety weapon loading requires a configured WeaponDatabase.";
            return false;
        }

        int restoredSelectedSlot = -1;
        for (int savedSlot = 0; savedSlot < savedWeapons.Length; savedSlot++)
        {
            WeaponSaveData saved = savedWeapons[savedSlot];
            if (saved == null || string.IsNullOrWhiteSpace(saved.itemID))
                continue;

            gunStats definition = weaponDatabase.GetByID(saved.itemID);
            if (definition == null)
            {

                continue;
            }

            gunStats runtimeWeapon = UnityEngine.Object.Instantiate(definition);
            runtimeWeapon.name = definition.name + " (Safety Save Runtime)";
            runtimeWeapon.hideFlags = HideFlags.DontSave;
            restoredWeapons.Add(runtimeWeapon);

            if (savedSlot == savedSelectionIndex)
                restoredSelectedSlot = restoredWeapons.Count - 1;
        }

        if (restoredWeapons.Count > 0)
        {
            restoredSelectionIndex = restoredSelectedSlot >= 0
                ? restoredSelectedSlot
                : Mathf.Clamp(
                    savedSelectionIndex,
                    0,
                    restoredWeapons.Count - 1);
        }

        error = string.Empty;
        return true;
    }
}
//==============================================================================================
// Declare Game Data
//==============================================================================================
[Serializable]
public class GameData
{
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    public int _savHP;
    public int _savmaxHP;
    public float _savstam;
    public float _savstamMax;
    public ItemSaveData[] _savInventory;
    public WeaponSaveData[] _savgunInv;
    public int _savgunInvPos;
    public bool _savhasFlashlight;
    public float[] _savplayerPosition;
    public float _savinventoryWeight;
    //==========================================================================================
    // Declare Functions 
    //==========================================================================================
    // Function, Default Constructor
    //------------------------------------------------------------------------------------------
    public GameData()
    {
        _savHP = 100;
        _savmaxHP = 100;
        _savstam = 100f;
        _savstamMax = 100f;
        _savplayerPosition = new float[] { 0f, 0f, 0f };
        _savgunInv = Array.Empty<WeaponSaveData>();
        _savhasFlashlight = false;
        _savInventory = new ItemSaveData[30];
        _savinventoryWeight = 0;
    }
    //==========================================================================================
    // Function, Save Game Overloaded Constructor
    //------------------------------------------------------------------------------------------
    public GameData(playerController player, Inventory playerInv) : this()
    {
        if (player == null || playerInv == null)
        {
            return;
        }
        List<gunStats> playergunInv = player.gunInv;
        _savplayerPosition = new float[3] {
            player.transform.position.x,
            player.transform.position.y,
            player.transform.position.z
        };
        _savHP = player.HP;
        _savmaxHP = player.maxHp;
        _savstam = player.stam;
        _savstamMax = player.stamMax;
        if (playerInv.inventoryItems != null)
        {
            _savInventory = new ItemSaveData[playerInv.inventoryItems.Length];
            for (int i = 0; i < playerInv.inventoryItems.Length; i++)
            {
                ItemStats item = playerInv.inventoryItems[i];
                _savInventory[i] = item == null ? null : new ItemSaveData
                {
                    itemID = item.itemID,
                    quantity = item.quantity
                };
            }
        }

        if (playergunInv != null && playergunInv.Count > 0) {
            _savgunInv = new WeaponSaveData[playergunInv.Count];
            for (int i = 0; i < playergunInv.Count; i++) {
                gunStats gun = playergunInv[i];
                _savgunInv[i] = gun == null ? null : new WeaponSaveData {
                    itemID = gun.itemID
                };
            }
        }

        //if (player.gunInv != null)
        //{
        //    _savgunInv = new List<gunStats>(player.gunInv);
        //} else
        //{
        //    _savgunInv = new List<gunStats>();
        //}
        _savgunInvPos = player.gunInvPos;
        _savhasFlashlight = player.hasFlashlight;
        _savstamMax = player.stamMax;
        _savinventoryWeight = playerInv.inventoryWeight;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Game Data CS
//==============================================================================================
