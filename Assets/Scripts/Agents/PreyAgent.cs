using System.Collections.Generic;
using UnityEngine;

public sealed class PreyAgent : MonoBehaviour
{
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float maxAcceleration = 9f;
    [SerializeField] private float perceptionRadius = 5f;
    [SerializeField] private float separationRadius = 1.5f;
    [SerializeField] private float separationWeight = 3.2f;
    [SerializeField] private float alignmentWeight = 0.8f;
    [SerializeField] private float cohesionWeight = 0.35f;
    [SerializeField] private float wanderStrength = 0.35f;
    [SerializeField] private float wanderTurnRate = 0.8f;
    [SerializeField] private float wanderTargetInterval = 1.5f;
    [SerializeField] private float pathFollowWeight = 8f;
    [SerializeField] private float waypointArrivalRadius = 2.2f;
    [SerializeField] private Vector2 waypointPauseTime = new Vector2(0.35f, 1.8f);
    [SerializeField] private float obstacleClearance = 2.1f;
    [SerializeField] private float obstacleLookAheadDistance = 5.5f;
    [SerializeField] private float obstacleAvoidanceWeight = 9f;
    [SerializeField] private float boundsStrength = 12f;
    [SerializeField] private float turnResponsiveness = 10f;
    [SerializeField] private float predatorPerceptionRadius = 20f;
    [SerializeField] private float predatorFleeWeight = 18f;
    [SerializeField] private float panicSpeedMultiplier = 1.45f;
    [SerializeField] private float panicAccelerationMultiplier = 1.35f;
    [SerializeField] private float threatMemoryTime = 0.8f;

    private readonly List<int> route = new List<int>();
    private readonly List<PreyAgent> nearbyAgents = new List<PreyAgent>(32);
    private readonly List<PredatorAgent> nearbyPredators = new List<PredatorAgent>(8);
    private SimulationBounds bounds;
    private SimulationManager simulationManager;
    private CityNavigation navigation;
    private Vector3 velocity;
    private Vector3 wanderDirection;
    private Vector3 wanderTargetDirection;
    private float nextWanderTargetTime;
    private float pauseUntilTime;
    private float lastThreatTime = -999f;
    private Vector3 lastThreatPosition;
    private int routeIndex;
    private int currentNodeIndex = -1;
    private bool isDead;

    public Vector3 Velocity => velocity;
    public IReadOnlyList<int> DebugRoute => route;
    public int DebugRouteIndex => routeIndex;
    public bool IsPaused => Time.time < pauseUntilTime;
    public Vector3 DebugTargetPosition => HasActiveRoute ? navigation.Nodes[route[routeIndex]].Position : transform.position;

