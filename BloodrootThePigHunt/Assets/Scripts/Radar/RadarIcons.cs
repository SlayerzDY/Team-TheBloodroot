using UnityEngine;
public class RadarIcons : MonoBehaviour
{

    // Attach this to the sprite icons of 2D Sprites in Unity
    // or any 2D sprite you want to show up on the map when the radar pans over it
    // make susre it has a sprite render that can be put in the serialize fields

    [SerializeField] private SpriteRenderer spRender;

    // lower = slower fade time from map
    [Range(.1f, .5f)]public float fadeSpeed = .2f;

    // sets the opacity of the icon
    private float currAlpha = 0f;

    void Start()
    {
        if (spRender != null)
        {

            Color change = spRender.color;
            change.a = 0f;
            spRender.color = change;

        }
    }

    void Update()
    {
        if (spRender != null)
        {
            // get the alpha for fading and set that alpha to the sprite
            currAlpha = Mathf.Max(0f, currAlpha - fadeSpeed * Time.deltaTime);
            Color color = spRender.color;
            color.a = currAlpha;
            spRender.color = color;
        }
    }

    public void Ping()
    {
        if (currAlpha <= 0f) { currAlpha = 1f; }
    }
}
