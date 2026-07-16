//==============================================================================================
// Using Unity Engine
//==============================================================================================
using Bloodroot.Features.BloodMoon;
using UnityEngine;
using System.Collections;
using UnityEngine.AI;
using System.Collections.Generic;
//==============================================================================================
// Declare Enemy AI
//==============================================================================================
public class enemyAI : MonoBehaviour, IDamage
{
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] float HP;
    [SerializeField] Renderer model;
    [SerializeField] NavMeshAgent agent;
    [SerializeField] int FOV;
    // Roam Stats
    [SerializeField] int roamDist;
    [SerializeField] int roamPauseTime;
    // Weapon Stats
    [SerializeField] GameObject bullet;
    [SerializeField] Transform gunPivot;
    [SerializeField] Transform shootPos;
    [SerializeField] float shootRate;
    [SerializeField] int gunRotateSpeed;
    [SerializeField] int faceTargetSpeed;
    [SerializeField] MobSpawner spawner;
    [SerializeField] float damageMultiplier;  
    [SerializeField] bool isMelee;
    [SerializeField] float meleeDamage;
    [SerializeField] float meleeRange;
    [SerializeField] float healthGrowth = 1.15f;
    [SerializeField] float damageGrowth = 1.08f;
    Color colorOrig;
    Vector3 playerDir;
    Vector3 startingPos;
    float shootTimer;
    bool playerInTrigger;
    waveManager manager;
    BoarBruteAI boarBrute;
    float chargeCooldown = 5f;
    float chargeCooldownTimer;
    bool isDead;
    float angleToPlayer;
    float roamTimer;
    float stoppingDistanceOrig;
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    void Start()
    {
        boarBrute = GetComponent<BoarBruteAI>();
        if (model != null) { colorOrig = model.material.color; }
        manager = FindAnyObjectByType<waveManager>();
        ApplyBloodMoonModifier();
        if (gameManager.instance != null) { 
            gameManager.instance.updateGameGoal(1);
            startingPos = transform.position;
            stoppingDistanceOrig = agent.stoppingDistance;
        }
    }
    //==========================================================================================
    // Function, Roam
    //==========================================================================================
    void checkRoam()
    {
        if (agent.remainingDistance < 0.01f)
        {
            roamTimer += Time.deltaTime;
            if (roamTimer > roamPauseTime) { roam(); }
        }
    }
    //==========================================================================================
    // Function, Roam
    //==========================================================================================
    void roam() {
        roamTimer = 0;
        agent.stoppingDistance = 0;
        Vector3 ranPos = Random.insideUnitSphere * roamDist;
        ranPos += startingPos;
        NavMeshHit hit;
        NavMesh.SamplePosition(ranPos, out hit, roamDist, 1);
        agent.SetDestination(hit.position);
    }
    //==========================================================================================
    // Function, ApplyBloodMoonModifier
    //==========================================================================================

    private void ApplyBloodMoonModifier()
    {
        damageMultiplier = 1f;

        if (manager == null)
            return;

        BloodMoonModifier modifier =
            manager.ActiveBloodMoonModifier;

        // A null modifier means this is a normal wave.
        if (modifier == null)
            return;

        HP = Mathf.Max(
            1,
            Mathf.CeilToInt(
                modifier.ModifyHealth(HP)));

        if (agent != null)
        {
            agent.speed =
                modifier.ModifySpeed(agent.speed);
        }

        damageMultiplier =
            modifier.ModifyDamage(1f);

        meleeDamage =
            Mathf.Max(0, Mathf.CeilToInt(
                modifier.ModifyDamage(meleeDamage)));
    }

    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void Update()
    {
        if (isDead)
            return;

        if (playerInTrigger)
        {
            ScreecherAI screecher = GetComponent<ScreecherAI>();
            if (screecher != null)
                screecher.Scream();
            bool isCharging =
                boarBrute != null && boarBrute.charging;

            if (!isCharging &&
                agent != null &&
                agent.isActiveAndEnabled &&
                agent.isOnNavMesh)
            {
                agent.SetDestination(
                    gameManager.instance.player.transform.position);
            }

            playerDir = gameManager.instance.player.transform.position - transform.position;

            if (screecher != null)
            {
                float distance = playerDir.magnitude;

            
                if (distance <= 10f && screecher.CanScream())
                {
                    screecher.Scream();
                }
                faceTarget();
            }
            if (boarBrute != null && !isCharging)
            {
                chargeCooldownTimer += Time.deltaTime;
                if (chargeCooldownTimer >= chargeCooldown && playerDir.magnitude > meleeRange * 2f)
                {
                    boarBrute.StartCharge();
                    chargeCooldownTimer = 0f;
                }
            }

            if (isMelee)
            {
                if (!isCharging)
                {
                    shootTimer += Time.deltaTime;
                    if (shootTimer >= shootRate && playerDir.magnitude <= meleeRange)
                    {
                        MeleeAttack();
                    }
                }
            }
            else
            {
                if (gunPivot == null || shootPos == null || bullet == null) { return; }
                shootTimer += Time.deltaTime;
                rotateGun();
                if (shootTimer >= shootRate)
                {
                    shoot();
                }
            }
        } else if (!playerInTrigger) {
            checkRoam();
        }
        else if (playerInTrigger && !canSeePlayer()) {
            checkRoam();
        }
    }

    void MeleeAttack()
    {
        shootTimer = 0;
        IDamage dmg = gameManager.instance.player.GetComponent<IDamage>();
        if (dmg != null)
        {
            dmg.TakeDamage(Mathf.RoundToInt(meleeDamage));
        }
    }
    //==========================================================================================
    // Function, On Trigger Enter
    //==========================================================================================
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInTrigger = true;
        }
    }
    //==========================================================================================
    // Function, On Trigger Exit
    //==========================================================================================
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInTrigger = false;
            agent.stoppingDistance = 0;
        }
    }
    //==========================================================================================
    // Function, Shoot
    //==========================================================================================
    void shoot()
    {
        shootTimer = 0;

        Quaternion bulletRotation = gunPivot.rotation;
        if (gameManager.instance != null && gameManager.instance.player != null)
        {
            Vector3 bulletDir =
                gameManager.instance.player.transform.position -
                shootPos.position;

            if (bulletDir.sqrMagnitude > 0.001f)
            {
                bulletRotation = Quaternion.LookRotation(bulletDir);
            }
        }

        GameObject spawnedBullet = Instantiate(bullet, shootPos.position, bulletRotation);
        Damage dmg = spawnedBullet.GetComponent<Damage>();
        if(dmg != null)
        {
            dmg.SetDamageMultiplier(damageMultiplier);
        }
    }
    //==========================================================================================
    // Function, Face Target
    //==========================================================================================
    void faceTarget()
    {
        Quaternion rot = Quaternion.LookRotation(new Vector3(playerDir.x, 0, playerDir.z));
        transform.rotation = Quaternion.Lerp(transform.rotation, rot, faceTargetSpeed * Time.deltaTime);
    }
    //==========================================================================================
    // Function, Rotate Gun
    //==========================================================================================
    void rotateGun()
    {
        Quaternion rot = Quaternion.LookRotation(playerDir);
        gunPivot.rotation = Quaternion.Lerp(gunPivot.rotation, rot, gunRotateSpeed * Time.deltaTime);
    }
    //==========================================================================================
    // Function, TakeDamage
    //==========================================================================================
    public void TakeDamage(int amount)
    {
        if (isDead)
            return;

        HP -= amount;

        if (HP <= 0)
        {
            // Die reports the death to WaveManager,
            // then starts the dissolve effect.
            Die();
        }
        else
        {
            StartCoroutine(flashRed());
        }
    }
    //==========================================================================================
    // Function, Alert
    //==========================================================================================
    public void Alert(Vector3 pos)
    {
        if (isDead)
            return;

        playerInTrigger = true;
    }
    //==========================================================================================
    // Function, Die
    //==========================================================================================

    private void Die()
    {
        // Prevent one enemy from being counted twice.
        if (isDead)
            return;

        isDead = true;
        playerInTrigger = false;

        foreach (Collider enemyCollider in GetComponentsInChildren<Collider>())
        {
            enemyCollider.enabled = false;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        // This reduces enemiesRemaining and allows the
        // WaveManager to start the following wave.
        if (manager == null)
        {
            manager = FindAnyObjectByType<waveManager>();
        }

        if (manager != null && manager.waveActive)
        {
            manager.EnemyDefeated(gameObject);
        }

        if (gameManager.instance != null)
        {
            gameManager.instance.updateGameGoal(-1);
        }

        Dissolver dissolver =
            GetComponent<Dissolver>();

        if (dissolver != null)
        {
            dissolver.StartCoroutine(
                dissolver.dissolve());
        }
        else
        {
            Destroy(gameObject);
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

    public void onDeath(bool dead)
    {
        if (dead)
        {
            Die();
        }
    }
    //==========================================================================================
    // Function, InitializeEnemy
    //==========================================================================================
    public void InitializeEnemy(int wave)
    {
        HP = 10 * Mathf.Pow(healthGrowth, wave - 1);

        meleeDamage = 3 *Mathf.Pow(damageGrowth, wave - 1);

    }
    //==========================================================================================
    // Function, Can See Player
    //==========================================================================================
    bool canSeePlayer() {
        shootTimer += Time.deltaTime;
        playerDir = gameManager.instance.player.transform.position - transform.position;
        angleToPlayer = Vector3.Angle(playerDir, transform.forward);
        Debug.DrawRay(transform.position, playerDir, Color.red);
        // Hey I see you!!
        RaycastHit hit;
        if (Physics.Raycast(transform.position, playerDir, out hit)) {
            if (hit.collider.CompareTag("Player") && angleToPlayer <= FOV) {
                agent.SetDestination(gameManager.instance.player.transform.position);
                rotateGun();
                faceTarget();
                if (shootTimer >= shootRate) {
                    shoot();
                }
                agent.stoppingDistance = stoppingDistanceOrig;
                return true;
            }
        }
        agent.stoppingDistance = 0;
        return false;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Enemy AI .cs
//==============================================================================================