    private bool HasActiveRoute => navigation != null
        && routeIndex >= 0
        && routeIndex < route.Count
        && route[routeIndex] >= 0
        && route[routeIndex] < navigation.Nodes.Count;

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
        pathFollowWeight = Mathf.Max(0f, pathFollowWeight);
        waypointArrivalRadius = Mathf.Max(0.1f, waypointArrivalRadius);
        waypointPauseTime = new Vector2(
            Mathf.Max(0f, waypointPauseTime.x),
            Mathf.Max(Mathf.Max(0f, waypointPauseTime.x), waypointPauseTime.y));
        obstacleClearance = Mathf.Max(0.1f, obstacleClearance);
        obstacleLookAheadDistance = Mathf.Max(0.1f, obstacleLookAheadDistance);
        obstacleAvoidanceWeight = Mathf.Max(0f, obstacleAvoidanceWeight);
        boundsStrength = Mathf.Max(0f, boundsStrength);
        turnResponsiveness = Mathf.Max(0.1f, turnResponsiveness);
        predatorPerceptionRadius = Mathf.Max(0.1f, predatorPerceptionRadius);
        predatorFleeWeight = Mathf.Max(0f, predatorFleeWeight);
        panicSpeedMultiplier = Mathf.Max(1f, panicSpeedMultiplier);
        panicAccelerationMultiplier = Mathf.Max(1f, panicAccelerationMultiplier);
        threatMemoryTime = Mathf.Max(0f, threatMemoryTime);
    }

    public void Initialize(SimulationBounds simulationBounds, SimulationManager manager, CityNavigation cityNavigation)
    {
        bounds = simulationBounds;
        simulationManager = manager;
        navigation = cityNavigation;

        if (velocity.sqrMagnitude < 0.001f)
        {
            velocity = GetRandomGroundDirection() * maxSpeed * Random.Range(0.35f, 0.75f);
        }

        wanderDirection = velocity.sqrMagnitude > 0.001f ? velocity.normalized : GetRandomGroundDirection();
        wanderTargetDirection = wanderDirection;
        currentNodeIndex = navigation != null ? navigation.GetNearestNodeIndex(transform.position) : -1;
        ChooseNewRoute();
    }

    private void Awake()
    {
        wanderDirection = GetRandomGroundDirection();
        wanderTargetDirection = wanderDirection;
        velocity = wanderDirection * maxSpeed * Random.Range(0.35f, 0.75f);
        nextWanderTargetTime = Time.time + Random.Range(0f, wanderTargetInterval);
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        UpdateRouteState();

        Vector3 acceleration = CalculateSteering();
        velocity += acceleration * deltaTime;
        bool isThreatened = IsThreatened;
        float targetMaxSpeed = isThreatened ? maxSpeed * panicSpeedMultiplier : maxSpeed;
        velocity = Vector3.ClampMagnitude(FlattenVector(velocity), IsPaused && !isThreatened ? maxSpeed * 0.35f : targetMaxSpeed);

        transform.position += velocity * deltaTime;

        if (bounds != null && !bounds.Contains(transform.position))
        {
            transform.position = bounds.ClosestPointInside(transform.position);
            velocity = Vector3.ProjectOnPlane(velocity, FlattenVector(transform.position - bounds.Center).normalized);
        }

        transform.position = FlattenPoint(transform.position);
        ResolveObstacleOverlap();
        RotateTowardVelocity(deltaTime);
    }

    public void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        if (simulationManager != null)
        {
            simulationManager.UnregisterPrey(this);
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (simulationManager != null)
        {
            simulationManager.UnregisterPrey(this);
        }
    }

    private void UpdateRouteState()
    {
        if (navigation == null || !navigation.HasWaypoints)
        {
            return;
        }

        if (IsPaused)
        {
            return;
        }

        if (!HasActiveRoute)
        {
            ChooseNewRoute();
            return;
        }

        Vector3 targetPosition = navigation.Nodes[route[routeIndex]].Position;
        if (FlattenVector(targetPosition - transform.position).sqrMagnitude > waypointArrivalRadius * waypointArrivalRadius)
        {
            return;
        }

        currentNodeIndex = route[routeIndex];
        routeIndex++;

        if (routeIndex < route.Count)
        {
            return;
        }

        route.Clear();
        routeIndex = 0;
        pauseUntilTime = Time.time + Random.Range(waypointPauseTime.x, waypointPauseTime.y);
    }

    private Vector3 CalculateSteering()
    {
        if (Time.time >= nextWanderTargetTime)
        {
            wanderTargetDirection = GetRandomGroundDirection();
            nextWanderTargetTime = Time.time + wanderTargetInterval;
        }

        wanderDirection = Vector3
            .Slerp(wanderDirection, wanderTargetDirection, wanderTurnRate * Time.deltaTime)
            .normalized;

        Vector3 desiredDirection = wanderDirection * wanderStrength;

        bool isThreatened = AddPredatorFleeingSteering(ref desiredDirection);

        if (!IsPaused && HasActiveRoute)
        {
            Vector3 toTarget = FlattenVector(navigation.Nodes[route[routeIndex]].Position - transform.position);
            if (toTarget.sqrMagnitude > 0.001f)
            {
                desiredDirection += toTarget.normalized * (isThreatened ? pathFollowWeight * 0.15f : pathFollowWeight);
            }
        }
        else if (!isThreatened)
        {
            desiredDirection += -velocity.normalized * 0.5f;
        }

        AddFlockingSteering(ref desiredDirection, !isThreatened);
        AddObstacleSteering(ref desiredDirection);

        if (bounds != null)
        {
            desiredDirection += bounds.GetCenteringDirection(transform.position) * boundsStrength;
        }

        desiredDirection = FlattenVector(desiredDirection);
        float targetSpeed = isThreatened ? maxSpeed * panicSpeedMultiplier : IsPaused ? maxSpeed * 0.15f : maxSpeed;
        float targetAcceleration = isThreatened ? maxAcceleration * panicAccelerationMultiplier : maxAcceleration;
        Vector3 desiredVelocity = desiredDirection.sqrMagnitude > 0.001f
            ? desiredDirection.normalized * targetSpeed
            : Vector3.zero;
        Vector3 steering = desiredVelocity - velocity;
        return Vector3.ClampMagnitude(FlattenVector(steering), targetAcceleration);
    }

    private bool AddPredatorFleeingSteering(ref Vector3 desiredDirection)
    {
        if (simulationManager == null)
        {
            return IsThreatened;
        }

        simulationManager.GetNearbyPredators(transform.position, predatorPerceptionRadius, nearbyPredators);

        Vector3 fleeDirection = Vector3.zero;
        float nearestDistanceSqr = predatorPerceptionRadius * predatorPerceptionRadius;
        bool sawThreat = false;

        for (int i = 0; i < nearbyPredators.Count; i++)
        {
            PredatorAgent predator = nearbyPredators[i];
            if (predator == null)
            {
                continue;
            }

            Vector3 offset = FlattenVector(transform.position - predator.transform.position);
            float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr > predatorPerceptionRadius * predatorPerceptionRadius || distanceSqr <= 0.0001f)
            {
                continue;
            }

            sawThreat = true;
            float distance = Mathf.Sqrt(distanceSqr);
            fleeDirection += offset.normalized / Mathf.Max(0.1f, distance);

            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                lastThreatPosition = predator.transform.position;
            }
        }

        if (sawThreat)
        {
            lastThreatTime = Time.time;
        }
        else if (IsThreatened)
        {
            Vector3 rememberedOffset = FlattenVector(transform.position - lastThreatPosition);
            if (rememberedOffset.sqrMagnitude > 0.001f)
            {
                fleeDirection += rememberedOffset.normalized * 0.35f;
            }
        }

        if (fleeDirection.sqrMagnitude <= 0.001f)
        {
            return IsThreatened;
        }

        desiredDirection += fleeDirection.normalized * predatorFleeWeight;
        return true;
    }

    private void AddFlockingSteering(ref Vector3 desiredDirection, bool includeGroupPull)
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

        simulationManager.GetNearbyPrey(transform.position, perceptionRadius, nearbyAgents);

        for (int i = 0; i < nearbyAgents.Count; i++)
        {
            PreyAgent other = nearbyAgents[i];
            if (other == null || other == this)
            {
                continue;
            }

            Vector3 offset = FlattenVector(transform.position - other.transform.position);
            float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr > perceptionRadiusSqr || distanceSqr <= 0.0001f)
            {
                continue;
            }

            flockmateCount++;
            alignment += FlattenVector(other.Velocity).normalized;
            cohesion += FlattenPoint(other.transform.position);

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
        cohesion = FlattenVector((cohesion / flockmateCount) - transform.position).normalized;

        if (includeGroupPull)
        {
            desiredDirection += alignment * alignmentWeight;
            desiredDirection += cohesion * cohesionWeight;
        }

        if (separationCount > 0)
        {
            desiredDirection += (separation / separationCount).normalized * separationWeight;
        }
    }

    private void AddObstacleSteering(ref Vector3 desiredDirection)
    {
        if (navigation == null)
        {
            return;
        }

        Vector3 avoidance = navigation.GetObstacleAvoidance(
            transform.position,
            velocity.sqrMagnitude > 0.001f ? velocity : desiredDirection,
            obstacleLookAheadDistance,
            obstacleClearance);

        desiredDirection += avoidance * obstacleAvoidanceWeight;
    }

    private void ChooseNewRoute()
    {
        if (navigation == null || !navigation.HasWaypoints)
        {
            route.Clear();
            routeIndex = 0;
            return;
        }

        int startIndex = currentNodeIndex >= 0 ? currentNodeIndex : navigation.GetNearestNodeIndex(transform.position);
        int targetIndex = navigation.GetRandomDifferentNodeIndex(startIndex);

        bool hasRoute = navigation.TryBuildRoute(startIndex, targetIndex, route);
        if (!hasRoute && startIndex >= 0 && startIndex < navigation.Nodes.Count && navigation.Nodes[startIndex].Neighbors.Count > 0)
        {
            List<int> neighbors = navigation.Nodes[startIndex].Neighbors;
            route.Clear();
            route.Add(startIndex);
            route.Add(neighbors[Random.Range(0, neighbors.Count)]);
        }

        routeIndex = 0;

        if (route.Count > 1)
        {
            float startDistanceSqr = FlattenVector(navigation.Nodes[route[0]].Position - transform.position).sqrMagnitude;
            if (startDistanceSqr <= waypointArrivalRadius * waypointArrivalRadius)
            {
                routeIndex = 1;
            }
        }
    }

    private void ResolveObstacleOverlap()
    {
        if (navigation == null)
        {
            return;
        }

        Vector3 before = transform.position;
        Vector3 projected = navigation.ProjectOutsideObstacles(before, obstacleClearance * 0.5f);
        projected = FlattenPoint(projected);

        if ((projected - before).sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 push = FlattenVector(projected - before);
        transform.position = projected;
        velocity = Vector3.ProjectOnPlane(velocity, push.normalized) + push.normalized * Mathf.Min(maxSpeed * 0.35f, push.magnitude / Mathf.Max(Time.deltaTime, 0.001f));
    }

    private void RotateTowardVelocity(float deltaTime)
    {
        if (velocity.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector3 forward = FlattenVector(velocity).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnResponsiveness * deltaTime);
    }

    private Vector3 FlattenPoint(Vector3 point)
    {
        return bounds != null ? bounds.ProjectPointToGround(point) : new Vector3(point.x, 0f, point.z);
    }

    private Vector3 FlattenVector(Vector3 vector)
    {
        return bounds != null ? bounds.ProjectVectorToGround(vector) : new Vector3(vector.x, 0f, vector.z);
    }

    private Vector3 GetRandomGroundDirection()
    {
        return bounds != null ? bounds.RandomGroundDirection() : RandomGroundDirectionFallback();
    }

    private bool IsThreatened => Time.time - lastThreatTime <= threatMemoryTime;

    private static Vector3 RandomGroundDirectionFallback()
    {
        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.001f)
        {
            return Vector3.forward;
        }

        return new Vector3(direction.x, 0f, direction.y);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, perceptionRadius);

        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, separationRadius);

        Gizmos.color = IsPaused ? new Color(1f, 0.85f, 0.25f, 0.85f) : new Color(0.35f, 1f, 0.45f, 0.85f);
        Gizmos.DrawLine(transform.position, DebugTargetPosition);
        Gizmos.DrawWireSphere(DebugTargetPosition, waypointArrivalRadius);
    }
}
