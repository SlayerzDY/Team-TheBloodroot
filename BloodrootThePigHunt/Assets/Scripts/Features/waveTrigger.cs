using UnityEngine;

public class waveTrigger : MonoBehaviour
{

    [SerializeField] waveManager manager;

    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered)
            return;

        if (!other.CompareTag("Player"))
            return;

        if (manager == null)
        {
            Debug.LogWarning("Wave Manager has not been assigned.");
            return;
        }

        hasTriggered = true;
        manager.BeginEncounter();

        GetComponent<Collider>().enabled = false;

    }
}
