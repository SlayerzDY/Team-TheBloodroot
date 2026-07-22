//==============================================================================================
// Using Unity Engine
//==============================================================================================
using UnityEngine;
using System.Collections;
//==============================================================================================
// Declare Laser
//==============================================================================================
public class laser : MonoBehaviour
{
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    [SerializeField] LineRenderer laserLine;
    [SerializeField] GameObject hitEffect;
    [SerializeField] Transform laserStartPos;
    [SerializeField] int damageAmount;
    [SerializeField] float damageRate;
    [SerializeField] int laserDistMax;
    bool isDamaging;
    //==========================================================================================
    // Declare Public Functions
    //==========================================================================================
    // Function, Update
    //------------------------------------------------------------------------------------------
    void Update()
    {
        createLaser();
    }
    //==========================================================================================
    // Function, Create Laser
    //------------------------------------------------------------------------------------------
    void createLaser()
    {
        RaycastHit hit;
        if (Physics.Raycast(laserStartPos.position, laserStartPos.forward, out hit, laserDistMax))
        {
            laserLine.SetPosition(0, laserStartPos.position);
            laserLine.SetPosition(1, hit.point);
            hitEffect.SetActive(true);
            hitEffect.transform.position = hit.point;
            IDamage dmg = hit.collider.GetComponent<IDamage>();
            if (dmg != null && !isDamaging) { StartCoroutine(damageTime(dmg)); }
        }
        else
        {
            laserLine.SetPosition(0, laserStartPos.position);
            laserLine.SetPosition(1, laserStartPos.position + laserStartPos.forward * laserDistMax);
            hitEffect.SetActive(false);
        }
    }
    //==========================================================================================
    // Function, Damage Time
    //------------------------------------------------------------------------------------------
    IEnumerator damageTime(IDamage d)
    {
        isDamaging = true;
        d.TakeDamage(damageAmount);
        yield return new WaitForSeconds(damageRate);
        isDamaging = false;
    }
    //==========================================================================================
}
//==============================================================================================
// End of Laser CS
//==============================================================================================