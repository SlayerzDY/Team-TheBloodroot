using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Expedition : MonoBehaviour
{
    public List<GameObject> scenes = new List<GameObject>();


    // Start is called once before the first execution of Update after the MonoBehaviour is created
void Start()
    {
        GameObject.FindGameObjectsWithTag("Expedition", scenes);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //==========================================================================================
    // Function, Expedition
    //==========================================================================================
    public bool ExpeditionStart(int scene)
    {

        return false;
    }
}
