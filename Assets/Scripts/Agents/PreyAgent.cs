using UnityEngine;

public sealed class PreyAgent : MonoBehaviour
{
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float maxAcceleration = 9f;
    [SerializeField] private float wanderStrength = 3f;
    [SerializeField] private float wanderTurnRate = 0.8f;
    [SerializeField] private float wanderTargetInterval = 1.5f;
    [SerializeField] private float boundsStrength = 12f;
    [SerializeField] private float turnResponsiveness = 10f;

    private SimulationBounds bounds;
    private Vector3 velocity;
    private Vector3 wanderDirection;
    private Vector3 wanderTargetDirection;
    private float nextWanderTargetTime;

    private void OnValidate()
    {
        maxSpeed = Mathf.Max(0.1f, maxSpeed);
        maxAcceleration = Mathf.Max(0.1f, maxAcceleration);
        wanderStrength = Mathf.Max(0f, wanderStrength);
        wanderTurnRate = Mathf.Max(0.05f, wanderTurnRate);
        wanderTargetInterval = Mathf.Max(0.1f, wanderTargetInterval);
        boundsStrength = Mathf.Max(0f, boundsStrength);
        turnResponsiveness = Mathf.Max(0.1f, turnResponsiveness);
    }

    public void Initialize(SimulationBounds simulationBounds)
    {
        bounds = simulationBounds;

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
        if (bounds != null)
        {
            desiredDirection += bounds.GetCenteringDirection(transform.position) * boundsStrength;
        }

        Vector3 desiredVelocity = desiredDirection.normalized * maxSpeed;
        Vector3 steering = desiredVelocity - velocity;
        return Vector3.ClampMagnitude(steering, maxAcceleration);
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
}
