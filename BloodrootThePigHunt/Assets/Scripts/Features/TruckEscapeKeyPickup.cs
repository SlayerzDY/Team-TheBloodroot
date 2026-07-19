using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class TruckEscapeKeyPickup : MonoBehaviour
{
    [SerializeField] private TruckEscapeEnding truckEnding;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool hideAfterPickup = true;

    private bool pickedUp;

    private void Reset()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1.8f, 1f, 1f);

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Awake()
    {
        if (truckEnding == null)
        {
            truckEnding = FindAnyObjectByType<TruckEscapeEnding>();
        }

        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (pickedUp ||
            !IsPlayer(other))
        {
            return;
        }

        if (truckEnding == null)
        {
            truckEnding = FindAnyObjectByType<TruckEscapeEnding>();
        }

        if (truckEnding == null)
        {
            Debug.LogError(
                "TruckEscapeKeyPickup could not find TruckEscapeEnding.");
            return;
        }

        pickedUp = true;
        truckEnding.CollectTruckKey();

        if (hideAfterPickup)
        {
            gameObject.SetActive(false);
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;

        Transform root = other.transform.root;

        return root != null &&
            root.CompareTag(playerTag);
    }
}
