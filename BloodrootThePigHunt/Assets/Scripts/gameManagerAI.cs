using UnityEngine;
using System.Collections.Generic;
using NUnit.Framework;
using System.Data;
public class gameManagerAI : MonoBehaviour
{
    public static gameManagerAI instance;

    [SerializeField] Transform player;
    List<screecherPigAI> enemies = new List<screecherPigAI>();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        instance = this;
    }

    public Transform GetPlayer()
    {
        return player;
    }
    public void RegisterEnemy(screecherPigAI enemy)
    {
        if (!enemies.Contains(enemy))
        {
            enemies.Add(enemy);
        }
    }
    public void UnregisterEnemy(screecherPigAI enemy)
    {
        if (enemies.Contains(enemy))
        {
            enemies.Remove(enemy);
        }
    }
    public int GetEnemyCount()
    {
        return enemies.Count;

    }
}
    
