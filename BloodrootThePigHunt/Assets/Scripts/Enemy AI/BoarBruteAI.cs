//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using UnityEngine.AI;

//==============================================================================================
// Declare Boar Brute AI
//==============================================================================================
public class BoarBruteAI : enemyAI
{
    // Serialized Variables
    [SerializeField] float chargeSpeed;
    [SerializeField] float chargeTime;
    [Header("Run Away")]
    [SerializeField] int circlePoints = 8;
    [SerializeField] float waypointTolerance = 0.5f;
    [SerializeField] float sampleTolerance = 2f;
    [Range(9, 100)] [SerializeField] int randomChanceComplete;
    Coroutine runAwayRoutine;
    [SerializeField] int runDistance;
    // Sound Effects
    //[SerializeField] AudioClip[] chargeSound;
    //[SerializeField, Range(0f, 1f)] float chargeSoundVolume;
    //[SerializeField] AudioClip[] damageSounds;
    //[SerializeField, Range(0f, 1f)] float damageSoundVolume;
    //[SerializeField] AudioClip[] trotSounds;
    //[SerializeField, Range(0f, 1f)] float trotSoundVolume;
    // Non-serialized Variables
    public bool charging;
    float timer;
    private bool shouldUpdate;

    private bool HasActiveAgentOnNavMesh()
    {
        return agent != null
            && agent.enabled
            && agent.isActiveAndEnabled
            && agent.isOnNavMesh;
    }

