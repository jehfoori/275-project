using System.Collections.Generic;
using UnityEngine;

public sealed class FoodSpawner : MonoBehaviour
{
    [SerializeField] private bool spawnFood;
    [SerializeField] private SimulationBounds bounds;
    [SerializeField] private FoodParticle foodPrefab;
    [SerializeField] private Transform foodParent;
    [SerializeField] private int initialFoodCount = 18;
    [SerializeField] private int maxFoodCount = 35;
    [SerializeField] private float spawnInterval = 1.4f;

    private readonly List<FoodParticle> activeFood = new List<FoodParticle>();
    private float nextSpawnTime;

    public IReadOnlyList<FoodParticle> ActiveFood => activeFood;

    private void OnValidate()
    {
        initialFoodCount = Mathf.Max(0, initialFoodCount);
        maxFoodCount = Mathf.Max(0, maxFoodCount);
        spawnInterval = Mathf.Max(0.1f, spawnInterval);
    }

    private void Start()
    {
        if (!spawnFood)
        {
            return;
        }

        for (int i = 0; i < initialFoodCount && activeFood.Count < maxFoodCount; i++)
        {
            SpawnFood();
        }

        nextSpawnTime = Time.time + spawnInterval;
    }

    private void Update()
    {
        if (!spawnFood)
        {
            return;
        }

        if (Time.time < nextSpawnTime)
        {
            return;
        }

        nextSpawnTime = Time.time + spawnInterval;

        if (activeFood.Count < maxFoodCount)
        {
            SpawnFood();
        }
    }

    public void UnregisterFood(FoodParticle food)
    {
        activeFood.Remove(food);
    }

    private void SpawnFood()
    {
        if (bounds == null || foodPrefab == null)
        {
            Debug.LogWarning("FoodSpawner needs bounds and a food prefab before it can spawn food.", this);
            return;
        }

        Transform parent = foodParent != null ? foodParent : transform;
        FoodParticle food = Instantiate(foodPrefab, bounds.RandomGroundPointInside(), Quaternion.identity, parent);
        food.Initialize(this);
        activeFood.Add(food);
    }
}
