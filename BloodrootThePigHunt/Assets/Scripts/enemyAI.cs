using UnityEngine;
using System.Collections;

public class enemyAI : MonoBehaviour, IDamage
{
    [SerializeField] Renderer model;

    [SerializeField] int HP;

    Color colorOriginal;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
        colorOriginal = model.material.color;

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void takeDamage(int amount)
    {

        HP -= amount;

        if(HP <= 0)
        {

            Destroy(gameObject);

        }
        else
        {

            StartCoroutine(flashRed());

        }

    }


    IEnumerator flashRed()
    {

        model.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        model.material.color = colorOriginal;

    }
}
