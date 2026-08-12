using UnityEngine;

public class ShotgunBullet : MonoBehaviour
{
    [Header("Pellet Settings")]
    public GameObject pelletPrefab;
    public int pelletCount = 8;
    public float spread = 5f;
    public bool isPlayerBullet = true;
    void Start()
    {
        spawnBulletsSpread();
    }
    private void spawnBulletsSpread() {
        // Spawn the additional pellets around the bullet's forward vector
        for (int i = 0; i < pelletCount; i++)
        {
            // Generate uniform random spread within a circular cone
            Vector2 randomCircle = Random.insideUnitCircle * spread;
            Quaternion spreadRotate = Quaternion.Euler(randomCircle.x, randomCircle.y, 0);

            // Apply rotation relative to this bullet's initial forward direction
            Vector3 newDir = transform.rotation * spreadRotate * Vector3.forward;

            GameObject pellet = Instantiate(
                pelletPrefab,
                transform.position,
                Quaternion.LookRotation(newDir)
            );

            Damage pelletDamage = pellet.GetComponent<Damage>();
            if (pelletDamage != null)
            {
                pelletDamage.SetPlayerBullet(isPlayerBullet);
            }
        }
    }
}
