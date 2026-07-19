//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
//==============================================================================================
// Declare Player Controller
//==============================================================================================
public class playerController : MonoBehaviour, IDamage, IPickupGun {
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    [SerializeField] CharacterController controller;
    [SerializeField] LayerMask ignoreLayer;
    float camRotX, camRotY;
    // Player Stats
    [SerializeField] int HP;
    [SerializeField] int speed;
    [SerializeField] int sprintMod;
    [SerializeField] int jumpSpeed;
    [SerializeField] int jumpMax;
    [SerializeField] int gravity;
    // Weapon Stats
    [SerializeField] List<gunStats> gunInv = new List<gunStats>();
    [SerializeField] GameObject gunModel;
       
    //[SerializeField] Transform gunPivot;
    [SerializeField] Transform shootPos;
    int jumpCount;
    int HPOrig;
    int gunInvPos;
    float shootTimer;
    Vector3 moveDir;
    Vector3 playerVel;
    //==========================================================================================
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    //==========================================================================================
    void Start() {
        HPOrig = HP;
        spawnPlayer();
    }
    //==========================================================================================
    // Update is called once per frame
    //==========================================================================================
    void Update() {
        if (!gameManager.instance.isPaused)
            movement();
        sprint();
    }
    //==========================================================================================
    // Function, Movement
    //==========================================================================================
    void movement() {
        selectGun();
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
        if (Input.GetButton("Fire1") && gunInv.Count > 0 && gunInv[gunInvPos].ammoCurr > 0 && shootTimer > gunInv[gunInvPos].shootRate) { shoot(); }
        if (gunInv.Count > 0) {
            Debug.DrawRay(Camera.main.transform.position, Camera.main.transform.forward * gunInv[gunInvPos].shootDistance, Color.red);
        }
        reload();
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
        gunInv[gunInvPos].ammoCurr--;
        updatePlayerAmmo();
        if (gunInv[gunInvPos].shootSound.Count() > 0) {
            int randomInt = Random.Range(0, gunInv[gunInvPos].shootSound.Count());
            AudioSource.PlayClipAtPoint(gunInv[gunInvPos].shootSound[randomInt], gameObject.transform.position, gunInv[gunInvPos].shootSoundVolume); 
        }
        Instantiate(gunInv[gunInvPos].bullet, shootPos.position, shootPos.rotation);
    }

    //==========================================================================================
    // Function, Rload
    //==========================================================================================

    void reload()
    {

        if (Input.GetButtonDown("Reload") && gunInv.Count > 0)
        {

            gunInv[gunInvPos].ammoCurr = gunInv[gunInvPos].ammoMax;
            updatePlayerAmmo();
        }

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
            if (GetComponent<Dissolver>() != null) { GetComponent<Dissolver>().StartCoroutine(GetComponent<Dissolver>().dissolve(true)); }
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
    // Function, Update Player UI
    //==========================================================================================
    public void updatePlayerUI() {
        //fixed
        gameManager.instance.playerHPBAR.fillAmount = (float)HP / HPOrig;
    }
    //==========================================================================================
    // Function, get Gun Stats
    //==========================================================================================
    public void updatePlayerAmmo() {
        if (gunInv.Count > 0)
        {
            gameManager.instance.AmmoCount.text = $"{gunInv[gunInvPos].ammoCurr} / {gunInv[gunInvPos].ammoMax}";
        } else {
            gameManager.instance.AmmoCount.text = "0 / 0";
        }
    }
    //==========================================================================================
    // Function, Flash Damage
    //==========================================================================================
    IEnumerator flashDamage() {
        gameManager.instance.playerDamageScreen.SetActive(true);
        yield return new WaitForSeconds(0.1f);
        gameManager.instance.playerDamageScreen.SetActive(false);
    }
    //==========================================================================================
    // Function, get Gun Stats
    //==========================================================================================
    public void getGunStats(gunStats gun)
    {
        gunInv.Add(gun);
        gunInvPos = gunInv.Count - 1;
        changeGun();
    }
    //==========================================================================================
    // Function, Change Gun
    //==========================================================================================
    void changeGun()
    {
        // Assign Visuals
        updatePlayerAmmo();
        gunModel.GetComponent<MeshFilter>().sharedMesh = gunInv[gunInvPos].gunModel.GetComponent<MeshFilter>().sharedMesh;
        gunModel.GetComponent<MeshRenderer>().sharedMaterial = gunInv[gunInvPos].gunModel.GetComponent<MeshRenderer>().sharedMaterial;
    }
    //==========================================================================================
    // Function, Select Gun
    //==========================================================================================
    void selectGun()
    {
        if (Input.GetAxis("Mouse ScrollWheel") > 0 && gunInvPos < gunInv.Count - 1)
        {
            gunInvPos++;
            changeGun();
        }
        else if (Input.GetAxis("Mouse ScrollWheel") > 0 && gunInvPos > 0)
        {
            gunInvPos--;
            changeGun();
        }
    }
    //==========================================================================================
    // Function, Spawn Player
    //==========================================================================================
    public void spawnPlayer() {
        controller.transform.position = gameManager.instance.playerSpawnPos.transform.position;
        Physics.SyncTransforms();
        HP = HPOrig;
        updatePlayerAmmo();
        updatePlayerUI();
    }
    //==============================================================================================
}
//==============================================================================================
// End of Player Controller .cs
//==============================================================================================