//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
[CreateAssetMenu]
//==============================================================================================
// Declare Gun Stats
//==============================================================================================
public class gunStats : ScriptableObject {
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    public GameObject gunModel;
    [Range(1, 10)] public int shootDamage;
    [Range(5, 1000)] public int shootDistance;
    [Range(.01f, 2)] public float shootRate;
    public int ammoCurr;
    [Range(5, 50)] public int ammoMax;
    public GameObject bullet;
    public AudioClip[] shootSound;
    [Range(0, 1)] public float shootSoundVolume;
    public AudioClip[] reloadSound;
    [Range(0, 1)] public float reloadSoundVolume;

    [Range(1, 8)]public int bulletCount = 1;
    [Range(0, 10f)] public float spread = 0;
    [Range(0, 1)] public int gunType = 0;

    //==========================================================================================
}
//==============================================================================================
// End of Gun Stats CS
//==============================================================================================