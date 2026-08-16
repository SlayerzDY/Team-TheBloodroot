using UnityEngine;

// needed for scriptable and I just wanted to customize thee menu name
[CreateAssetMenu (menuName = "Upgrades For Table" )]

public class UpgradeScriptable : ScriptableObject
{
    // public variables
    public string upgradeNamee;
    public GameObject requiredItem;
    public int requirdAmount;
    public gunStats targetGun;
    public string statToUpgrade;
    [Range(0.5f, 20)]public float upgradeValue;
}
