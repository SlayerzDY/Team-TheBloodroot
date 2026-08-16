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
    public List<gunStats> _savgunInv;
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
        _savstam = 100f;
        _savplayerPosition = new float[] { 0f, 0f, 0f };
        _savgunInv = new List<gunStats>();
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
            Debug.Log("Please Assign Player Controller and Inventory!");
            return;
        }
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
        if (player.gunInv != null)
        {
            _savgunInv = new List<gunStats>(player.gunInv);
        }
        _savgunInvPos = player.gunInvPos;
        _savhasFlashlight = player.hasFlashlight;
        _savinventoryWeight = playerInv.inventoryWeight;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Game Data CS
//==============================================================================================
