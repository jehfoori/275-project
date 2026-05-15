using System.Collections.Generic;
using UnityEngine;

public sealed class SimulationManager : MonoBehaviour
{
    [SerializeField] private SimulationBounds bounds;
    [SerializeField] private PreyAgent preyPrefab;
    [SerializeField] private FoodSpawner foodSpawner;
    [SerializeField] private Transform agentParent;
    [SerializeField] private int startingPreyCount = 30;
    [SerializeField] private bool spawnOnStart = true;

    private readonly List<PreyAgent> preyAgents = new List<PreyAgent>();

    public IReadOnlyList<PreyAgent> PreyAgents => preyAgents;

    private void OnValidate()
    {
        startingPreyCount = Mathf.Max(0, startingPreyCount);
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnInitialPrey();
        }
    }

    [ContextMenu("Spawn Initial Prey")]
    private void SpawnInitialPrey()
    {
        if (bounds == null || preyPrefab == null)
        {
            Debug.LogWarning("SimulationManager needs bounds and a prey prefab before it can spawn agents.", this);
            return;
        }

        Transform parent = agentParent != null ? agentParent : transform;

        for (int i = 0; i < startingPreyCount; i++)
        {
            PreyAgent prey = Instantiate(preyPrefab, bounds.RandomPointInside(), Random.rotation, parent);
            RegisterPrey(prey);
            prey.Initialize(bounds, this, foodSpawner);
        }
    }

    public void RegisterPrey(PreyAgent prey)
    {
        if (prey != null && !preyAgents.Contains(prey))
        {
            preyAgents.Add(prey);
        }
    }

    public void UnregisterPrey(PreyAgent prey)
    {
        preyAgents.Remove(prey);
    }
}
