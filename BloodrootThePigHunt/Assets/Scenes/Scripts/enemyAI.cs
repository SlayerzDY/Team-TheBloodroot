//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
using UnityEditor;
//==============================================================================================
// Declare Enemy AI
//==============================================================================================
public class EnemyAI : MonoBehaviour, IDamage {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] int HP;
    [SerializeField] float moveSpeed;
    [SerializeField] float detectionRange;
    [SerializeField] float attackRange;
    [SerializeField] int attackDamage;
    [SerializeField] float attackRate;
    [SerializeField] Renderer model;
    [SerializeField] Dissolver dissolver;
    [SerializeField] MobSpawner spawner;

    Transform player;
    Color colorOrig;
    float attackTimer;

    enum State { 
        Idle,Chase,Attack,Dead
    }
    State currentState = State.Idle;
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    void Start()
    {
        if (model != null)
        {
            colorOrig = model.material.color;
        }
        if (dissolver == null)
        {
            dissolver = GetComponent<Dissolver>();
        }
        if (spawner == null)
        {
            spawner = FindAnyObjectByType<MobSpawner>();
        }

        player = gameManager.instance.player.transform;
    }
    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void Update() {

        if (currentState == State.Dead)
        {
            return;
        }
        float dist = Vector3.Distance(transform.position, player.position);

        if (dist <= attackRange)
        {
            currentState = State.Attack;
        }
        else if (dist <= detectionRange)
        {
            currentState = State.Chase;
        }
        else
        {
            currentState = State.Chase;
        }
        switch (currentState)
        {
           
            case State.Chase:
                {
                    Chase();
                    break;
                }
            case State.Attack: 
                { 
                Attack(); 
                  break;
                
                }
        }

        attackTimer += Time.deltaTime;
    }
    void Idle()
    {
        Vector3 dir = (player.position - transform.position).normalized;
        transform.position += dir * (moveSpeed * 0.5f) * Time.deltaTime;
    }

    void Chase()
    {
        Vector3 dir = (player.position - transform.position).normalized;
        transform.position += dir * moveSpeed * Time.deltaTime;
    }

    void Attack()
    {
        if (attackTimer >= attackRate)
        {
            IDamage dmg = player.GetComponent<IDamage>();
            if (dmg != null)
            {
                dmg.TakeDamage(attackDamage);
            }
            attackTimer = 0f;
        }
    }

    public void Alert(Vector3 pos)
    {
        if (currentState == State.Dead)
        {
            return;
        }
        currentState = State.Chase;
    }
    //==========================================================================================
    // Function, TakeDamage
    //==========================================================================================
    public void TakeDamage(int amount)
    {
        HP -= amount;
        if (HP <= 0)
        {
            Die();

        }
        else
        {
            StartCoroutine(flashRed());
        }
    }
        
      //==========================================================================================
     // Function, Die
      //==========================================================================================
            void Die()
           {
                currentState = State.Dead;

                if (spawner != null)
                {
                    spawner.MobDied();
                }

                if (dissolver != null)
                {
                    dissolver.StartCoroutine(dissolver.dissolve());
                }
                else
                {
                    Destroy(gameObject);
                }

            }


    IEnumerator flashRed()
    {

            if (model != null)
            {
            model.material.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            model.material.color = colorOrig;
               
            }
       

    }

    public void onDeath(bool dead)
    {
        if (!dead)
        {
            Die();
        }
    }
    //==========================================================================================
}
//==============================================================================================
// End of Enemy AI .cs
//==============================================================================================