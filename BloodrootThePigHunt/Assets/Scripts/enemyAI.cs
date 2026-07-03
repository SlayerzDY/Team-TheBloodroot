//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
using UnityEngine.AI;
//==============================================================================================
// Declare Enemy AI
//==============================================================================================
public class EnemyAI : MonoBehaviour, IDamage {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] int HP;
    [SerializeField] Renderer model;
    [SerializeField] NavMeshAgent agent;
    [SerializeField] GameObject bullet;
    [SerializeField] Transform gunPivot;
    [SerializeField] Transform shootPos;
    [SerializeField] float shootRate;
    [SerializeField] int gunRotateSpeed;
    [SerializeField] int faceTargetSpeed;
    Color colorOrig;
    Vector3 playerDir;
    float shootTimer;
    bool playerInTrigger;
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    void Start() {
        colorOrig = model.material.color;
        gameManager.instance.updateGameGoal(1);
    }
    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void Update() {
        // Shoot Mechanics
        if (playerInTrigger) {
            agent.SetDestination(gameManager.instance.player.transform.position);
            shootTimer += Time.deltaTime;
            playerDir = gameManager.instance.player.transform.position - transform.position;
            rotateGun();
            faceTarget();
            if (shootTimer >= shootRate) {
                shoot();
            }
        }
    }
    //==========================================================================================
    // Function, On Trigger Enter
    //==========================================================================================
    void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            playerInTrigger = true;
        }
    }
    //==========================================================================================
    // Function, On Trigger Exit
    //==========================================================================================
    void OnTriggerExit(Collider other) {
        if (other.CompareTag("Player")) {
            playerInTrigger = false;
        }
    }
    //==========================================================================================
    // Function, Shoot
    //==========================================================================================
    void shoot() {
        shootTimer = 0;
        Instantiate(bullet, shootPos.position, gunPivot.rotation);
    }
    //==========================================================================================
    // Function, Face Target
    //==========================================================================================
    void faceTarget() { 
        Quaternion rot = Quaternion.LookRotation(new Vector3(playerDir.x, 0, playerDir.z));
        transform.rotation = Quaternion.Lerp(transform.rotation, rot, faceTargetSpeed * Time.deltaTime);
    }
    //==========================================================================================
    // Function, Rotate Gun
    //==========================================================================================
    void rotateGun() {
        Quaternion rot = Quaternion.LookRotation(playerDir);
        gunPivot.rotation = Quaternion.Lerp(gunPivot.rotation, rot, shootRate * Time.deltaTime);
    }
    //==========================================================================================
    // Function, TakeDamage
    //==========================================================================================
    public void TakeDamage(int amount) {
        HP -= amount;
        if (HP <= 0) {
            if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolve()); }
            gameManager.instance.updateGameGoal(-1);
        } else {
            StartCoroutine(flashRed());
        }
    }
    //==========================================================================================
    // Function, Flash
    //==========================================================================================
    IEnumerator flashRed()
    {
        //model.material.color = Color.red;
        //yield return new WaitForSeconds(0.1f);
        //model.material.color = colorOrig;
        if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolveFlash()); }
        yield return null;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Enemy AI .cs
//==============================================================================================