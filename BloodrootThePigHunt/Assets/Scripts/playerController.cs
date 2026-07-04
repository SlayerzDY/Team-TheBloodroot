//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
//==============================================================================================
// Declare Player Controller
//==============================================================================================
public class playerController : MonoBehaviour, IDamage {
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    [SerializeField] CharacterController controller;
    [SerializeField] LayerMask ignoreLayer;
    // Player Stats
    [SerializeField] int HP;
    [SerializeField] int speed;
    [SerializeField] int sprintMod;
    [SerializeField] int jumpSpeed;
    [SerializeField] int jumpMax;
    [SerializeField] int gravity;
    // Weapon Stats
    [SerializeField] int shootDamage;
    [SerializeField] int shootDist;
    [SerializeField] float shootRate;
    int jumpCount;
    int HPOrig;
    float shootTimer;
    Vector3 moveDir;
    Vector3 playerVel;
    //==========================================================================================
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    //==========================================================================================
    void Start() {
        HPOrig = HP;
    }
    //==========================================================================================
    // Update is called once per frame
    //==========================================================================================
    void Update() {
        movement();
        sprint();
    }
    //==========================================================================================
    // Function, Movement
    //==========================================================================================
    void movement() {
        
        if (controller.isGrounded) {
            playerVel.y = 0;
            jumpCount = 0;
        }
        // kill after the plus to make Side Scroller
        moveDir = Input.GetAxis("Horizontal") * transform.right + Input.GetAxis("Vertical") * transform.forward;
        controller.Move(moveDir.normalized * speed * Time.deltaTime);
        jump();
        controller.Move(playerVel * Time.deltaTime);
        playerVel.y -= gravity * Time.deltaTime;
        shootTimer += Time.deltaTime;
        if (Input.GetButton("Fire1") && shootTimer > shootRate) { shoot(); }
        Debug.DrawRay(Camera.main.transform.position, Camera.main.transform.forward * shootDist, Color.red);
    }
    //==========================================================================================
    // Function, Movement
    //==========================================================================================
    void sprint() {
        if(Input.GetButtonDown("Sprint")) {
            speed *= sprintMod;
        } else if(Input.GetButtonUp("Sprint")) {
            speed /= sprintMod;
        }
    }
    //==========================================================================================
    // Function, Movement
    //==========================================================================================
    void jump() {
        if (Input.GetButtonDown("Jump") && jumpCount < jumpMax) {
            playerVel.y = jumpSpeed;
            jumpCount++;

        }
    }

    //==========================================================================================
    // Function, Shoot
    //==========================================================================================
    void shoot() {
        shootTimer = 0;
        RaycastHit hit;
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, shootDist, ~ignoreLayer)) {
            if (hit.collider.CompareTag("Player")) { return; }
            if (!(hit.collider is CapsuleCollider)) { return; }
            Debug.Log(hit.collider.name);
            IDamage dmg = hit.collider.GetComponent<IDamage>();
            if (dmg != null) { dmg.TakeDamage(shootDamage); }
        }
    }
    //==========================================================================================
    // Function, TakeDamage
    //==========================================================================================
    public void TakeDamage(int amount)
    {
        HP -= amount;
        if (HP <= 0)
        {
            if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolve()); }
            gameManager.instance.youLose();
        }
        else
        {
            StartCoroutine(flashRed());
        }
    }
    //==========================================================================================
    // Function, TakeDamage
    //==========================================================================================
    IEnumerator flashRed()
    {  
        //model.material.color = Color.red;
        //yield return new WaitForSeconds(0.1f);
        //model.material.color = colorOrig;
        if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolveFlash()); }
        yield return null;
    }

    public void onDeath(bool dead)
    {
        throw new System.NotImplementedException();
    }
    //==========================================================================================
}
//==============================================================================================
// End of Player Controller .cs
//==============================================================================================