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
    // Function, Start
    //==========================================================================================
    void Start() {
        
    }
    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void Update() {
        elapsedTime += Time.deltaTime;
    }
    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void OnTriggerEnter(Collider gameObject) {
        elapsedTime = 0f;
        StartCoroutine(holdObject(gameObject.gameObject));
    }
    //==========================================================================================
    // Function, Hold Object
    //==========================================================================================
    IEnumerator holdObject(GameObject entity) {
        disableMovement(entity);    
        while (elapsedTime < holdTime) {
            yield return null;
        }
        StartCoroutine(releaseObject(entity));

    }
    //==========================================================================================
    // Function, Release Object
    //==========================================================================================
    IEnumerator releaseObject(GameObject entity) {
        while (elapsedTime < releaseTime) {
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