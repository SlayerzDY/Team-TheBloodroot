using UnityEngine;

public class waveTrigger : MonoBehaviour
{
    [SerializeField] waveManager manager;
    [SerializeField] bool startOnPlayerTrigger;

    private bool hasTriggered;

    private void Awake()
    {
        if (manager == null)
        {
            manager = FindAnyObjectByType<waveManager>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!startOnPlayerTrigger || hasTriggered)
            return;

        if (!other.CompareTag("Player"))
            return;

        if (manager == null)
        {

            return;
        }

        hasTriggered = true;
        manager.BeginEncounter();
    }
}
