using UnityEngine;

public class RadarRotata : MonoBehaviour
{

    // This is basically just the rotate script but instead of vector 3 up I used 0,0
    public float rotSpeed;

    // Update is called once per frame
    void Update()
    {
        // - so it goes clockwise instead of counter clockwise on the Zaxi
        transform.Rotate(0, 0, -rotSpeed * Time.deltaTime);

    }
}
