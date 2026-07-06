using UnityEngine;

public class StarsFollow : MonoBehaviour
{
    public Transform Camera;

    private void LateUpdate()
    {
        
        if(Camera != null)
        {

            transform.position = Camera.position;

        }

    }

}
