using UnityEngine;

public sealed class SimulationManager : MonoBehaviour
{
    [SerializeField] private SimulationBounds bounds;
    [SerializeField] private PreyAgent preyPrefab;
    [SerializeField] private Transform agentParent;
    [SerializeField] private int startingPreyCount = 30;
    [SerializeField] private bool spawnOnStart = true;

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
            prey.Initialize(bounds);
        }
    }
}
