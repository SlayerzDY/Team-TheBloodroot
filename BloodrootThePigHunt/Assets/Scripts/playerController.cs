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
public class playerController : MonoBehaviour, IDamage, IPickupGun, IPickupFlashlight {
    //==========================================================================================
    // Define Variables
    //==========================================================================================
    [SerializeField] CharacterController controller;
    [SerializeField] LayerMask ignoreLayer;
    float camRotX, camRotY;
    // Player Stats
    [Range(1, 1000)] [SerializeField] int HP;
    [Range(1, 100)] [SerializeField] int speed;
    [Range(1, 10)] [SerializeField] int sprintMod;
    [Range(1, 10)] [SerializeField] int jumpSpeed;
    [Range(1, 10)] [SerializeField] int jumpMax;
    [SerializeField] int gravity;
    // Weapon Stats
    [SerializeField] List<gunStats> gunInv = new List<gunStats>();
    [SerializeField] GameObject gunModel;
    [SerializeField] AudioClip[] hurtEffects;
    [Range(0f, 1f)] [SerializeField] float hurtSoundVolume;
    [SerializeField] AudioClip[] deathEffects;
    [Range(0f, 1f)][SerializeField] float deathSoundVolume;
    [SerializeField] AudioClip[] jumpEffects;
    [Range(0f, 1f)][SerializeField] float jumpSoundVolume;
    // Flashlight Stats
    [SerializeField] flashlightStats flashlight;
    [SerializeField] string flashlightButton = "Fire2";
    [SerializeField] KeyCode flashlightKey = KeyCode.F;
    Vector3 flashlightHoldPosition = new Vector3(0.75f, -0.85f, -0.9f);
    Vector3 flashlightLightPosition = new Vector3(0, -0.05f, 0.15f);
    GameObject flashlightModel;
    GameObject flashlightLightObject;
    Light flashlightLight;
    bool hasFlashlight;
    bool flashlightOn;
    float flashlightFlickerTimer;
    float lowBatterySoundTimer;

    //[SerializeField] Transform gunPivot;
    [SerializeField] Transform shootPos;
    int jumpCount;
    int HPOrig;
    int gunInvPos;
    float shootTimer;
    Vector3 moveDir;
    Vector3 playerVel;
    private bool soundPlaying = false;
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
        if (!gameManager.instance.isPaused) {
            movement();
            useFlashlight();
        }
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
            playRandomSound(jumpEffects, jumpSoundVolume);
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
        ScoreboardManager.GetOrCreate().AddShotFired();
        updatePlayerAmmo();
        if (gunInv[gunInvPos].shootSound.Count() > 0) {
            int randomInt = Random.Range(0, gunInv[gunInvPos].shootSound.Count());
            float randomShotVolume = Random.Range(-gunInv[gunInvPos].shootSoundVolume * 0.9f, gunInv[gunInvPos].shootSoundVolume * 0.9f);
            AudioSource.PlayClipAtPoint(gunInv[gunInvPos].shootSound[randomInt], gameObject.transform.position, (gunInv[gunInvPos].shootSoundVolume + randomShotVolume)); 
        }
        GameObject bullet =
            Instantiate(gunInv[gunInvPos].bullet, shootPos.position, shootPos.rotation);

        Damage bulletDamage =
            bullet.GetComponent<Damage>();

