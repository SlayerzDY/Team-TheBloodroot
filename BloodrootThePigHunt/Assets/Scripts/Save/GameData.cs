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
// Declare Game Data
//==============================================================================================
[Serializable]
public class GameData
{
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    public int _savHP;
    public float _savstam;
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
        List<gunStats> playergunInv = gameManager.instance.player.GetComponent<playerController>()?.gunInv;
        _savHP = 100;
        _savstam = 100f;
        _savplayerPosition = new float[] { 0f, 0f, 0f };
        _savgunInv = new WeaponSaveData[playergunInv.Count];
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
            //Debug.Log("Please Assign Player Controller and Inventory!");
            return;
        }
        List<gunStats> playergunInv = gameManager.instance.player.GetComponent<playerController>()?.gunInv;
        _savplayerPosition = new float[3] {
            player.transform.position.x,
            player.transform.position.y,
            player.transform.position.z
        };
        _savHP = player.HP;
        _savstam = player.stam;
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
        _savinventoryWeight = playerInv.inventoryWeight;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Game Data CS
//==============================================================================================
