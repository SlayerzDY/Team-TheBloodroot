//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
using UnityEngine.AI;
//==============================================================================================
// Instructions for Using the Dissolve Script
//==============================================================================================
/* 
 * Instructions for using the Dissolver Script:
 * 1.) Assign the hold time in the Inspector.
 * 2.) Assign the release time in the Inspector.
*/
//==============================================================================================
// Declare Player Captured 
//==============================================================================================
public class PlayerCaptured : MonoBehaviour, IInteract {
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    [SerializeField] float holdTime = 5f;
    [SerializeField] float releaseTime = 5f;
    [SerializeField] AudioClip damageSound;
    //float elapsedTime = 0f;
    bool isTriggered = false;
    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void OnTriggerEnter(Collider other) {
        if (isTriggered) { gameObject.GetComponent<Damage>().enabled = false; }
        if (!isTriggered) {
            if (!(other is CapsuleCollider) && other.CompareTag("Enemy")) return;
            //istriggered was placed to early and causing premature lockage of bear traps maybe due to the floor or something else but should be fixed now
            if (other.CompareTag("Enemy")) {
                if (other.GetComponent<enemyAI>() != null) {
                    isTriggered = true;
                    StartCoroutine(holdObject(other.gameObject));
                }
            }
            if (other.CompareTag("Player")) {
                isTriggered = true;
                StartCoroutine(holdObject(other.gameObject));
            }
        }
    }
    //==========================================================================================
    // Function, Hold Object
    //==========================================================================================
    IEnumerator holdObject(GameObject entity) {
        if (damageSound != null) { AudioSource.PlayClipAtPoint(damageSound, gameObject.transform.position, 0.35f); }
        disableMovement(entity);
        float timer = 0f;
        while (timer < holdTime) {
            timer += Time.deltaTime;
            yield return null;
        }
        StartCoroutine(releaseObject(entity));
    }
    //==========================================================================================
    // Function, Release Object
    //==========================================================================================
    private void disable() {
        if (gameObject.GetComponent<Dissolver>() != null) { gameObject.GetComponent<Dissolver>().StartCoroutine(gameObject.GetComponent<Dissolver>().dissolve()); return; } else { Destroy(gameObject); }
    }
    //==========================================================================================
    // Function, Release Object
    //==========================================================================================
    IEnumerator releaseObject(GameObject entity) {
        float timer = 0f;
        while (timer < releaseTime) {
            timer += Time.deltaTime;
            yield return null;
        }
        enableMovement(entity);
        disable();
    }
    //==========================================================================================
    // Function, Disable Movement
    //==========================================================================================
    void disableMovement(GameObject entity) {
        Damage dmg = this.GetComponent<Damage>();
        if (entity.tag == gameManager.instance.player.tag) {
            playerController playerCtrl = entity.GetComponent<playerController>();
            if (dmg != null) {
                dmg.enabled = false;
            }
            if (playerCtrl != null) {
                playerCtrl.enabled = false;
            }
        }
        if (entity.tag == "Enemy") {
            // needed to disable nave mesh agent to stop ai
            enemyAI enemyAI = entity.GetComponent<enemyAI>();
            NavMeshAgent enemyAI2 = entity.GetComponent<NavMeshAgent>();
            if (dmg != null) {
                dmg.enabled = false;
            }
            if (enemyAI != null) {
                enemyAI.enabled = false;
                enemyAI2.enabled = false;
            }
        }    
    }
    //==========================================================================================
    // Function, Enable Movement
    //==========================================================================================
    void enableMovement(GameObject entity) {
        if (entity.tag == gameManager.instance.player.tag) {
            playerController playerCtrl = entity.GetComponent<playerController>();
            if (playerCtrl != null) {
                playerCtrl.enabled = true;
            }
        }
        if (entity.tag == "Enemy") {
            enemyAI enemyAI = entity.GetComponent<enemyAI>();
            NavMeshAgent enemyAI2 = entity.GetComponent<NavMeshAgent>();
            if (enemyAI != null) {
                enemyAI.enabled = true;
                enemyAI2.enabled = true;
            }
        }
    }
    //==========================================================================================
    // Function, Send Interact
    //==========================================================================================
    public void SendInteract(Collider target) {
        if (damageSound != null) { AudioSource.PlayClipAtPoint(damageSound, gameObject.transform.position, 0.35f); }
        isTriggered = true;
        disable();
    }
    //==========================================================================================
}
//==============================================================================================
// Declare Player Captured
//==============================================================================================
