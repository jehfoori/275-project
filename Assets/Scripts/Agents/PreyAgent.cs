using UnityEngine;

public sealed class PreyAgent : MonoBehaviour
{
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float maxAcceleration = 9f;
    [SerializeField] private float perceptionRadius = 5f;
    [SerializeField] private float separationRadius = 1.5f;
    [SerializeField] private float separationWeight = 2.4f;
    [SerializeField] private float alignmentWeight = 1.1f;
    [SerializeField] private float cohesionWeight = 0.9f;
    [SerializeField] private float wanderStrength = 1.2f;
    [SerializeField] private float wanderTurnRate = 0.8f;
    [SerializeField] private float wanderTargetInterval = 1.5f;
    [SerializeField] private float boundsStrength = 12f;
    [SerializeField] private float turnResponsiveness = 10f;

    private SimulationBounds bounds;
    private SimulationManager simulationManager;
    private Vector3 velocity;
    private Vector3 wanderDirection;
    private Vector3 wanderTargetDirection;
    private float nextWanderTargetTime;

    public Vector3 Velocity => velocity;

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
        turnResponsiveness = Mathf.Max(0.1f, turnResponsiveness);
    }

    public void Initialize(SimulationBounds simulationBounds, SimulationManager manager)
    {
        bounds = simulationBounds;
        simulationManager = manager;

        if (velocity.sqrMagnitude < 0.001f)
        {
            velocity = Random.onUnitSphere * maxSpeed * 0.5f;
            wanderDirection = velocity.normalized;
            wanderTargetDirection = wanderDirection;
        }
    }

    private void Awake()
    {
        wanderDirection = Random.onUnitSphere;
        wanderTargetDirection = wanderDirection;
        velocity = wanderDirection * maxSpeed * 0.5f;
        nextWanderTargetTime = Time.time + Random.Range(0f, wanderTargetInterval);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        Vector3 acceleration = CalculateSteering();

        velocity += acceleration * deltaTime;
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);

        transform.position += velocity * deltaTime;

        if (bounds != null && !bounds.Contains(transform.position))
        {
            transform.position = bounds.ClosestPointInside(transform.position);
            velocity = Vector3.ProjectOnPlane(velocity, (transform.position - bounds.Center).normalized);
        }

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

        Vector3 desiredDirection = wanderDirection * wanderStrength;

        AddFlockingSteering(ref desiredDirection);

        if (bounds != null)
        {
            desiredDirection += bounds.GetCenteringDirection(transform.position) * boundsStrength;
        }

        Vector3 desiredVelocity = desiredDirection.normalized * maxSpeed;
        Vector3 steering = desiredVelocity - velocity;
        return Vector3.ClampMagnitude(steering, maxAcceleration);
    }

    private void AddFlockingSteering(ref Vector3 desiredDirection)
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

        desiredDirection += alignment * alignmentWeight;
        desiredDirection += cohesion * cohesionWeight;

        if (separationCount > 0)
        {
            desiredDirection += (separation / separationCount).normalized * separationWeight;
        }
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
    }
}
