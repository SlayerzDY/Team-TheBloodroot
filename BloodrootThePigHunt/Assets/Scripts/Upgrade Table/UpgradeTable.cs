using System.Collections.Generic;
using UnityEngine;

public class UpgradeTable : MonoBehaviour, IInteract
{
    // serialized fields
    [SerializeField] private List<UpgradeScriptable> tableInventory = new List<UpgradeScriptable>();
    [SerializeField] private UpgradeUI upgradeWindow;
    //private variables
    public UpgradeScriptable seleectedUpgrade;

    void Start()
    {

        upgradeWindow = FindAnyObjectByType<UpgradeUI>(FindObjectsInactive.Include);
        if (upgradeWindow == null) {
            return; 
        }

    }

    public void SendInteract(Collider target)
    {
        if(upgradeWindow != null && tableInventory.Count > 0){ upgradeWindow.OpenPanelMulti(tableInventory, this); }

    }

    public void UpgradeRunTimeScriptableData(UpgradeScriptable newData) {  seleectedUpgrade = newData;     }

    public void TryPurchaingUpgradee()
    {

        if (seleectedUpgrade == null) { return; }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { return; }

        Inventory playerInventory = player.GetComponent<Inventory>();
        playerController Ctrl = player.GetComponent<playerController>();
        if (playerInventory == null || Ctrl == null) { return; }

        Item itemComponent = seleectedUpgrade.requiredItem.GetComponent<Item>();

        if (itemComponent == null || itemComponent.item == null)
        {
            return;
        }

        ItemStats scriptableItemData = itemComponent.item;
        bool hasItem = playerInventory.CheckItem(scriptableItemData);

        if (hasItem)
        {
            playerInventory.RemoveItem(scriptableItemData.itemName, seleectedUpgrade.requirdAmount, false, 0);

            ApplyUpgrade(Ctrl);
            if (upgradeWindow != null) upgradeWindow.ShowNotification("Upgrade Successful", true);
        }
        else
        {
            if (upgradeWindow != null) upgradeWindow.ShowNotification("Failed: Not enough materials!", false);
            //gameManager.instance.ToastMenu(true, "Insufficient crafting materials!");
        }


    }

    private void ApplyUpgrade(playerController ctrler)
    {
        string stat = seleectedUpgrade.statToUpgrade.ToLower();

        if (stat == "Damage" || stat == "Attack" || stat == "ShootDamage")
        {
            if (seleectedUpgrade.targetGun != null)
            {
                string gunType = seleectedUpgrade.targetGun.name.ToLower();
                float upgradeAmount = seleectedUpgrade.upgradeValue;

                if (gunType.Contains("pistol"))
                {
                    ctrler.pistolDamageMultiplier += upgradeAmount;
                    //gameManager.instance.ToastMenu(true, $"Pistol upgraded to x{ctrler.pistolDamageMultiplier}");
                }
                else if (gunType.Contains("rifle"))
                {
                    ctrler.rifleDamageMultiplier += upgradeAmount;
                    //gameManager.instance.ToastMenu(true, $"Rifle upgraded to {ctrler.rifleDamageMultiplier}x");
                }
                else if (gunType.Contains("shotgun"))
                {
                    ctrler.shotgunDamageMultiplier += upgradeAmount;
                    //gameManager.instance.ToastMenu(true, $"Shotgun upgraded to {ctrler.shotgunDamageMultiplier}x");
                }
            }
            return;
        }

        switch (stat)
        {
            case "ammo":
                break;
            case "Health":
            case "HP":
                ctrler.healthMultiplier += seleectedUpgrade.upgradeValue;
                ctrler.healthMultiplier = Mathf.Clamp(ctrler.healthMultiplier, 1f, 4f);
                ctrler.UpdateUpgradedStats("Health");
                break;

            case "Stamina":
                ctrler.staminaMultiplier += seleectedUpgrade.upgradeValue;
                ctrler.staminaMultiplier = Mathf.Clamp(ctrler.staminaMultiplier, 1f, 4f);
                ctrler.UpdateUpgradedStats("Stamina");
                break;
        }


    }

}