    private bool TryRestoreAgentToNavMesh()
    {
        if (agent == null || !agent.gameObject.activeInHierarchy) { return false; }
        if (HasActiveAgentOnNavMesh()) { return true; }

        float recoveryRadius = Mathf.Max(
            sampleTolerance,
            Mathf.Abs(chargeSpeed) * Mathf.Max(chargeTime, 0f) + agent.radius);

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, recoveryRadius, agent.areaMask))
        {
            return false;
        }

        // The charge moves the transform manually while the agent is disabled.
        // Re-enable only after placing the transform on a valid NavMesh point.
        agent.enabled = false;
        transform.position = hit.position;
        agent.enabled = true;
        return HasActiveAgentOnNavMesh();
    }

    private bool TrySetAgentDestination(Vector3 destination)
    {
        return HasActiveAgentOnNavMesh() && agent.SetDestination(destination);
    }

    //==========================================================================================
    // Function Override, Start
    //==========================================================================================
    protected override void Start() {
        base.Start();
        shouldUpdate = true;
        if (agent == null) { agent = GetComponent<NavMeshAgent>(); }
    }
    //==========================================================================================
    // Function Override, Update
    //==========================================================================================
    protected override void Update() {
        // enemyAI.Update reads agent state, so do not enter it while this boar is
        // charging with its NavMeshAgent intentionally disabled.
        if (charging)
        {
            if (gameManager.instance != null && gameManager.instance.player != null)
            {
                playerDir = gameManager.instance.player.transform.position - transform.position;
            }

            StartCharge();
            return;
        }

        if (!TryRestoreAgentToNavMesh()) { return; }

        base.Update();
        if (playerInTrigger) { if (shouldUpdate) { StartCharge(); } } else { checkRoam(); }
    }
    //==========================================================================================
    // Function Override, Run Away
    //==========================================================================================
    protected virtual void runAway() {
        charging = false;
        timer = 0f;
        shouldUpdate = false;
        if (!TryRestoreAgentToNavMesh())
        {
            shouldUpdate = true;
            return;
        }
        if (runAwayRoutine != null) { StopCoroutine(runAwayRoutine); }
        runAwayRoutine = StartCoroutine(RunAwayInCircle());
    }
    //==========================================================================================
    // Run a full loop around the player, then charge back in
    //==========================================================================================
    private System.Collections.IEnumerator RunAwayInCircle() {
        Vector3 playerPos = gameManager.instance.player.transform.position;
        Vector3 startOffset = transform.position - playerPos;
        startOffset.y = 0f;
        float startAngle = Mathf.Atan2(startOffset.z, startOffset.x);
        for (int i = 1; i < circlePoints; i++) {
            if (!HasActiveAgentOnNavMesh()) { yield break; }
            int rand = Random.Range(0, randomChanceComplete) + 1;
            if (rand <= 8) { continue; }
            playerPos = gameManager.instance.player.transform.position;
            float angle = startAngle + (i / (float)circlePoints) * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 candidatePoint = playerPos + dir * runDistance;
            Vector3 waypoint;
            if (NavMesh.SamplePosition(candidatePoint, out NavMeshHit hit, sampleTolerance, NavMesh.AllAreas)) {
                waypoint = hit.position;
            } else {
                waypoint = transform.position;
            }
            if (!TrySetAgentDestination(waypoint)) { yield break; }
            while (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh
                   && (agent.pathPending || agent.remainingDistance > waypointTolerance)) {
                yield return null;
            }
        }
        shouldUpdate = true;
    }
    //==========================================================================================
    // Function, Get Random Point
    //==========================================================================================
    protected virtual Vector3 getRandomPoint()
    {
        Vector3 result;
        Vector3 playerPos = gameManager.instance.player.transform.position;
        Vector2 randomCircle = Random.insideUnitCircle.normalized;
        Vector3 randomDir = new Vector3(randomCircle.x, 0f, randomCircle.y);
        Vector3 candidatePoint = playerPos + randomDir * runDistance;
        if (NavMesh.SamplePosition(candidatePoint, out NavMeshHit hit, sampleTolerance, NavMesh.AllAreas)) {
            result = hit.position;
            return result;
        }
        result = transform.position;
        return result;
    }
    //==========================================================================================
    // Start Charge
    //==========================================================================================
    public virtual void StartCharge() {
        if (isUnalived || gameManager.instance == null || gameManager.instance.player == null)
        {
            charging = false;
            timer = 0f;
            return;
        }

        if (!charging) {
            if (!TryRestoreAgentToNavMesh())
            {
                timer = 0f;
                return;
            }

            Vector3 dirToPlayer = gameManager.instance.player.transform.position - transform.position;
            dirToPlayer.y = 0f;
            if (dirToPlayer.sqrMagnitude > 0.0001f) { transform.forward = dirToPlayer.normalized; }
            agent.enabled = false;
        }
        charging = true;
        performCharge();
    }

    //==========================================================================================
    // Perform Charge
    //==========================================================================================
    protected virtual void performCharge() {
        if (!charging && gameManager.instance != null && gameManager.instance.player != null) {
            TrySetAgentDestination(gameManager.instance.player.transform.position);
        }
        if (charging) {
            timer += Time.deltaTime;
            transform.position += transform.forward * chargeSpeed * Time.deltaTime;
            if (timer >= chargeTime) {
                charging = false;
                timer = 0f;
                if (!TryRestoreAgentToNavMesh())
                {
                    return;
                }

                if (gameManager.instance != null && gameManager.instance.player != null) {
                    TrySetAgentDestination(gameManager.instance.player.transform.position);
                }
            }
        }
        if (!charging) {
            chargeCooldownTimer += Time.deltaTime;
            if (chargeCooldownTimer >= chargeCooldown && playerDir.magnitude > meleeRange * 2f && !isUnalived) {
                chargeCooldownTimer = 0f;
                StartCharge();
                return;
            }
        }
        if (isMelee) {
            if (charging) {
                shootTimer += Time.deltaTime;
                if (shootTimer >= shootRate && playerDir.magnitude <= meleeRange && !isUnalived) {
                    MeleeAttack();
                }
            }
        } else {
            if (gunPivot == null || shootPos == null || bullet == null) { return; }
            shootTimer += Time.deltaTime;
            rotateGun();
            if (shootTimer >= shootRate && !isUnalived)
            {
                shoot();
            }
        }
    }
    //==========================================================================================
    // Function Override, Can See Player
    //==========================================================================================
    protected override bool canSeePlayer()
    {
        shootTimer += Time.deltaTime;
        playerDir = gameManager.instance.player.transform.position - transform.position;
        angleToPlayer = Vector3.Angle(playerDir, transform.forward);

        RaycastHit hit;
        if (Physics.Raycast(transform.position, playerDir, out hit))
        {
            if (hit.collider.CompareTag("Player") && angleToPlayer <= FOV)
            {
                if (!TrySetAgentDestination(gameManager.instance.player.transform.position)) { return false; }
                rotateGun();
                faceTarget();
                if (shootTimer >= shootRate)
                {
                    shoot();
                }
                agent.stoppingDistance = stoppingDistanceOrig;
                return true;
            }
        }
        if (HasActiveAgentOnNavMesh()) { agent.stoppingDistance = 0; }
        return false;
    }
    //==========================================================================================
    // Function Override, Melee Attack
    //==========================================================================================
    protected override void MeleeAttack() {
        try {
            int rand = Random.Range(0, 1);
            if (rand == 0) {
                animator.SetTrigger("Attack1");
            } else {
                animator.SetTrigger("Attack2");
            }

            base.MeleeAttack();
        } finally {
            runAway();
        }
    }

    public override void TakeDamage(int amount) {
        animator.SetTrigger("Damage");
        base.TakeDamage(amount);
    }

    protected override void Die()
    {
        animator.SetTrigger("Die");
        base.Die();
    }

    //==========================================================================================
}
//==============================================================================================
// End of BoarBruteAI.cs
//==============================================================================================
