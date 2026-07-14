//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
//==============================================================================================
// Declare Player Controller
//==============================================================================================
public class playerController : MonoBehaviour, IDamage, IPickupGun {
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    [SerializeField] CharacterController controller;
    [SerializeField] LayerMask ignoreLayer;
    // Controls
    [SerializeField] int sens;
    [SerializeField] int lockVertMin, lockVertMax;
    float camRotX, camRotY;
    // Player Stats
    [SerializeField] int HP;
    [SerializeField] int speed;
    [SerializeField] int sprintMod;
    [SerializeField] int jumpSpeed;
    [SerializeField] int jumpMax;
    [SerializeField] int gravity;
    // Weapon Stats
    [SerializeField] GameObject gunModel;
    [SerializeField] int shootDamage;
    [SerializeField] int shootDist;
    [SerializeField] float shootRate;
    [SerializeField] Transform gunPivot;
    [SerializeField] Transform shootPos;
    [SerializeField] GameObject bullet;
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
        updatePlayerUI();
    }
    //==========================================================================================
    // Update is called once per frame
    //==========================================================================================
    void Update() {
        movement();
        sprint();
        rotateGun();
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
    // Function, Rotate Gun
    //==========================================================================================
    void rotateGun()
    {
        if (gameManager.instance != null && !gameManager.instance.isPaused)
        {
            Quaternion cameraWorldRot = Camera.main.transform.rotation;
            gunPivot.rotation = Quaternion.Lerp(gunPivot.rotation, cameraWorldRot, sens * Time.deltaTime);
        }
    }
    //==========================================================================================
    // Function, Sprint
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
        Instantiate(bullet, shootPos.position, gunPivot.rotation);
    }
    //==========================================================================================
    // Function, TakeDamage
    //==========================================================================================
    public void TakeDamage(int amount)
    {
        HP -= amount;
        updatePlayerUI();
        StartCoroutine(flashDamage());  
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
    // Function, OnDeath
    //==========================================================================================
    public void onDeath(bool death)
    {
        if (death)
        {
            return;
        }
        Dissolver dissolver = GetComponent<Dissolver>();
        if (dissolver != null)
        {
            dissolver.StartCoroutine(dissolver.dissolve());
        }
        gameManager.instance.youLose();
    }


    //==========================================================================================
    // Function, flashRed
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

    public void updatePlayerUI()
    {
        //fixed
        gameManager.instance.playerHPBAR.fillAmount = (float)HP / HPOrig;

    }

    IEnumerator flashDamage()
    {

        gameManager.instance.playerDamageScreen.SetActive(true);

        yield return new WaitForSeconds(0.1f);

        gameManager.instance.playerDamageScreen.SetActive(false);

    }
    //==========================================================================================
    // Function, get Gun Stats
    //==========================================================================================
    public void getGunStats(gunStats gun) {
        // Assign Stats
        shootDamage = gun.shootDamage;
        shootDist = gun.shootDistance;
        shootRate = gun.shootRate;
        // Assign Visuals
        gunModel.GetComponent<MeshFilter>().sharedMesh = gun.gunModel.GetComponent<MeshFilter>().sharedMesh;
        gunModel.GetComponent<MeshRenderer>().sharedMaterial = gun.gunModel.GetComponent<MeshRenderer>().sharedMaterial;

    }
    //==============================================================================================
}
//==============================================================================================
// End of Player Controller .cs
//==============================================================================================