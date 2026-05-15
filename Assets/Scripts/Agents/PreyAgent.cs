using UnityEngine;

public sealed class PreyAgent : MonoBehaviour
{
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float maxAcceleration = 9f;
    [SerializeField] private float perceptionRadius = 5f;
    [SerializeField] private float separationRadius = 1.5f;
    [SerializeField] private float separationWeight = 3.2f;
    [SerializeField] private float alignmentWeight = 1.1f;
    [SerializeField] private float cohesionWeight = 0.9f;
    [SerializeField] private float wanderStrength = 1.2f;
    [SerializeField] private float wanderTurnRate = 0.8f;
    [SerializeField] private float wanderTargetInterval = 1.5f;
    [SerializeField] private float boundsStrength = 12f;
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float energyDrainRate = 2.6f;
    [SerializeField] private float hungerThreshold = 55f;
    [SerializeField] private float foodPerceptionRadius = 12f;
    [SerializeField] private float foodSeekWeight = 12f;
    [SerializeField] private float eatRadius = 0.6f;
    [SerializeField] private float turnResponsiveness = 10f;

    private SimulationBounds bounds;
    private SimulationManager simulationManager;
    private FoodSpawner foodSpawner;
    private Vector3 velocity;
    private Vector3 wanderDirection;
    private Vector3 wanderTargetDirection;
    private FoodParticle targetFood;
    private float nextWanderTargetTime;
    private float energy;
    private float energyDrainMultiplier = 1f;

    public Vector3 Velocity => velocity;
    public float Energy => energy;
    public bool IsHungry => energy <= hungerThreshold;

    private void OnValidate()
    {
        maxSpeed = Mathf.Max(0.1f, maxSpeed);
        maxAcceleration = Mathf.Max(0.1f, maxAcceleration);
        perceptionRadius = Mathf.Max(0.1f, perceptionRadius);
        separationRadius = Mathf.Clamp(separationRadius, 0.1f, perceptionRadius);
        separationWeight = Mathf.Max(0f, separationWeight);
        alignmentWeight = Mathf.Max(0f, alignmentWeight);
        cohesionWeight = Mathf.Max(0f, cohesionWeight);
        wanderStrength = Mathf.Max(0f, wanderStrength);
        wanderTurnRate = Mathf.Max(0.05f, wanderTurnRate);
        wanderTargetInterval = Mathf.Max(0.1f, wanderTargetInterval);
        boundsStrength = Mathf.Max(0f, boundsStrength);
        maxEnergy = Mathf.Max(1f, maxEnergy);
        energyDrainRate = Mathf.Max(0f, energyDrainRate);
        hungerThreshold = Mathf.Clamp(hungerThreshold, 0f, maxEnergy);
        foodPerceptionRadius = Mathf.Max(0.1f, foodPerceptionRadius);
        foodSeekWeight = Mathf.Max(0f, foodSeekWeight);
        eatRadius = Mathf.Max(0.05f, eatRadius);
        turnResponsiveness = Mathf.Max(0.1f, turnResponsiveness);
    }

    public void Initialize(SimulationBounds simulationBounds, SimulationManager manager, FoodSpawner spawner)
    {
        bounds = simulationBounds;
        simulationManager = manager;
        foodSpawner = spawner;

        if (velocity.sqrMagnitude < 0.001f)
        {
            velocity = Random.onUnitSphere * maxSpeed * 0.5f;
            wanderDirection = velocity.normalized;
            wanderTargetDirection = wanderDirection;
        }
    }

    private void Awake()
    {
        energy = Random.Range(hungerThreshold, maxEnergy);
        energyDrainMultiplier = Random.Range(0.55f, 1.35f);
        wanderDirection = Random.onUnitSphere;
        wanderTargetDirection = wanderDirection;
        velocity = wanderDirection * maxSpeed * 0.5f;
        nextWanderTargetTime = Time.time + Random.Range(0f, wanderTargetInterval);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        energy = Mathf.Max(0f, energy - energyDrainRate * energyDrainMultiplier * deltaTime);
        targetFood = FindNearestVisibleFood();

        Vector3 acceleration = CalculateSteering();

        velocity += acceleration * deltaTime;
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);

        transform.position += velocity * deltaTime;

        if (bounds != null && !bounds.Contains(transform.position))
        {
            transform.position = bounds.ClosestPointInside(transform.position);
            velocity = Vector3.ProjectOnPlane(velocity, (transform.position - bounds.Center).normalized);
        }

