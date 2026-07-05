//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
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
    float elapsedTime = 0f;
    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void OnTriggerEnter(Collider other) {
        if (!(other is CapsuleCollider) && other.CompareTag("Enemy")) return;
        if (other.CompareTag("Enemy")) {
            if (other.GetComponent<enemyAI>() != null) {
                StartCoroutine(holdObject(other.gameObject));
            }
        }
        if (other.CompareTag("Player")) {
            StartCoroutine(holdObject(other.gameObject));
        }
    }
    //==========================================================================================
    // Function, Hold Object
    //==========================================================================================
    IEnumerator holdObject(GameObject entity) {
        if (damageSound != null) { AudioSource.PlayClipAtPoint(damageSound, gameObject.transform.position); }
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
        Destroy(gameObject);
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
        Destroy(gameObject);
    }
    //==========================================================================================
    // Function, Disable Movement
    //==========================================================================================
    void disableMovement(GameObject entity) {
        if (entity.tag == gameManager.instance.player.tag) {
            playerController playerCtrl = entity.GetComponent<playerController>();
            if (playerCtrl != null) {
                playerCtrl.enabled = false;
            }
        }
        if (entity.tag == "Enemy") {
            enemyAI enemyAI = entity.GetComponent<enemyAI>();
            if (enemyAI != null) {
                enemyAI.enabled = false;
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
            if (enemyAI != null) {
                enemyAI.enabled = true;
            }
        }
    }
    //==========================================================================================
    // Function, Send Interact
    //==========================================================================================
    public void SendInteract(Collider target) {
        if (damageSound != null) { AudioSource.PlayClipAtPoint(damageSound, gameObject.transform.position); }
        if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolve()); return;  }
        disable();
    }
    //==========================================================================================
}
//==============================================================================================
// Declare Player Captured
//==============================================================================================