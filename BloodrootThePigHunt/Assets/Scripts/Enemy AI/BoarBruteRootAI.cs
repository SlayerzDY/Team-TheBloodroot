using UnityEngine;

public class BoarBruteRootAI : BoarBruteAI
{
    [SerializeField] GameObject RootTrap;
    [Range(1, 100)] [SerializeField] public int SpawnChance;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void StartCharge()
    {
        base.StartCharge();
        if (RootTrap == null) { return; }
        int rand = Random.Range(0, 100);
        if (rand < SpawnChance)
        {
            Instantiate(RootTrap, gameManager.instance.player.transform);
        }
    }
}
