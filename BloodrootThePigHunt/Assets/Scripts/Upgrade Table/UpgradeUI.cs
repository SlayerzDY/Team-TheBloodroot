using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class UpgradeUI : MonoBehaviour
{
    // serialized fieelds
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text seleectText;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private TMP_Text costTeext;
    [SerializeField] private TMP_Text notificationText;
    [SerializeField] private Button purchase;
    [SerializeField] private Button listButtonPrefab;
    [SerializeField] private Transform listContainer;

    // private variables
    private UpgradeScriptable activeScriptable;
    private UpgradeTable activeTable;

    private void Start()
    {
        //if (panel != null) { panel.SetActive(false); }
    }

    public void OpenPanelMulti(List<UpgradeScriptable> available, UpgradeTable table)
    {

        activeTable = table;
        gameManager.instance.OpenUpgradeMenu();
        // should remove old buttons in list
        foreach(Transform child in listContainer)
        {

            Destroy(child.gameObject);

        }
        for (int i = 0; i < available.Count; i++) {
        
            UpgradeScriptable data = available[i];
            Button newBtn = Instantiate(listButtonPrefab, listContainer);
            newBtn.GetComponentInChildren<TMP_Text>().text = data.upgradeNamee;
            newBtn.onClick.AddListener(() => DisplayerUpgradeDetails(data));
        
        }
        ResetDetails();

    }

    private void OnPurchasseClicked() { 

        if(activeTable != null && activeScriptable != null)
        {
            activeTable.UpgradeRunTimeScriptableData(activeScriptable);
            activeTable.TryPurchaingUpgradee();
            DisplayerUpgradeDetails(activeScriptable);
        }

    }

    void ResetDetails()
    {
        seleectText.text = "Select a Upgrade";
        detailsText.text = "";
        costTeext.text = "";
        notificationText.text = "";

        if(purchase!= null) {purchase.gameObject.SetActive(false);}

    }

    private void DisplayerUpgradeDetails(UpgradeScriptable data) {

        playerController player = FindAnyObjectByType<playerController>();

        activeScriptable = data;
        seleectText.text = $"Selected Upgrade: {data.upgradeNamee}";
        detailsText.text = $"{data.statToUpgrade} is being Upgraded (+{data.upgradeValue}x)";
        costTeext.text = $"Need {data.requirdAmount} {data.requiredItem.GetComponent<Item>().item.itemName}'s"; ;   
        
        if(purchase != null) { purchase.gameObject.SetActive(true); }
        purchase.onClick.RemoveAllListeners();
        purchase.onClick.AddListener(OnPurchasseClicked);
    
    }

    public void ClosePaneel() {

        if (gameManager.instance != null)
        {
            if (MenuTracker.Instance != null) { MenuTracker.Instance.Clear(); }   
            gameManager.instance.stateUnpause();
        }

    }

    public void ShowNotification(string message, bool isSuccess)
    {

        if (notificationText == null) { return; }
        notificationText.text = message;
        // change colorss depending on background
        if (isSuccess) { notificationText.color = Color.green;  }
        else {notificationText.color = Color.red; }

    }

}
