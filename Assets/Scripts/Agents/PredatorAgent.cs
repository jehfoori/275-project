using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PredatorAgent : MonoBehaviour
{
    [SerializeField] private float maxSpeed = 12.5f;
    [SerializeField] private float maxAcceleration = 24f;
    [SerializeField] private float preyDetectionRadius = 38f;
    [SerializeField] private float attackRadius = 2.3f;
    [SerializeField] private float attackReachBuffer = 1.25f;
    [SerializeField] private float eatPauseTime = 1.25f;
    [SerializeField] private float pursuitWeight = 12f;
    [SerializeField] private float pursuitLeadTime = 0.55f;
    [SerializeField] private float wanderStrength = 0.55f;
    [SerializeField] private float wanderTurnRate = 0.65f;
    [SerializeField] private float wanderTargetInterval = 1.8f;
    [SerializeField] private float pathFollowWeight = 6f;
    [SerializeField] private float waypointArrivalRadius = 4f;
    [SerializeField] private float obstacleClearance = 4.2f;
    [SerializeField] private float obstacleLookAheadDistance = 9f;
    [SerializeField] private float obstacleAvoidanceWeight = 12f;
    [SerializeField] private float boundsStrength = 12f;
    [SerializeField] private float turnResponsiveness = 7f;
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float damageCooldown = 0.35f;
    [SerializeField] private float defeatDestroyDelay = 1.5f;
    [SerializeField] private bool autoAddHealthBar = true;
    [SerializeField] private float debugDamageAmount = 25f;

    private readonly List<int> route = new List<int>();
    private readonly List<PreyAgent> nearbyPrey = new List<PreyAgent>(32);
    private SimulationBounds bounds;
    private SimulationManager simulationManager;
    private CityNavigation navigation;
    private AgentVisualController visualController;
    private PreyAgent targetPrey;
    private Vector3 velocity;
    private Vector3 wanderDirection;
    private Vector3 wanderTargetDirection;
    private float nextWanderTargetTime;
    private float pauseUntilTime;
    private int routeIndex;
    private int currentNodeIndex = -1;
    private float currentHealth;
    private float nextDamageTime;
    private float pendingCelebrateTime = -1f;
    private bool isDefeated;

    public Vector3 Velocity => velocity;
    public bool IsEating => Time.time < pauseUntilTime;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthNormalized => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public bool IsDefeated => isDefeated;
    public PreyAgent TargetPrey => targetPrey;

    private bool HasActiveRoute => navigation != null
        && routeIndex >= 0
        && routeIndex < route.Count
        && route[routeIndex] >= 0
        && route[routeIndex] < navigation.Nodes.Count;

    private void OnValidate()
    {
        maxSpeed = Mathf.Max(0.1f, maxSpeed);
        maxAcceleration = Mathf.Max(0.1f, maxAcceleration);
        preyDetectionRadius = Mathf.Max(0.1f, preyDetectionRadius);
        attackRadius = Mathf.Max(0.1f, attackRadius);
        attackReachBuffer = Mathf.Max(0f, attackReachBuffer);
        eatPauseTime = Mathf.Max(0f, eatPauseTime);
        pursuitWeight = Mathf.Max(0f, pursuitWeight);
        pursuitLeadTime = Mathf.Max(0f, pursuitLeadTime);
        wanderStrength = Mathf.Max(0f, wanderStrength);
        wanderTurnRate = Mathf.Max(0.05f, wanderTurnRate);
        wanderTargetInterval = Mathf.Max(0.1f, wanderTargetInterval);
        pathFollowWeight = Mathf.Max(0f, pathFollowWeight);
        waypointArrivalRadius = Mathf.Max(0.1f, waypointArrivalRadius);
        obstacleClearance = Mathf.Max(0.1f, obstacleClearance);
        obstacleLookAheadDistance = Mathf.Max(0.1f, obstacleLookAheadDistance);
        obstacleAvoidanceWeight = Mathf.Max(0f, obstacleAvoidanceWeight);
        boundsStrength = Mathf.Max(0f, boundsStrength);
        turnResponsiveness = Mathf.Max(0.1f, turnResponsiveness);
        maxHealth = Mathf.Max(1f, maxHealth);
        damageCooldown = Mathf.Max(0f, damageCooldown);
        defeatDestroyDelay = Mathf.Max(0f, defeatDestroyDelay);
        debugDamageAmount = Mathf.Max(0f, debugDamageAmount);
    }

    public void Initialize(SimulationBounds simulationBounds, SimulationManager manager, CityNavigation cityNavigation)
    {
        bounds = simulationBounds;
        simulationManager = manager;
        navigation = cityNavigation;

        if (velocity.sqrMagnitude < 0.001f)
        {
            velocity = GetRandomGroundDirection() * maxSpeed * Random.Range(0.25f, 0.55f);
        }

        wanderDirection = velocity.sqrMagnitude > 0.001f ? velocity.normalized : GetRandomGroundDirection();
        wanderTargetDirection = wanderDirection;
        currentNodeIndex = navigation != null ? navigation.GetNearestNodeIndex(transform.position) : -1;
        ChooseNewRoute();
    }

    private void Awake()
    {
        currentHealth = maxHealth;
        visualController = GetComponent<AgentVisualController>();
        EnsureHealthBar();
        wanderDirection = GetRandomGroundDirection();
        wanderTargetDirection = wanderDirection;
        velocity = wanderDirection * maxSpeed * Random.Range(0.25f, 0.55f);
        nextWanderTargetTime = Time.time + Random.Range(0f, wanderTargetInterval);
    }

    private void Update()
    {
        if (isDefeated)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        if (!IsEating)
        {
            UpdateRouteState();
            AcquireTarget();
            TryEatNearbyPrey();
        }
        else
        {
            TryTriggerPendingCelebrate();
        }

        Vector3 acceleration = IsEating ? -velocity : CalculateSteering();
        velocity += acceleration * deltaTime;
        velocity = Vector3.ClampMagnitude(FlattenVector(velocity), IsEating ? maxSpeed * 0.15f : maxSpeed);

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

    private void OnDestroy()
    {
        if (simulationManager != null)
        {
            simulationManager.UnregisterPredator(this);
        }
    }

    public bool TakeDamage(float amount)
    {
        if (isDefeated || amount <= 0f || Time.time < nextDamageTime)
        {
            return false;
        }

        nextDamageTime = Time.time + damageCooldown;
        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (currentHealth <= 0f)
        {
            Defeat();
        }
        else if (visualController != null)
        {
            visualController.TriggerHit();
        }

        return true;
    }

    [ContextMenu("Debug Damage Titan")]
    private void DebugDamageTitan()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("Debug titan damage is only available in Play Mode.", this);
            return;
        }

        TakeDamage(debugDamageAmount);
    }

    private void Defeat()
    {
        if (isDefeated)
        {
            return;
        }

        isDefeated = true;
        targetPrey = null;
        velocity = Vector3.zero;
        pendingCelebrateTime = -1f;

        if (visualController != null)
        {
            visualController.TriggerDefeat();
        }

        if (simulationManager != null)
        {
            simulationManager.RecordTitanDefeated(this);
        }

        Destroy(gameObject, defeatDestroyDelay);
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

        if (targetPrey != null)
        {
            Vector3 predictedTarget = targetPrey.transform.position + targetPrey.Velocity * pursuitLeadTime;
            Vector3 toPrey = FlattenVector(predictedTarget - transform.position);
            if (toPrey.sqrMagnitude > 0.001f)
            {
                desiredDirection += toPrey.normalized * pursuitWeight;
            }
        }
        else if (HasActiveRoute)
        {
            Vector3 toTarget = FlattenVector(navigation.Nodes[route[routeIndex]].Position - transform.position);
            if (toTarget.sqrMagnitude > 0.001f)
            {
                desiredDirection += toTarget.normalized * pathFollowWeight;
            }
        }

        AddObstacleSteering(ref desiredDirection);

        if (bounds != null)
        {
            desiredDirection += bounds.GetCenteringDirection(transform.position) * boundsStrength;
        }

        desiredDirection = FlattenVector(desiredDirection);
        Vector3 desiredVelocity = desiredDirection.sqrMagnitude > 0.001f
            ? desiredDirection.normalized * maxSpeed
            : Vector3.zero;

        return Vector3.ClampMagnitude(FlattenVector(desiredVelocity - velocity), maxAcceleration);
    }

    private void AcquireTarget()
    {
        if (simulationManager == null)
        {
            targetPrey = null;
            return;
        }

        simulationManager.GetNearbyPrey(transform.position, preyDetectionRadius, nearbyPrey);

        PreyAgent nearest = null;
        float nearestDistanceSqr = preyDetectionRadius * preyDetectionRadius;

        for (int i = 0; i < nearbyPrey.Count; i++)
        {
            PreyAgent prey = nearbyPrey[i];
            if (prey == null)
            {
                continue;
            }

            float distanceSqr = FlattenVector(prey.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr)
            {
                nearest = prey;
                nearestDistanceSqr = distanceSqr;
            }
        }

        targetPrey = nearest;
    }

    private void TryEatNearbyPrey()
    {
        if (simulationManager == null)
        {
            return;
        }

        float effectiveAttackRadius = attackRadius + attackReachBuffer;
        simulationManager.GetNearbyPrey(transform.position, effectiveAttackRadius, nearbyPrey);
        float attackRadiusSqr = effectiveAttackRadius * effectiveAttackRadius;

        for (int i = 0; i < nearbyPrey.Count; i++)
        {
            PreyAgent prey = nearbyPrey[i];
            if (prey == null)
            {
                continue;
            }

            if (FlattenVector(prey.transform.position - transform.position).sqrMagnitude > attackRadiusSqr)
            {
                continue;
            }

            prey.Die(this);
            targetPrey = null;
            pauseUntilTime = Time.time + eatPauseTime;
            pendingCelebrateTime = Time.time + Mathf.Min(0.45f, eatPauseTime * 0.5f);
            velocity *= 0.2f;
            if (visualController != null)
            {
                visualController.TriggerAttack();
            }

            return;
        }
    }

    private void TryTriggerPendingCelebrate()
    {
        if (pendingCelebrateTime < 0f || Time.time < pendingCelebrateTime)
        {
            return;
        }

        pendingCelebrateTime = -1f;
        if (visualController != null)
        {
            visualController.TriggerCelebrate();
        }
    }

    private void EnsureHealthBar()
    {
        if (!autoAddHealthBar || GetComponent<PredatorHealthBar>() != null)
        {
            return;
        }

        gameObject.AddComponent<PredatorHealthBar>();
    }

    private void UpdateRouteState()
    {
        if (navigation == null || !navigation.HasWaypoints || targetPrey != null)
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

        if (routeIndex >= route.Count)
        {
            route.Clear();
            routeIndex = 0;
            ChooseNewRoute();
        }
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
        Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, preyDetectionRadius);

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, attackRadius);

        if (attackReachBuffer > 0f)
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, attackRadius + attackReachBuffer);
        }
    }
}
