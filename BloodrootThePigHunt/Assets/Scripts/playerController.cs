using UnityEngine;

public class playerController : MonoBehaviour
{
    [SerializeField] CharacterController controller;
    [SerializeField] LayerMask ignoreLayer;

    [SerializeField] int HP;
    [SerializeField] int speed;
    [SerializeField] int sprintMod;
    [SerializeField] int jumpSpeed;
    [SerializeField] int jumpMax;
    [SerializeField] int gravity;

    [SerializeField] int shootDamage;
    [SerializeField] int shootDist;
    [SerializeField] float shootRate;

    int jumpCount;
    int HPOrig;

    float shootTimer;

    Vector3 moveDir;
    Vector3 playerVel;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        HPOrig = HP; 
    }

    // Update is called once per frame
    void Update()
    {

        movement();

        sprint();

    }

    void movement()
    {

        if (controller.isGrounded)
        {

            playerVel.y = 0;

            jumpCount = 0;

        }

        // remove vertical for side scrolling
        moveDir = Input.GetAxis("Horizontal") * transform.right + Input.GetAxis("Vertical") * transform.forward;
        controller.Move(moveDir.normalized * speed * Time.deltaTime);

        jump();
        controller.Move(playerVel * Time.deltaTime);
        playerVel.y -= gravity * Time.deltaTime;

        shootTimer += Time.deltaTime;
        if(Input.GetButton("Fire1") && shootTimer > shootRate)
        {

            shoot();

        }

        Debug.DrawRay(Camera.main.transform.position, Camera.main.transform.forward * shootDist, Color.red);

    }

    void sprint()
    {

        if (Input.GetButtonDown("Sprint"))
        {

            speed *= sprintMod;

        } else if (Input.GetButtonUp("Sprint"))
        {

            speed /= sprintMod;

        }

    }

    void jump()
    {

        if (Input.GetButtonDown("Jump") && jumpCount < jumpMax)
        {

            playerVel.y = jumpSpeed;

            jumpCount++;

        }

    }

    void shoot()
    {

        shootTimer = 0;

        RaycastHit hit;

        if(Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, shootDist, ~ignoreLayer))
        {

            Debug.Log(hit.collider.name);

            IDamage dmg = hit.collider.GetComponent<IDamage>();

            if(dmg != null)
            {

                dmg.takeDamage(shootDamage);

            }

        }

    }

}