        if (bulletDamage != null)
        {
            bulletDamage.SetPlayerBullet(true);
        }
    }
    //==========================================================================================
    // Function, Rload
    //==========================================================================================

    void reload() {
        if (Input.GetButtonDown("Reload") && gunInv.Count > 0) {
            if (gunInv[gunInvPos].reloadSound.Count() > 0) {
                int randomInt = Random.Range(0, gunInv[gunInvPos].shootSound.Count());
                float randomShotVolume = Random.Range(-gunInv[gunInvPos].shootSoundVolume * 0.9f, gunInv[gunInvPos].shootSoundVolume * 0.9f);
                AudioSource.PlayClipAtPoint(gunInv[gunInvPos].reloadSound[randomInt], gameObject.transform.position, (gunInv[gunInvPos].shootSoundVolume + randomShotVolume));
            }
            gunInv[gunInvPos].ammoCurr = gunInv[gunInvPos].ammoMax;
            updatePlayerAmmo();
        }
    }
    //==========================================================================================
    // Function, TakeDamage
    //==========================================================================================
    public void TakeDamage(int amount)
    {
        ScoreboardManager.GetOrCreate().AddDamageTaken(amount);
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
            StartCoroutine(playRandomSound(hurtEffects, hurtSoundVolume));
        }
    }
    //==========================================================================================
    // Function, Play Hurt Sound
    //==========================================================================================
    IEnumerator playRandomSound(AudioClip[] array, float volume) {
        if (!soundPlaying) {
            soundPlaying = true;
            if (array.Count() > 0) {
                int randomInt = Random.Range(0, array.Count());
                float randomVolume = Random.Range(-volume * 0.9f, volume * 0.9f);
                AudioSource.PlayClipAtPoint(array[randomInt], gameObject.transform.position, (volume + randomVolume));
                yield return new WaitForSeconds(array[randomInt].length);
            }
            soundPlaying = false;
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
            playRandomSound(deathEffects, deathSoundVolume);
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
        if (gameManager.instance == null || gameManager.instance.playerHPBAR == null)
        {
            return;
        }

        gameManager.instance.playerHPBAR.fillAmount = (float)HP / HPOrig;
    }
    //==========================================================================================
    // Function, get Gun Stats
    //==========================================================================================
    public void updatePlayerAmmo() {
        if (gameManager.instance == null || gameManager.instance.AmmoCount == null)
        {
            return;
        }

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
    // Function, get Flashlight Stats
    //==========================================================================================
    public void getFlashlightStats(flashlightStats light)
    {
        flashlight = light;
        hasFlashlight = true;
        flashlightOn = true;
        equipFlashlight();
        updateFlashlightUI();
        Debug.Log("Flashlight picked up. Press F or Fire2 to toggle it.");
    }
    //==========================================================================================
    // Function, Equip Flashlight
    //==========================================================================================
    void equipFlashlight()
    {
        if (flashlightModel != null)
        {
            Destroy(flashlightModel);
        }

        if (flashlightLightObject != null)
        {
            Destroy(flashlightLightObject);
        }

        Transform flashlightParent = Camera.main != null ? Camera.main.transform : transform;

        if (flashlight.flashlightModel != null)
        {
            flashlightModel = Instantiate(flashlight.flashlightModel, flashlightParent);
            updateFlashlightModelPosition();
        }
        else
        {
            flashlightModel = new GameObject("Flashlight");
            flashlightModel.transform.SetParent(flashlightParent);
            flashlightModel.transform.localScale = Vector3.one;
            updateFlashlightModelPosition();
        }

        flashlightLightObject = new GameObject("Flashlight Beam");
        flashlightLightObject.transform.SetParent(flashlightParent);
        flashlightLightObject.transform.localPosition = flashlightLightPosition;
        flashlightLightObject.transform.localRotation = Quaternion.identity;
        flashlightLightObject.transform.localScale = Vector3.one;
        flashlightLight = flashlightLightObject.AddComponent<Light>();

        flashlightLight.type = LightType.Spot;
        flashlightLight.color = new Color(1f, 0.92f, 0.72f);
        flashlightLight.renderMode = LightRenderMode.ForcePixel;
        flashlightLight.cullingMask = ~0;
        flashlightLight.enabled = flashlightOn;
        updateFlashlightLight();
    }
    //==========================================================================================
    // Function, Use Flashlight
    //==========================================================================================
    void useFlashlight()
    {
        if (!hasFlashlight || flashlight == null)
        {
            return;
        }

        if (flashlightButtonPressed())
        {
            toggleFlashlight();
        }

        updateFlashlightModelPosition();

        if (!flashlightOn)
        {
            return;
        }

        flashlight.batteryCurr -= flashlight.batteryDrainRate * Time.deltaTime;

        if (flashlight.batteryCurr <= 0)
        {
            flashlight.batteryCurr = 0;
            turnFlashlightOff();
        }

        updateFlashlightLight();
        updateFlashlightUI();
    }
    //==========================================================================================
    // Function, Toggle Flashlight
    //==========================================================================================
    void toggleFlashlight()
    {
        if (flashlight.batteryCurr <= 0)
        {
            turnFlashlightOff();
            return;
        }

        flashlightOn = !flashlightOn;

        if (flashlightLight != null)
        {
            flashlightLight.enabled = flashlightOn;
        }

        updateFlashlightLight();
        Debug.Log(flashlightOn ? "Flashlight turned on." : "Flashlight turned off.");

        if (flashlight.toggleSound.Count() > 0)
        {
            StartCoroutine(playRandomSound(flashlight.toggleSound, flashlight.flashlightSoundVolume));
        }

        updateFlashlightUI();
    }
    //==========================================================================================
    // Function, Flashlight Button Pressed
    //==========================================================================================
    bool flashlightButtonPressed()
    {
        bool pressed = Input.GetKeyDown(flashlightKey);

        if (!string.IsNullOrEmpty(flashlightButton))
        {
            try
            {
                pressed = pressed || Input.GetButtonDown(flashlightButton);
            }
            catch (System.ArgumentException)
            {
                flashlightButton = "";
            }
        }

        return pressed;
    }
    //==========================================================================================
    // Function, Update Flashlight Model Position
    //==========================================================================================
    void updateFlashlightModelPosition()
    {
        if (flashlightModel == null)
        {
            return;
        }

        flashlightModel.transform.localPosition = flashlightHoldPosition;
        flashlightModel.transform.localRotation = Quaternion.identity;
    }
    //==========================================================================================
    // Function, Turn Flashlight Off
    //==========================================================================================
    void turnFlashlightOff()
    {
        flashlightOn = false;

        if (flashlightLight != null)
        {
            flashlightLight.enabled = false;
        }

        updateFlashlightLight();
        updateFlashlightUI();
    }
    //==========================================================================================
    // Function, Update Flashlight Light
    //==========================================================================================
    void updateFlashlightLight()
    {
        if (flashlightLight == null || flashlight == null)
        {
            return;
        }

        flashlightLight.range = flashlight.lightRange;
        flashlightLight.spotAngle = flashlight.lightAngle;

        if (!flashlightOn)
        {
            flashlightLight.intensity = 0;
            return;
        }

        float batteryPercent = flashlight.batteryCurr / Mathf.Max(flashlight.batteryMax, 1);
        bool lowBattery = batteryPercent <= flashlight.lowBatteryPercent;
        bool bloodMoonActive = isBloodMoonActive();
        bool shouldFlicker = lowBattery || bloodMoonActive;

        if (shouldFlicker && Time.time >= flashlightFlickerTimer)
        {
            float flickerChance = lowBattery ? 0.45f : flashlight.bloodMoonFlickerChance;
            bool flickerOff = Random.Range(0f, 1f) < flickerChance;
            flashlightLight.intensity = flickerOff ? flashlight.lightIntensity * 0.2f : flashlight.lightIntensity;
            flashlightFlickerTimer = Time.time + 0.08f;
        }
        else if (!shouldFlicker)
        {
            flashlightLight.intensity = flashlight.lightIntensity;
        }

        if (lowBattery && Time.time >= lowBatterySoundTimer && flashlight.batteryLowSound.Count() > 0)
        {
            StartCoroutine(playRandomSound(flashlight.batteryLowSound, flashlight.flashlightSoundVolume));
            lowBatterySoundTimer = Time.time + 3;
        }
    }
    //==========================================================================================
    // Function, Is Blood Moon Active
    //==========================================================================================
    bool isBloodMoonActive()
    {
        waveManager manager = FindAnyObjectByType<waveManager>();
        return manager != null && manager.ActiveBloodMoonModifier != null;
    }
    //==========================================================================================
    // Function, Update Flashlight UI
    //==========================================================================================
    void updateFlashlightUI()
    {
        if (gameManager.instance == null || gameManager.instance.FlashlightCount == null)
        {
            return;
        }

        if (!hasFlashlight || flashlight == null)
        {
            gameManager.instance.FlashlightCount.text = "Flashlight: --";
            return;
        }

        string lightState = flashlightOn ? "On" : "Off";
        gameManager.instance.FlashlightCount.text = $"Flashlight: {flashlight.batteryCurr:F0}% {lightState}";
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