        TryConsumeFood();
        RotateTowardVelocity(deltaTime);
    }

    private void OnDestroy()
    {
        if (simulationManager != null)
        {
            simulationManager.UnregisterPrey(this);
        }
    }

    private Vector3 CalculateSteering()
    {
        if (Time.time >= nextWanderTargetTime)
        {
            wanderTargetDirection = Random.onUnitSphere;
            nextWanderTargetTime = Time.time + wanderTargetInterval;
        }

        wanderDirection = Vector3
            .Slerp(wanderDirection, wanderTargetDirection, wanderTurnRate * Time.deltaTime)
            .normalized;

        bool seekingFood = targetFood != null;
        float hungerPressure = hungerThreshold > 0f
            ? Mathf.Clamp01((hungerThreshold - energy) / hungerThreshold)
            : 0f;
        float wanderMultiplier = seekingFood ? 0.25f : 1f;
        float alignmentMultiplier = seekingFood ? 0.45f : 1f;
        float cohesionMultiplier = seekingFood ? 0.2f : 1f;

        Vector3 desiredDirection = wanderDirection * wanderStrength * wanderMultiplier;

        AddFlockingSteering(ref desiredDirection, alignmentMultiplier, cohesionMultiplier);
        AddFoodSeekingSteering(ref desiredDirection);

        if (bounds != null)
        {
            desiredDirection += bounds.GetCenteringDirection(transform.position) * boundsStrength;
        }

        float targetSpeed = seekingFood ? maxSpeed : maxSpeed * Mathf.Lerp(0.85f, 1f, hungerPressure);
        Vector3 desiredVelocity = desiredDirection.normalized * targetSpeed;
        Vector3 steering = desiredVelocity - velocity;
        return Vector3.ClampMagnitude(steering, maxAcceleration);
    }

    private void AddFlockingSteering(ref Vector3 desiredDirection, float alignmentMultiplier, float cohesionMultiplier)
    {
        if (simulationManager == null)
        {
            return;
        }

        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        int flockmateCount = 0;
        int separationCount = 0;
        float perceptionRadiusSqr = perceptionRadius * perceptionRadius;
        float separationRadiusSqr = separationRadius * separationRadius;

        foreach (PreyAgent other in simulationManager.PreyAgents)
        {
            if (other == null || other == this)
            {
                continue;
            }

            Vector3 offset = transform.position - other.transform.position;
            float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr > perceptionRadiusSqr || distanceSqr <= 0.0001f)
            {
                continue;
            }

            flockmateCount++;
            alignment += other.Velocity.normalized;
            cohesion += other.transform.position;

            if (distanceSqr < separationRadiusSqr)
            {
                separationCount++;
                separation += offset.normalized / Mathf.Max(0.01f, Mathf.Sqrt(distanceSqr));
            }
        }

        if (flockmateCount == 0)
        {
            return;
        }

        alignment = (alignment / flockmateCount).normalized;
        cohesion = ((cohesion / flockmateCount) - transform.position).normalized;

        desiredDirection += alignment * alignmentWeight * alignmentMultiplier;
        desiredDirection += cohesion * cohesionWeight * cohesionMultiplier;

        if (separationCount > 0)
        {
            desiredDirection += (separation / separationCount).normalized * separationWeight;
        }
    }

    private void AddFoodSeekingSteering(ref Vector3 desiredDirection)
    {
        if (targetFood == null)
        {
            return;
        }

        Vector3 toFood = targetFood.transform.position - transform.position;
        float hungerPressure = hungerThreshold > 0f
            ? Mathf.Clamp01((hungerThreshold - energy) / hungerThreshold)
            : 1f;
        desiredDirection += toFood.normalized * foodSeekWeight * Mathf.Lerp(0.75f, 1.25f, hungerPressure);
    }

    private FoodParticle FindNearestVisibleFood()
    {
        if (!IsHungry || foodSpawner == null)
        {
            return null;
        }

        FoodParticle nearestFood = null;
        float nearestDistanceSqr = foodPerceptionRadius * foodPerceptionRadius;

        foreach (FoodParticle food in foodSpawner.ActiveFood)
        {
            if (food == null || !food.IsAvailable)
            {
                continue;
            }

            float distanceSqr = (food.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr <= nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestFood = food;
            }
        }

        return nearestFood;
    }

    private void TryConsumeFood()
    {
        if (targetFood == null)
        {
            return;
        }

        float eatRadiusSqr = eatRadius * eatRadius;
        if ((targetFood.transform.position - transform.position).sqrMagnitude > eatRadiusSqr)
        {
            return;
        }

        energy = Mathf.Min(maxEnergy, energy + targetFood.Consume());
    }

    private void RotateTowardVelocity(float deltaTime)
    {
        if (velocity.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector3 forward = velocity.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        Quaternion targetRotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(90f, 0f, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnResponsiveness * deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, perceptionRadius);

        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, separationRadius);

        Gizmos.color = new Color(0.4f, 1f, 0.35f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, foodPerceptionRadius);
    }
}
