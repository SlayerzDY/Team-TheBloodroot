using System.Collections;
using UnityEngine;

public class RootAttack : MonoBehaviour
{
    [Header("Root Settings")]
    [SerializeField] GameObject rootPrefab;
    [SerializeField] float transformDuration;
    [SerializeField] int rootCount = 8;
    [SerializeField] float rootSpread = 5f;
    [SerializeField] bool isPlayerBullet = true;
    [SerializeField] int despawnTime;

    public bool shouldSpawn = true;

    void Start()
    {
        StartCoroutine(spawnChangeForm());
        StartCoroutine(despawn());
        if (!shouldSpawn) { return; }
        rootAttack();
    }

    private void rootAttack()
    {
        for (int i = 0; i < rootCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * rootSpread;
            Vector3 spawnPos = transform.position + new Vector3(randomOffset.x, -1f, randomOffset.y);

            Vector3 baseEuler = rootPrefab.transform.eulerAngles;
            Quaternion spawnRot = Quaternion.Euler(baseEuler.x, Random.Range(0f, 360f), baseEuler.z);

            GameObject pellet = Instantiate(this.gameObject, spawnPos, spawnRot);

            // Force a clean, known-good scale — don't trust whatever the live source's
            // scale happened to be at the instant of Instantiate
            pellet.transform.localScale = Vector3.one;

            RootAttack pelletRoot = pellet.GetComponent<RootAttack>();
            if (pelletRoot != null)
            {
                pelletRoot.shouldSpawn = false;
                pelletRoot.transformDuration = transformDuration;
            }

            Damage pelletDamage = pellet.GetComponent<Damage>();
            if (pelletDamage != null) pelletDamage.SetPlayerBullet(isPlayerBullet);
        }
    }

    private IEnumerator spawnChangeForm()
    {
        Vector3 targetScale = transform.localScale;
        Debug.Log($"[{gameObject.name}] targetScale={targetScale}, duration={transformDuration}");
        transform.localScale = new Vector3(targetScale.x, targetScale.y, 0f);
        float elapsed = 0f;
        while (elapsed < transformDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transformDuration);
            float z = Mathf.Lerp(0f, targetScale.z, t);
            transform.localScale = new Vector3(targetScale.x, targetScale.y, z);
            Debug.Log($"[{gameObject.name}] IsScaling z={z}, elapsed={elapsed}");
            yield return null;
        }
        transform.localScale = targetScale;
        Debug.Log($"[{gameObject.name}] DONE final={transform.localScale}");
    }
    private IEnumerator despawn() {
        if (despawnTime <= 0f) { yield return null; }
        yield return new WaitForSeconds(despawnTime);
        if (this.gameObject.GetComponent<Dissolver>() != null) { 
            this.gameObject.GetComponent<Dissolver>().StartCoroutine(this.gameObject.GetComponent<Dissolver>().dissolve()); 
        } else { 
            Destroy(gameObject); 
        }
    }
}