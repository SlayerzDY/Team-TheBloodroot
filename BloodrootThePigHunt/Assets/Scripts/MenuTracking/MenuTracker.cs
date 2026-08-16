using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MenuTracker : MonoBehaviour
{
    //Singleton
    public static MenuTracker Instance { get; private set; }

    //Private Variables
    private List<GameObject> previousMenus = new List<GameObject>();

    //==========================================================================================
    // Function, Awake
    //==========================================================================================
    private void Awake()
    {
        // If another instance of MenuTracker exists
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Delete copy
            return;
        }

        // Initialize new instance
        Instance = this;
    }
    //==========================================================================================
    // Function, Start
    //==========================================================================================
    void Start()
    {

    }

    //==========================================================================================
    // Function, Update
    //==========================================================================================
    void Update()
    {
        
    }

    //==========================================================================================
    // Function, Add Menu
    //==========================================================================================
    //Add current menu before going 1 step deeper
    public void AddMenu(GameObject menu)
    {
        previousMenus.Add(menu);
        Debug.Log(menu.ToString() + " has been added to queue");
    }
    //==========================================================================================
    // Function, Remove Last Menu 
    //==========================================================================================
    private void RemoveLast()
    {
        if (previousMenus.Count != 0)
        {
            int size = previousMenus.Count;
            previousMenus.RemoveAt(size - 1);
        }
        else
        {
            Debug.Log("There are no previous menus to remove");
        }
    }
    //==========================================================================================
    // Function, Clear Queue
    //==========================================================================================
    public void Clear()
    {
        short size = (short)previousMenus.Count;
        short removed = 0;
        while (previousMenus.Count > 0)
        {
            Instance.RemoveLast();
            removed++;
        }
        Debug.Log(removed + "/" +  size + " removed from queue");
    }
    //==========================================================================================
    // Function, Previous Menu
    //==========================================================================================
    public GameObject PreviousMenu()
    {
        if (previousMenus.Count != 0)
        {
            GameObject last = previousMenus[previousMenus.Count - 1];
            Instance.RemoveLast();
            return last;

        }
        else {
            Debug.Log("No previous menu found in queue");
            return null; 
        }
    }
}
