using UnityEngine;

public class MiniMapFollow : MonoBehaviour
{
    private Transform Player;
    private gameManager manager;
    public float heightOffset = 25f;
    private bool started = false;

    void Start()
    {

        GameObject gm = GameObject.Find("gamemanager");

        if(gm != null)
        {

            manager = gm.GetComponent<gameManager>();


        }

    }

    void LateUpdate()
    {

        if (!started && manager != null && manager.player != null)
        {
            Player = manager.player.transform;

            transform.SetParent(Player);

            // set camera at whatever heigh offset is set
            transform.localPosition = new Vector3(0f, heightOffset, 0f);

            // set the camera straight down
            transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            started = true;
           
        }

    }
}
