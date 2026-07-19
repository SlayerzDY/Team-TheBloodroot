//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
[CreateAssetMenu]
//==============================================================================================
// Declare Flashlight Stats
//==============================================================================================
public class flashlightStats : ScriptableObject {
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    public GameObject flashlightModel;
    [Range(1, 100)] public float batteryCurr;
    [Range(1, 100)] public float batteryMax;
    [Range(.1f, 10)] public float batteryDrainRate;
    [Range(0, 25)] public float batteryRechargeRate;
    [Range(1, 100)] public float batteryPickupAmount;
    [Range(0, 10)] public float lightIntensity;
    [Range(1, 100)] public float lightRange;
    [Range(1, 179)] public float lightAngle;
    [Range(0, 1)] public float lowBatteryPercent;
    [Range(0, 1)] public float bloodMoonFlickerChance;
    public AudioClip[] toggleSound;
    public AudioClip[] batteryLowSound;
    [Range(0, 1)] public float flashlightSoundVolume;
    //==========================================================================================
}
//==============================================================================================
// End of Flashlight Stats CS
//==============================================================================================
