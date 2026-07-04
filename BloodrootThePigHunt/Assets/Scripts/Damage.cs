//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
//==============================================================================================
// Declare Damage
//==============================================================================================
// BLOOD MOON INSTALLATION
// -----------------------
// This support change is required even though WaveManager does not talk to Damage directly. The
// repository keeps damageAmount private, so EnemyAI needs one small public method owned by Damage
// to apply the current wave's multiplier without breaking that ownership.
//
// Bullet prefab setup: keep Damage on the root GameObject instantiated by EnemyAI. Keep Type set to
// bullet and assign Rigidbody, Damage Amount, Bullet Speed, Bullet Destroy Time, and Hit Effect as
// before. Damage Amount is the normal-wave base value; Blood Moon scaling starts from that number.
public class Damage : MonoBehaviour
{
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    enum damageType
    {
        bullet,
        stationary,
        DOT
    }
    [SerializeField] damageType type;
    [SerializeField] Rigidbody rb;
    // Keep this private and configure it in the Inspector. SetDamageMultiplier() is the intentionally
    // narrow runtime entry point used by EnemyAI; no other script needs direct write access.
    [SerializeField] int damageAmount;
    [SerializeField] float damageRate;
    [SerializeField] int bulletSpeed;
    [SerializeField] int bulletDestroyTime;
    [SerializeField] ParticleSystem hitEffect;
    bool isDamaging;

    // Keep the prefab's configured value so Blood Moon scaling always starts from base damage. This
    // avoids stacking a new multiplier on top of a value that was already changed. It also makes this
    // script ready for pooling as long as SetDamageMultiplier() is called whenever a bullet is reused.
    int baseDamageAmount;

    // Awake runs during Instantiate, before EnemyAI receives the returned bullet and calls the setter.
    // That ordering guarantees baseDamageAmount still contains the untouched Inspector value.
    void Awake()
    {
        baseDamageAmount = damageAmount;
    }

    // EnemyAI calls this immediately after instantiating a projectile.
    // Damage remains responsible for its own private damageAmount field.
    // Mathf.Max prevents a negative multiplier, RoundToInt converts the result back to the integer used
    // by IDamage.TakeDamage(), and the outer Max guarantees the final projectile damage is not negative.
    // A normal wave passes 1, so damage remains the prefab value. A Blood Moon wave passes the multiplier
    // returned by BloodMoonModifier.ModifyDamage(1f).
    public void SetDamageMultiplier(float multiplier)
    {
        damageAmount = Mathf.Max(
            0,
            Mathf.RoundToInt(baseDamageAmount * Mathf.Max(0f, multiplier)));
    }
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    void Start()
    {
        if (type == damageType.bullet)
        {
            // EnemyAI has already called SetDamageMultiplier() before Start runs. Movement and lifetime
            // remain unchanged; only damageAmount may differ during a Blood Moon wave.
            rb.linearVelocity = transform.forward * bulletSpeed;
            Destroy(gameObject, bulletDestroyTime);

        }
    }
    //==========================================================================================
    // Function, On Trigger Enter
    //==========================================================================================
    private void OnTriggerEnter(Collider other)
    {
        // Safety Check, Ensure isnt another trigger
        if (other.isTrigger) { return; }
        // Get Damage Interface from other object
        IDamage dmg = other.GetComponent<IDamage>();
        // Checks isnt DOT Damage, if not then apply damage to the other object
        if (dmg != null && type != damageType.DOT)
        {
            // Regular Damage
            dmg.TakeDamage(damageAmount);
        }
        // Checks if the damage type is bullet, if so then apply hit effect and destroy the bullet
        if (type == damageType.bullet)
        {
            if (hitEffect != null)
            {
                Instantiate(hitEffect, transform.position, Quaternion.identity);
            }
            Destroy(gameObject);
        }
    }
    //==========================================================================================
    // Function, On Trigger Stay
    //==========================================================================================
    private void OnTriggerStay(Collider other)
    {
        // Safety Check, Ensure isnt another trigger
        if (other.isTrigger) { return; }
        // Get Damage Interface from other object
        IDamage dmg = other.GetComponent<IDamage>();
        // DOT Damage
        if (dmg != null && type == damageType.DOT && !isDamaging)
        {
            StartCoroutine(damageOther(dmg));
        }

    }
    //==========================================================================================
    // Helper Function, Damage Other
    //==========================================================================================
    IEnumerator damageOther(IDamage d)
    {
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