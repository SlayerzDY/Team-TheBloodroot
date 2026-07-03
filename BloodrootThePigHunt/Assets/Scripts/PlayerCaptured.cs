//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
//==============================================================================================
// Declare Player Captured
//==============================================================================================
public class PlayerCaptured : MonoBehaviour {
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    [SerializeField] float holdTime = 5f;
    [SerializeField] float releaseTime = 5f;    
    float elapsedTime = 0f;
    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void OnTriggerEnter(Collider other) {
        if (!(other is CapsuleCollider)) return;
        if (other.CompareTag("Enemy")) {
            if (other.GetComponent<EnemyAI>() != null) {
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
        disableMovement(entity);
        // A local timer that starts fresh at 0
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
    IEnumerator releaseObject(GameObject entity) {
        // Another fresh local timer
        float timer = 0f;
        while (timer < releaseTime) {
            timer += Time.deltaTime;
            yield return null;
        }
        enableMovement(entity);
        Destroy(gameObject); // Now it will safely wait for both timers to finish!
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
        if (entity.tag == "Enemy")
        {
            EnemyAI enemyAI = entity.GetComponent<EnemyAI>();
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
            EnemyAI enemyAI = entity.GetComponent<EnemyAI>();
            if (enemyAI != null) {
                enemyAI.enabled = true;
            }
        }
    }
    //==========================================================================================
}
//==============================================================================================
// Declare Player Captured
//==============================================================================================