using UnityEngine;
using System.Collections.Generic;
public class gameManagerAI : MonoBehaviour
{
    public static gameManagerAI instance;

    [SerializeField] Transform player;
    List<enemyAI> enemies = new List<enemyAI>();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        instance = this;
    }

    public Transform GetPlayer()
    {
        return player;
    }
    public void RegisterEnemy(enemyAI enemy)
    {
        if (!enemies.Contains(enemy))
        {
            enemies.Add(enemy);
        }
    }
    public void UnregisterEnemy(enemyAI enemy)
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
    
