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
public class enemyAI : MonoBehaviour, IDamage
{
    private const float NavMeshRecoveryRadius = 6f;

    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] protected float HP;
    [SerializeField] protected GameObject[] drops;
    [SerializeField] protected GameObject genericPickupShell;
    [SerializeField] protected Renderer model;
    [SerializeField] public NavMeshAgent agent;
    [SerializeField] public Animator animator;
    [SerializeField] protected int FOV;
    // Roam Stats
    [SerializeField] protected int roamDist;
    [SerializeField] protected int roamPauseTime;
    [SerializeField] protected int stoppingDistance;
    // Weapon Stats
    [SerializeField] protected GameObject bullet;
    [SerializeField] protected Transform gunPivot;
    [SerializeField] protected Transform shootPos;
    [SerializeField] protected float shootRate;
    [SerializeField] protected int gunRotateSpeed;
    [SerializeField] protected int faceTargetSpeed;
    [SerializeField] protected MobSpawner spawner;
    [SerializeField] protected float damageMultiplier;  
    [SerializeField] protected bool isMelee;
    [SerializeField] protected float meleeDamage;
    [SerializeField] protected float meleeRange;
    [SerializeField] protected float healthGrowth = 1.15f;
    [SerializeField] protected float damageGrowth = 1.08f;
    [SerializeField, Range(100f, 1000f)] int sightRange;
    protected Color colorOrig;
    protected Vector3 playerDir;
    protected Vector3 startingPos;
    protected float shootTimer;
    protected bool playerInTrigger;
    protected waveManager manager;
    protected BoarBruteAI boarBrute;
    protected float chargeCooldown = 5f;
    protected float chargeCooldownTimer;
    protected bool isDead;
    protected float angleToPlayer;
    protected float roamTimer;
    protected float stoppingDistanceOrig;
    protected bool isCharging;
    protected bool isUnalived;
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    protected virtual void Start() {
        if (agent == null) { agent = GetComponent<NavMeshAgent>(); }
        animator = GetComponentInChildren<Animator>();
        isUnalived = false;
        boarBrute = GetComponent<BoarBruteAI>();
        if (model != null) { colorOrig = model.material.color; }
        manager = FindAnyObjectByType<waveManager>();
        ApplyBloodMoonModifier();
    }
    //==========================================================================================
    // Function, Roam
    //==========================================================================================
    protected virtual void checkRoam()
    {
        if (!EnemyNavMeshSafety.TryRecover(
                agent,
                transform.position,
                NavMeshRecoveryRadius)) { return; }

        if (!isDead)
        {
            if (agent.remainingDistance < 0.01f)
            {
                roamTimer += Time.deltaTime;
                if (roamTimer > roamPauseTime) { roam(); }
            }
        }
    }
    //==========================================================================================
    // Function, Roam
    //==========================================================================================
    protected virtual void roam() {
        if (!EnemyNavMeshSafety.TryRecover(
                agent,
                transform.position,
                NavMeshRecoveryRadius)) { return; }

        roamTimer = 0;
        agent.stoppingDistance = 0;
        Vector3 ranPos = Random.insideUnitSphere * roamDist;
        ranPos += startingPos;
        EnemyNavMeshSafety.TrySetDestination(
            agent,
            ranPos,
            NavMeshRecoveryRadius,
            Mathf.Max(0.1f, roamDist));
    }
    //==========================================================================================
    // Function, ApplyBloodMoonModifier
    //==========================================================================================

    protected virtual void ApplyBloodMoonModifier()
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
    protected virtual void Update() {
        if (isDead) { return; }
        if (!EnemyNavMeshSafety.TryRecover(
                agent,
                transform.position,
                NavMeshRecoveryRadius)) { return; }

        if (animator != null) {
            animator.SetFloat("Speed", agent.velocity.magnitude);
            animator.SetFloat(
                "Speed",
                agent.velocity.magnitude / Mathf.Max(0.01f, agent.speed),
                0.1f,
                Time.deltaTime);
        }
        if (playerInTrigger) {
            playerDir = gameManager.instance.player.transform.position - transform.position;
            ScreecherAI screecher = GetComponent<ScreecherAI>();
            if (screecher != null) {
                float distance = playerDir.magnitude;
                if (distance <= 10f && screecher.CanScream() && !isUnalived)
                {
                    if (animator != null) { animator.SetTrigger("Roar"); }
                    screecher.Scream();
                }
                faceTarget();
            }
        } else {
            checkRoam(); 
        }
    }

    protected virtual void MeleeAttack()
    {
        shootTimer = 0;
        if (gameManager.instance == null || gameManager.instance.player == null) { return; }

        IDamage dmg = gameManager.instance.player.GetComponent<IDamage>();
        if (dmg != null)
        {
            dmg.TakeDamage(Mathf.RoundToInt(meleeDamage));
        }
    }
    //==========================================================================================
    // Function, On Trigger Enter
    //==========================================================================================
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInTrigger = true;
        }
    }
    //==========================================================================================
    // Function, On Trigger Exit
    //==========================================================================================
    protected virtual void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInTrigger = false;
            if (EnemyNavMeshSafety.IsReady(agent)) { agent.stoppingDistance = 0; }
        }
    }
    //==========================================================================================
    // Function, Shoot
    //==========================================================================================
    protected virtual void shoot()
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
    public virtual void TakeDamage(int amount) {
        if (isDead)
            return;
        HP -= amount;
        if (HP <= 0) {
            // Die reports the death to WaveManager,
            // then starts the dissolve effect.
            Die();
            GetComponent<EnemyAudioControl>().PlayDeathSound();
        } else {
            StartCoroutine(flashRed());
        }
    }
    //==========================================================================================
    // Function, Alert
    //==========================================================================================
    public virtual void Alert(Vector3 pos) {
        if (isDead) { return; }
        playerInTrigger = true;
    }
    //==========================================================================================
    // Function, Die
    //==========================================================================================

    protected virtual void Die() {
        // Prevent one enemy from being counted twice.
        isUnalived = true;
        if (isDead) { return; }
        isDead = true;
        playerInTrigger = false;
        //ScoreboardManager.GetOrCreate().AddEnemyPigKilled();
        //foreach (Collider enemyCollider in GetComponentsInChildren<Collider>()) {
        //    enemyCollider.enabled = false;
        //}
        //if (agent != null && agent.isOnNavMesh) {
        //    agent.isStopped = true;
        //}
        // This reduces enemiesRemaining and allows the
        // WaveManager to start the following wave.
        if (manager == null) {
            manager = FindAnyObjectByType<waveManager>();
        }
        if (manager != null && manager.waveActive) {
            manager.EnemyDefeated(gameObject);
        }
        Dissolver dissolver = GetComponent<Dissolver>();
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
        if (dissolver != null) {
            dissolver.StartCoroutine(dissolver.dissolve(true));
        } else {
            Destroy(gameObject);
        }
    }
    //==========================================================================================
    // Function, Copy Item
    //------------------------------------------------------------------------------------------
    private ItemStats CopyItem(ItemStats source, int qty) {
        return new ItemStats {
            itemName = source.itemName,
            itemDescription = source.itemDescription,
            icon = source.icon,
            weight = source.weight,
            quantity = qty,
            stackSize = source.stackSize,
            itemMesh = source.itemMesh,
            pickupSound = source.pickupSound,
            itemIncreases = source.itemIncreases,
            itemID = source.itemID
        };
    }
    //==========================================================================================
    // Function, Flash
    //==========================================================================================
    IEnumerator flashRed() {
        //model.material.color = Color.red;
        //yield return new WaitForSeconds(0.1f);
        //model.material.color = colorOrig;
        if (this.GetComponent<Dissolver>() != null) { this.GetComponent<Dissolver>().StartCoroutine(this.GetComponent<Dissolver>().dissolveFlash()); }
        yield return null;
    }
    //==========================================================================================
    // Function, On Death
    //==========================================================================================
    public virtual void onDeath(bool dead) {
        if (dead) { Die(); }
    }
    //==========================================================================================
    // Function, InitializeEnemy
    //==========================================================================================
    public virtual void InitializeEnemy(int wave) {
        HP = 10 * Mathf.Pow(healthGrowth, wave - 1);
        meleeDamage = 3 *Mathf.Pow(damageGrowth, wave - 1);
    }
    //==========================================================================================
    // Function, Can See Player
    //==========================================================================================
    protected virtual bool canSeePlayer() {
        if (!EnemyNavMeshSafety.TryRecover(
                agent,
                transform.position,
                NavMeshRecoveryRadius) ||
            gameManager.instance == null ||
            gameManager.instance.player == null) { return false; }

        shootTimer += Time.deltaTime;
        playerDir = gameManager.instance.player.transform.position - transform.position;
        angleToPlayer = Vector3.Angle(playerDir, transform.forward);

        // Hey I see you!!
        RaycastHit hit;
        if (Physics.Raycast(transform.position, playerDir, out hit, sightRange)) {
            if (hit.collider.CompareTag("Player") && angleToPlayer <= FOV) {
                if (!EnemyNavMeshSafety.TrySetDestination(
                        agent,
                        gameManager.instance.player.transform.position,
                        NavMeshRecoveryRadius,
                        Mathf.Max(1f, stoppingDistanceOrig))) { return false; }
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
