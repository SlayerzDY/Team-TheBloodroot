//==============================================================================================
// Using Unity Engine
//==============================================================================================
using Bloodroot.Features.BloodMoon;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
//==============================================================================================
// Declare Enemy AI
//==============================================================================================
public class juggernautEnemyAI : MonoBehaviour, IDamage
{
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] float HP;
    [SerializeField] GameObject[] drops;
    [SerializeField] private GameObject genericPickupShell;
    [SerializeField] Renderer model;
    [SerializeField] public NavMeshAgent agent;
    [SerializeField] public Animator animator;
    [SerializeField] int FOV;
    // Roam Stats
    [SerializeField] int roamDist;
    [SerializeField] int roamPauseTime;
    [SerializeField] int stoppingDistance;
    // Weapon Stats
    [SerializeField] GameObject bullet;
    [SerializeField] Transform gunPivot;
    [SerializeField] Transform shootPos;
    [SerializeField] float shootRate;
    [SerializeField] int gunRotateSpeed;
    [SerializeField] int faceTargetSpeed;
    [SerializeField] MobSpawner spawner;
    [SerializeField] float damageMultiplier;  
    [SerializeField] float healthGrowth = 1.15f;
    //[SerializeField] float damageGrowth = 1.08f;
    Vector3 playerDir;
    Vector3 startingPos;
    float shootTimer;
    bool playerInTrigger;
    waveManager manager;
    bool isDead;
    float angleToPlayer;
    float roamTimer;
    float stoppingDistanceOrig;
    //private bool isUnalived;
    private bool isRunning;
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    protected virtual void Start() {
        stoppingDistanceOrig = stoppingDistance;
        animator = GetComponentInChildren<Animator>();
        //isUnalived = false;
        manager = FindAnyObjectByType<waveManager>();
        startingPos = transform.position;
        agent.updateRotation = false;
        ApplyBloodMoonModifier();
    }
    //==========================================================================================
    // Function, Roam
    //==========================================================================================
    protected virtual void checkRoam() {
        if (agent.remainingDistance < 0.01f) {
            roamTimer += Time.deltaTime;
            if (roamTimer > roamPauseTime) { roam(); }
        }
    }
    //==========================================================================================
    // Function, Roam
    //==========================================================================================
    protected virtual void roam() {
        roamTimer = 0;
        agent.stoppingDistance = 0;
        Vector3 ranPos = Random.insideUnitSphere * roamDist;
        ranPos += startingPos;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(ranPos, out hit, roamDist, 1)) {
            agent.SetDestination(hit.position);
        }
    }
    //==========================================================================================
    // Function, ApplyBloodMoonModifier
    //==========================================================================================

    protected virtual void ApplyBloodMoonModifier() {
        damageMultiplier = 1f;
        if (manager == null) { return; }
        BloodMoonModifier modifier = manager.ActiveBloodMoonModifier;
        // A null modifier means this is a normal wave.
        if (modifier == null) {return; }
        HP = Mathf.Max(1, Mathf.CeilToInt(modifier.ModifyHealth(HP)));
        if (agent != null) {
            agent.speed = modifier.ModifySpeed(agent.speed);
        }
        damageMultiplier = modifier.ModifyDamage(1f);
    }

    //==========================================================================================
    // Function, Update
    //==========================================================================================
    protected virtual void Update() {
        if (isDead) { return; }
        if (animator != null) { animator.SetFloat("Speed", agent.velocity.magnitude); }
        animator.SetFloat("Speed", agent.velocity.magnitude / agent.speed, 0.1f, Time.deltaTime);
        if (playerInTrigger) {
            if (canSeePlayer()) {

            }
            else
            {
                checkRoam();
            }
        }
    }
    //==========================================================================================
    // Function, On Trigger Enter
    //==========================================================================================
    protected virtual void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            playerInTrigger = true;
        }
    }
    //==========================================================================================
    // Function, On Trigger Exit
    //==========================================================================================
    protected virtual void OnTriggerExit(Collider other) {
        if (other.CompareTag("Player")) {
            playerInTrigger = false;
            agent.stoppingDistance = 0;
        }
    }
    //==========================================================================================
    // Function, Shoot
    //==========================================================================================
    protected virtual void shoot() {
        shootTimer = 0;
        Quaternion bulletRotation = gunPivot.rotation;
        if (gameManager.instance != null && gameManager.instance.player != null) {
            Vector3 bulletDir =
                gameManager.instance.player.transform.position -
                shootPos.position;
            if (bulletDir.sqrMagnitude > 0.001f) {
                bulletRotation = Quaternion.LookRotation(bulletDir);
            }
        }
        GameObject spawnedBullet = Instantiate(bullet, shootPos.position, bulletRotation);
        Damage dmg = spawnedBullet.GetComponent<Damage>();
        if(dmg != null) {
            dmg.SetDamageMultiplier(damageMultiplier);
        }
    }
    //==========================================================================================
    // Function, Face Target
    //==========================================================================================
    protected virtual void faceTarget() {
        Quaternion rot = Quaternion.LookRotation(new Vector3(playerDir.x, 0, playerDir.z));
        transform.rotation = Quaternion.Lerp(transform.rotation, rot, faceTargetSpeed * Time.deltaTime);
    }
    //==========================================================================================
    // Function, Rotate Gun
    //==========================================================================================
    protected virtual void rotateGun() {
        Quaternion rot = Quaternion.LookRotation(playerDir);
        gunPivot.rotation = Quaternion.Lerp(gunPivot.rotation, rot, gunRotateSpeed * Time.deltaTime);
    }
    //==========================================================================================
    // Function, TakeDamage
    //==========================================================================================
    public void TakeDamage(int amount) {
        if (isDead) { return; }
        HP -= amount;
        if (HP <= 0) {
            GetComponent<EnemyAudioControl>().PlayDeathSound();
            Die();
        } else {
            StartCoroutine(flashRed());
        }
    }
    //==========================================================================================
    // Function, Alert
    //==========================================================================================
    protected virtual void Alert(Vector3 pos) {
        if (isDead) { return; }
        playerInTrigger = true;
    }
    //==========================================================================================
    // Function, Die
    //==========================================================================================
    protected virtual void Die()
    {
        // Prevent one enemy from being counted twice.
        //isUnalived = true;
        if (isDead) { return; }
        isDead = true;
        playerInTrigger = false;
        foreach (Collider enemyCollider in GetComponentsInChildren<Collider>()) {
            enemyCollider.enabled = false;
        }
        if (agent != null && agent.isOnNavMesh) {
            agent.isStopped = true;
        }
        // This reduces enemiesRemaining and allows the
        // WaveManager to start the following wave.
        if (manager == null) {
            manager = FindAnyObjectByType<waveManager>();
        }
        if (manager != null && manager.waveActive) {
            manager.EnemyDefeated(gameObject);
        }
        // Handles dropped Items
        for (int i = 0; i < drops.Length; i++) {
            Item item = drops[i].GetComponent<Item>();
            if (item.item.itemName == null) {  continue; }
            Vector2 randomCircle = Random.insideUnitCircle.normalized * 5;
            Vector3 randomPosition = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
            Quaternion localRotation = transform.localRotation;
            genericPickupShell.GetComponent<Item>().item = CopyItem(item.item, item.item.quantity);
            GameObject newPickup = Instantiate(genericPickupShell, randomPosition, localRotation);
            newPickup.GetComponent<Item>().canInteract = true;
            newPickup.GetComponent<Item>().ApplyMeshToSelf();
        }
        Dissolver dissolver = GetComponent<Dissolver>();
        if (dissolver != null) {
            dissolver.StartCoroutine(dissolver.dissolve());
        } else {
            Destroy(gameObject);
        }
    }
    //==========================================================================================
    // Function, Copy Item
    //------------------------------------------------------------------------------------------
    protected virtual ItemStats CopyItem(ItemStats source, int qty) {
        return new ItemStats {
            itemName = source.itemName,
            itemDescription = source.itemDescription,
            icon = source.icon,
            weight = source.weight,
            quantity = qty,
            stackSize = source.stackSize,
            itemMesh = source.itemMesh,
            pickupSound = source.pickupSound,
            itemIncreases = source.itemIncreases
        };
    }
    //==========================================================================================
    // Function, Flash
    //==========================================================================================
    protected virtual IEnumerator flashRed() {
        if (this.GetComponent<Dissolver>() != null) { this.GetComponent<Dissolver>().StartCoroutine(this.GetComponent<Dissolver>().dissolveFlash()); }
        yield return null;
    }
    public virtual void onDeath(bool dead) {
        if (dead) {
            Die();
        }
    }
    //==========================================================================================
    // Function, InitializeEnemy
    //==========================================================================================
    public virtual void InitializeEnemy(int wave) {
        HP = 10 * Mathf.Pow(healthGrowth, wave - 1);
    }
    //==========================================================================================
    // Function, Can See Player
    //==========================================================================================
    protected virtual bool canSeePlayer()
    {
        shootTimer += Time.deltaTime;
        playerDir = gameManager.instance.player.transform.position - transform.position;
        float distToPlayer = playerDir.magnitude;
        angleToPlayer = Vector3.Angle(playerDir, transform.forward);


        RaycastHit hit;
        if (Physics.Raycast(transform.position, playerDir, out hit))
        {
            if (hit.collider.CompareTag("Player") && angleToPlayer <= FOV)
            {
                rotateGun();
                faceTarget();

                Vector3 dirToPlayer = playerDir.normalized;

                if (distToPlayer > stoppingDistanceOrig)
                {
                    // too far: approach with flank offset
                    Quaternion flankOffset = Quaternion.Euler(0, 30f, 0);
                    Vector3 flankedDir = flankOffset * dirToPlayer;
                    Vector3 stopPoint = gameManager.instance.player.transform.position - flankedDir * stoppingDistanceOrig;
                    agent.SetDestination(stopPoint);
                }
                else if (distToPlayer < stoppingDistanceOrig * 0.9f)
                {
                    // too close: back away to hold range
                    Vector3 retreatPoint = transform.position - dirToPlayer * (stoppingDistanceOrig - distToPlayer);
                    agent.SetDestination(retreatPoint);
                }
                else
                {
                    // right in the pocket: hold still
                    agent.ResetPath();
                }

                if (shootTimer >= shootRate)
                {
                    shoot();
                }
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
