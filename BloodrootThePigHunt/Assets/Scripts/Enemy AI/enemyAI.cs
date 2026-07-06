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
    [SerializeField] int HP;
    [SerializeField] Renderer model;
    [SerializeField] NavMeshAgent agent;
    [SerializeField] GameObject bullet;
    [SerializeField] Transform gunPivot;
    [SerializeField] Transform shootPos;
    [SerializeField] float shootRate;
    [SerializeField] int gunRotateSpeed;
    [SerializeField] int faceTargetSpeed;
    [SerializeField] MobSpawner spawner;
    [SerializeField] float damageMultiplier;  
    [SerializeField] bool isMelee;
    [SerializeField] int meleeDamage;
    [SerializeField] float meleeRange;
    Color colorOrig;
    Vector3 playerDir;
    float shootTimer;
    bool playerInTrigger;
    waveManager manager;
    BoarBruteAI boarBrute;
    float chargeCooldown = 5f;
    float chargeCooldownTimer;
    bool isDead;
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    void Start()
    {
        boarBrute = GetComponent<BoarBruteAI>();
        if (model != null) 
        { colorOrig = model.material.color; }

        manager = FindAnyObjectByType<waveManager>();

        ApplyBloodMoonModifier();

        if (gameManager.instance != null)
        { gameManager.instance.updateGameGoal(1); }
    }
    //==========================================================================================
    // Function, ApplyBloodMoonModifier
    //==========================================================================================

    private void ApplyBloodMoonModifier()
    {
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
    }

    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void Update()
    {
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
                if (gunPivot == null || shootPos == null || bullet == null)
                    return;

                shootTimer += Time.deltaTime;
                rotateGun();
                if (shootTimer >= shootRate)
                {
                    shoot();
                }
            }
        }
    }

    void MeleeAttack()
    {
        shootTimer = 0;
        IDamage dmg = gameManager.instance.player.GetComponent<IDamage>();
        if (dmg != null)
        {
            dmg.TakeDamage(meleeDamage);
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
        }
    }
    //==========================================================================================
    // Function, Shoot
    //==========================================================================================
    void shoot()
    {
        shootTimer = 0;
       GameObject spawnedBullet = Instantiate(bullet, shootPos.position, gunPivot.rotation);
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
        gunPivot.rotation = Quaternion.Lerp(gunPivot.rotation, rot, shootRate * Time.deltaTime);
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

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        // This reduces enemiesRemaining and allows the
        // WaveManager to start the following wave.
        if (manager != null)
        {
            manager.EnemyDefeated();
        }
        else
        {
            Debug.LogWarning(
                "Enemy could not report its death " +
                "because WaveManager was not found.");
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
}
    //==========================================================================================
//==============================================================================================
// End of Enemy AI .cs
//==============================================================================================
