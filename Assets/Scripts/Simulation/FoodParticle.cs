using UnityEngine;

public sealed class FoodParticle : MonoBehaviour
{
    [SerializeField] private float energyValue = 35f;

    private FoodSpawner spawner;
    private bool consumed;

    public float EnergyValue => energyValue;
    public bool IsAvailable => !consumed && isActiveAndEnabled;

    private void OnValidate()
    {
        energyValue = Mathf.Max(0f, energyValue);
    }

    public void Initialize(FoodSpawner owner)
    {
        spawner = owner;
        consumed = false;
    }

    public float Consume()
    {
        if (consumed)
        {
            return 0f;
        }

        consumed = true;
        spawner?.UnregisterFood(this);
        Destroy(gameObject);
        return energyValue;
    }
}
