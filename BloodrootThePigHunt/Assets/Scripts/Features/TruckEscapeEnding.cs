using TMPro;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class TruckEscapeEnding : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private waveManager manager;
    [SerializeField] private string playerTag = "Player";

    [Header("Optional UI")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField, TextArea] private string lockedMessage =
        "Survive the final wave before escaping.";
    [SerializeField, TextArea] private string readyMessage =
        "The woods are quiet... get back to the truck.";
    [SerializeField, TextArea] private string escapeMessage =
        "You made it back to the truck.";
    [SerializeField] private bool hideMessageUntilReady = true;

    private bool escapeReady;
    private bool gameEnded;

    private void Reset()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.5f, 0f);
        trigger.size = new Vector3(7f, 3f, 6f);

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Awake()
    {
        if (manager == null)
        {
            manager = FindAnyObjectByType<waveManager>();
        }

        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnEnable()
    {
        if (manager == null)
        {
            manager = FindAnyObjectByType<waveManager>();
        }

        if (manager != null)
        {
            manager.AllWavesCompleted += UnlockEscape;

            if (manager.FinalWaveCleared)
            {
                UnlockEscape();
            }
        }

        RefreshMessage();
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.AllWavesCompleted -= UnlockEscape;
        }
    }

    private void Start()
    {
        if (manager != null &&
            manager.FinalWaveCleared)
        {
            UnlockEscape();
            return;
        }

        RefreshMessage();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (gameEnded ||
            !IsPlayer(other))
        {
            return;
        }

        if (!escapeReady)
        {
            ShowMessage(lockedMessage);
            return;
        }

        EndGameAtTruck();
    }

    private void UnlockEscape()
    {
        escapeReady = true;
        ShowMessage(readyMessage);
    }

    private void EndGameAtTruck()
    {
        gameEnded = true;
        ShowMessage(escapeMessage);

        if (gameManager.instance != null)
        {
            gameManager.instance.youWin();
        }
        else
        {
            Debug.LogError(
                "TruckEscapeEnding could not find GameManager to end the game.");
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

    private void RefreshMessage()
    {
        if (messageText == null)
            return;

        if (escapeReady)
        {
            ShowMessage(readyMessage);
            return;
        }

        if (hideMessageUntilReady)
        {
            messageText.gameObject.SetActive(false);
            return;
        }

        ShowMessage(lockedMessage);
    }

    private void ShowMessage(string message)
    {
        if (messageText == null)
            return;

        messageText.gameObject.SetActive(true);
        messageText.text = message;
    }
}
