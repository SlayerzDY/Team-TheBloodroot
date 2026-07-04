//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
//==============================================================================================
// Declare Damage
//==============================================================================================
public class Damage : MonoBehaviour {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    enum damageType {
        bullet,
        stationary,
        DOT
    }
    [SerializeField] damageType type;
    [SerializeField] Rigidbody rb;
    [SerializeField] int damageAmount;
    [SerializeField] float damageRate;
    [SerializeField] int bulletSpeed;
    [SerializeField] int bulletDestroyTime;
    [SerializeField] ParticleSystem hitEffect;
    bool isDamaging;
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    void Start() {
        if (type == damageType.bullet) {
            rb.linearVelocity = transform.forward * bulletSpeed;
            Destroy(gameObject, bulletDestroyTime); 

        }
    }
    //==========================================================================================
    // Function, On Trigger Enter
    //==========================================================================================
    private void OnTriggerEnter(Collider other) {
        // Safety Check, Ensure isnt another trigger
        if (other.isTrigger) { return; }
        // Get Damage Interface from other object
        IDamage dmg = other.GetComponent<IDamage>();
        // Checks isnt DOT Damage, if not then apply damage to the other object
        if (dmg != null && type != damageType.DOT) {
            // Regular Damage
            dmg.TakeDamage(damageAmount);
        }
        // Checks if the damage type is bullet, if so then apply hit effect and destroy the bullet
        if (type == damageType.bullet) {
            if (hitEffect != null) {
                Instantiate(hitEffect, transform.position, Quaternion.identity);
            }
            Destroy(gameObject);
        }
    }
    //==========================================================================================
    // Function, On Trigger Stay
    //==========================================================================================
    private void OnTriggerStay(Collider other) {
        // Safety Check, Ensure isnt another trigger
        if (other.isTrigger) { return; }
        // Get Damage Interface from other object
        IDamage dmg = other.GetComponent<IDamage>();
        // DOT Damage
        if (dmg != null && type == damageType.DOT && !isDamaging) {
            StartCoroutine(damageOther(dmg));
        }
        
    }
    //==========================================================================================
    // Helper Function, Damage Other
    //==========================================================================================
    IEnumerator damageOther(IDamage d) {
        isDamaging = true;
        d.TakeDamage(damageAmount);
        yield return new WaitForSeconds(damageRate);
        isDamaging = false;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Damage 
//==============================================================================================