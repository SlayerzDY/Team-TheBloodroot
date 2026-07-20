using UnityEngine;

public class RadarRotata : MonoBehaviour
{

    // The way I used this script and can be used in the future is that I created a ui image and removed the image componet 
    // anchored it to the middle of where I wanted it to act as a safe zone then created a UiImageCild to the Parent to create the line and basically
    // this script rotates the safe zone not the line and creates a ray cast from the player position so that it looks like when the image crosses over the enemy it pings them on the map for a specific ammount of time
    // it also locks it to the player rotation so it stays with the line on the radar
    // p.s this took so long because I couldn't figure out why the map was pinging 180 degrees ahead without the raycast tellling me they were offset and that it was also going opposite direction from my radar line

    public float rotSpeed;

    // currently somwhere between 22-25 is the minimap disstance
    [Range(25f, 100f)]public float radarRadius = 25f;

    // this sets the layers in which the ray cast can hit for example, if you give bullets an Icon and then set it to be able to detect them the should show up on the radar if they get pinged should being the optimal term here
    public LayerMask Enemy;

    private Transform playerTransform;
    private float currentLocalAngle = 0f;

    // Update is called once per frame
    void Update()
    {
        // if you see if statements like this I am just trying to reogrinize my style for if there is just one thing inside like this then the if statment should just be one line
        if(playerTransform == null) { playerTransform = gameManager.instance.player.transform; }

        currentLocalAngle = (currentLocalAngle - rotSpeed * Time.deltaTime) % 360f;

        if (currentLocalAngle < 0) { currentLocalAngle += 360f;}
        
        float angleRad = currentLocalAngle * Mathf.Deg2Rad;

        Vector3 localRayDir = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
        Vector3 rayDirection = playerTransform.TransformDirection(localRayDir);
        RaycastHit hit;

        transform.localRotation = Quaternion.Euler(0f, 0f, currentLocalAngle - 90f);

        //Debug.DrawRay(playerTransform.position, rayDirection * radarRadius, Color.cyan);

        if (Physics.Raycast(playerTransform.position, rayDirection, out hit, radarRadius, Enemy))
        { 
            RadarIcons pigIcon = hit.collider.GetComponentInChildren<RadarIcons>();
            if (pigIcon != null){ pigIcon.Ping();}
        }
    }  
}
