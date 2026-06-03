using System.Collections.Generic;
using UnityEngine;

public sealed class PreyAgent : MonoBehaviour
{
    public enum HumanRole
    {
        Civilian,
        Soldier
    }

    private enum HumanState
    {
        Calm,
        Evacuating,
        Escaped
    }

    private enum SoldierState
    {
        Patrol,
        Alert,
        Rally,
        Engage,
        Withdraw
    }

    [SerializeField] private HumanRole role = HumanRole.Civilian;
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
    [Header("Evacuation")]
    [SerializeField] private float evacuationArrivalRadius = 4f;
    [SerializeField] private float evacuationPathWeightMultiplier = 1.2f;
    [SerializeField] private float evacuationWanderMultiplier = 0.2f;
    [Header("Panic Contagion")]
    [SerializeField] private float panicContagionRadius = 14f;
    [SerializeField] private float panicContagionCheckInterval = 0.35f;
    [SerializeField] private int panicContagionMinNeighbors = 2;
    [SerializeField] private float panicContagionRatio = 0.35f;
    [Header("Needs And Stress")]
    [SerializeField] private float initialStress = 0f;
    [SerializeField] private float stressGainNearPredator = 0.75f;
    [SerializeField] private float stressGainWhenFleeing = 0.28f;
    [SerializeField] private float stressGainFromPanicNeighbors = 0.18f;
    [SerializeField] private float stressEvacuationThreshold = 0.75f;
    [SerializeField] private float fatigueGainMoving = 0.035f;
    [SerializeField] private float fatigueGainSprinting = 0.09f;
    [SerializeField] private float fatigueSpeedPenalty = 0.32f;
    [SerializeField] private float fatigueAccelerationPenalty = 0.25f;
    [SerializeField] private float stressFleeWeightMultiplier = 0.45f;
    [SerializeField] private float stressSeparationMultiplier = 0.35f;
    [SerializeField] private float stressCohesionPenalty = 0.5f;
    [SerializeField] private float stressWanderMultiplier = 0.6f;
    [SerializeField] private float stressEvacuationPathMultiplier = 0.35f;
    [SerializeField] private float soldierStressAttackPenalty = 0.35f;
    [Header("Soldier")]
    [SerializeField] private float soldierThreatRadius = 26f;
    [SerializeField] private float soldierAttackRadius = 4f;
    [SerializeField] private float soldierAttackDamage = 18f;
    [SerializeField] private float soldierAttackCooldown = 1.15f;
    [SerializeField] private float soldierDefensiveAttackRadiusMultiplier = 1.15f;
    [SerializeField] private float soldierApproachWeight = 18f;
    [SerializeField] private float soldierAlertEvacueeRadius = 18f;
    [SerializeField] private int soldierAlertEvacueeMinNeighbors = 2;
    [SerializeField] private float soldierAlertEvacueeRatio = 0.25f;
    [SerializeField] private int minSoldiersToEngage = 3;
    [SerializeField] private float soldierSupportRadius = 24f;
    [SerializeField] private float soldierRallyMinDistance = 12f;
    [SerializeField] private float soldierRallyMaxDistance = 28f;
    [SerializeField] private float soldierRallyApproachWeight = 12f;
    [SerializeField] private float soldierRallyRetreatWeight = 16f;
    [SerializeField] private float soldierRallyStrafeWeight = 3f;

    private readonly List<int> route = new List<int>();
    private readonly List<PreyAgent> nearbyAgents = new List<PreyAgent>(32);
    private readonly List<PredatorAgent> nearbyPredators = new List<PredatorAgent>(8);
    private SimulationBounds bounds;
    private SimulationManager simulationManager;
    private CityNavigation navigation;
    private AgentVisualController visualController;
    private Vector3 velocity;
    private Vector3 wanderDirection;
    private Vector3 wanderTargetDirection;
    private float nextWanderTargetTime;
    private float pauseUntilTime;
    private float lastThreatTime = -999f;
    private float nextSoldierAttackTime;
    private float nextPanicContagionCheckTime;
    private float stress;
    private float fatigue;
    private Vector3 lastThreatPosition;
    private PredatorAgent soldierTarget;
    private int routeIndex;
    private int currentNodeIndex = -1;
    private int evacuationTargetNodeIndex = -1;
    private HumanState humanState = HumanState.Calm;
    private SoldierState soldierState = SoldierState.Patrol;
    private bool isDead;

    public HumanRole Role => role;
    public Vector3 Velocity => velocity;
    public bool IsEvacuating => humanState == HumanState.Evacuating;
    public float Stress => stress;
    public float Fatigue => fatigue;
    public IReadOnlyList<int> DebugRoute => route;
    public int DebugRouteIndex => routeIndex;
    public bool IsPaused => Time.time < pauseUntilTime;
    public Vector3 DebugTargetPosition => HasActiveEvacuationTarget
        ? navigation.Nodes[evacuationTargetNodeIndex].Position
        : HasActiveRoute
            ? navigation.Nodes[route[routeIndex]].Position
            : transform.position;

    private bool HasActiveRoute => navigation != null
        && routeIndex >= 0
        && routeIndex < route.Count
        && route[routeIndex] >= 0
        && route[routeIndex] < navigation.Nodes.Count;

    private bool HasActiveEvacuationTarget => navigation != null
        && evacuationTargetNodeIndex >= 0
        && evacuationTargetNodeIndex < navigation.Nodes.Count;

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
        evacuationArrivalRadius = Mathf.Max(0.1f, evacuationArrivalRadius);
        evacuationPathWeightMultiplier = Mathf.Max(0f, evacuationPathWeightMultiplier);
        evacuationWanderMultiplier = Mathf.Clamp01(evacuationWanderMultiplier);
        panicContagionRadius = Mathf.Max(0.1f, panicContagionRadius);
        panicContagionCheckInterval = Mathf.Max(0.05f, panicContagionCheckInterval);
        panicContagionMinNeighbors = Mathf.Max(1, panicContagionMinNeighbors);
        panicContagionRatio = Mathf.Clamp01(panicContagionRatio);
        initialStress = Mathf.Clamp01(initialStress);
        stressGainNearPredator = Mathf.Max(0f, stressGainNearPredator);
        stressGainWhenFleeing = Mathf.Max(0f, stressGainWhenFleeing);
        stressGainFromPanicNeighbors = Mathf.Max(0f, stressGainFromPanicNeighbors);
        stressEvacuationThreshold = Mathf.Clamp01(stressEvacuationThreshold);
        fatigueGainMoving = Mathf.Max(0f, fatigueGainMoving);
        fatigueGainSprinting = Mathf.Max(0f, fatigueGainSprinting);
        fatigueSpeedPenalty = Mathf.Clamp01(fatigueSpeedPenalty);
        fatigueAccelerationPenalty = Mathf.Clamp01(fatigueAccelerationPenalty);
        stressFleeWeightMultiplier = Mathf.Max(0f, stressFleeWeightMultiplier);
        stressSeparationMultiplier = Mathf.Max(0f, stressSeparationMultiplier);
        stressCohesionPenalty = Mathf.Clamp01(stressCohesionPenalty);
        stressWanderMultiplier = Mathf.Clamp01(stressWanderMultiplier);
        stressEvacuationPathMultiplier = Mathf.Max(0f, stressEvacuationPathMultiplier);
        soldierStressAttackPenalty = Mathf.Clamp01(soldierStressAttackPenalty);
        soldierThreatRadius = Mathf.Max(0.1f, soldierThreatRadius);
        soldierAttackRadius = Mathf.Clamp(soldierAttackRadius, 0.1f, soldierThreatRadius);
        soldierAttackDamage = Mathf.Max(0f, soldierAttackDamage);
        soldierAttackCooldown = Mathf.Max(0f, soldierAttackCooldown);
        soldierDefensiveAttackRadiusMultiplier = Mathf.Max(1f, soldierDefensiveAttackRadiusMultiplier);
        soldierApproachWeight = Mathf.Max(0f, soldierApproachWeight);
        soldierAlertEvacueeRadius = Mathf.Max(0.1f, soldierAlertEvacueeRadius);
        soldierAlertEvacueeMinNeighbors = Mathf.Max(1, soldierAlertEvacueeMinNeighbors);
        soldierAlertEvacueeRatio = Mathf.Clamp01(soldierAlertEvacueeRatio);
        minSoldiersToEngage = Mathf.Max(1, minSoldiersToEngage);
        soldierSupportRadius = Mathf.Max(0.1f, soldierSupportRadius);
        soldierRallyMinDistance = Mathf.Max(soldierAttackRadius, soldierRallyMinDistance);
        soldierRallyMaxDistance = Mathf.Max(soldierRallyMinDistance + 0.1f, soldierRallyMaxDistance);
        soldierRallyApproachWeight = Mathf.Max(0f, soldierRallyApproachWeight);
        soldierRallyRetreatWeight = Mathf.Max(0f, soldierRallyRetreatWeight);
        soldierRallyStrafeWeight = Mathf.Max(0f, soldierRallyStrafeWeight);
    }

    public void SetRole(HumanRole newRole)
    {
        role = newRole;
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
        visualController = GetComponent<AgentVisualController>();
        stress = Mathf.Clamp01(initialStress);
        wanderDirection = GetRandomGroundDirection();
        wanderTargetDirection = wanderDirection;
        velocity = wanderDirection * maxSpeed * Random.Range(0.35f, 0.75f);
        nextWanderTargetTime = Time.time + Random.Range(0f, wanderTargetInterval);
        nextPanicContagionCheckTime = Time.time + Random.Range(0f, panicContagionCheckInterval);
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        UpdateRouteState();
        TryPanicContagion();

        Vector3 acceleration = CalculateSteering();
        bool isThreatened = IsThreatened;
        UpdateNeeds(deltaTime, isThreatened);
        velocity += acceleration * deltaTime;
        float targetMaxSpeed = GetEffectiveSpeed(isThreatened ? maxSpeed * panicSpeedMultiplier : maxSpeed);
        velocity = Vector3.ClampMagnitude(FlattenVector(velocity), IsPaused && !isThreatened ? maxSpeed * 0.35f : targetMaxSpeed);

        transform.position += velocity * deltaTime;

        if (bounds != null && !bounds.Contains(transform.position))
        {
            transform.position = bounds.ClosestPointInside(transform.position);
            velocity = Vector3.ProjectOnPlane(velocity, FlattenVector(transform.position - bounds.Center).normalized);
        }

        transform.position = FlattenPoint(transform.position);
        ResolveObstacleOverlap();
        TryEscapeIfArrived();
        TrySoldierAttack();
        RotateTowardVelocity(deltaTime);
    }

    public void Die(PredatorAgent killedBy = null)
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        if (simulationManager != null)
        {
            if (killedBy != null)
            {
                simulationManager.RecordHumanCasualty(this);
            }
            else
            {
                simulationManager.UnregisterPrey(this);
            }
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

        if (humanState == HumanState.Evacuating)
        {
            UpdateEvacuationTargetState();
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
        UpdateSoldierState();

        if (Time.time >= nextWanderTargetTime)
        {
            wanderTargetDirection = GetRandomGroundDirection();
            nextWanderTargetTime = Time.time + wanderTargetInterval;
        }

        wanderDirection = Vector3
            .Slerp(wanderDirection, wanderTargetDirection, wanderTurnRate * Time.deltaTime)
            .normalized;

        float activeWanderStrength = humanState == HumanState.Evacuating
            ? wanderStrength * evacuationWanderMultiplier
            : wanderStrength;
        activeWanderStrength *= role == HumanRole.Civilian
            ? 1f + stress * stressWanderMultiplier
            : 1f - stress * stressWanderMultiplier;
        Vector3 desiredDirection = wanderDirection * activeWanderStrength;

        soldierTarget = role == HumanRole.Soldier && soldierState != SoldierState.Withdraw
            ? FindNearestPredator(soldierThreatRadius)
            : null;
        if (role == HumanRole.Soldier && soldierTarget == null && (soldierState == SoldierState.Rally || soldierState == SoldierState.Engage))
        {
            soldierState = SoldierState.Alert;
        }

        bool isEngagingPredator = false;
        bool isThreatened = false;

        if (role == HumanRole.Soldier && soldierState == SoldierState.Withdraw)
        {
            isThreatened = AddPredatorFleeingSteering(ref desiredDirection);
        }
        else if (role == HumanRole.Soldier && soldierTarget != null)
        {
            AddStressFromVisiblePredator(soldierTarget);
            int nearbySoldierCount = CountNearbyCombatSoldiers(soldierTarget);
            soldierState = nearbySoldierCount >= minSoldiersToEngage ? SoldierState.Engage : SoldierState.Rally;
            isEngagingPredator = true;

            if (soldierState == SoldierState.Engage)
            {
                AddSoldierEngagementSteering(ref desiredDirection, soldierTarget);
            }
            else
            {
                AddSoldierRallySteering(ref desiredDirection, soldierTarget);
                isThreatened = IsSoldierInRallyDanger(soldierTarget);
            }
        }
        else
        {
            isThreatened = AddPredatorFleeingSteering(ref desiredDirection);
        }

        if (humanState == HumanState.Evacuating)
        {
            if (isThreatened)
            {
                evacuationTargetNodeIndex = -1;
            }
            else
            {
                EnsureEvacuationTarget();
            }
        }

        if (!isEngagingPredator && !isThreatened && humanState == HumanState.Evacuating && HasActiveEvacuationTarget)
        {
            Vector3 toTarget = FlattenVector(navigation.Nodes[evacuationTargetNodeIndex].Position - transform.position);
            if (toTarget.sqrMagnitude > 0.001f)
            {
                desiredDirection += toTarget.normalized * pathFollowWeight * GetEffectiveEvacuationPathMultiplier();
            }
        }
        else if (!isEngagingPredator && !IsPaused && HasActiveRoute)
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

        AddFlockingSteering(ref desiredDirection, humanState == HumanState.Calm && !isThreatened && !isEngagingPredator);
        AddObstacleSteering(ref desiredDirection);

        if (bounds != null)
        {
            desiredDirection += bounds.GetCenteringDirection(transform.position) * boundsStrength;
        }

        desiredDirection = FlattenVector(desiredDirection);
        bool shouldSlowForPause = IsPaused && humanState != HumanState.Evacuating;
        float targetSpeed = GetEffectiveSpeed(isThreatened ? maxSpeed * panicSpeedMultiplier : shouldSlowForPause ? maxSpeed * 0.15f : maxSpeed);
        float targetAcceleration = GetEffectiveAcceleration(isThreatened ? maxAcceleration * panicAccelerationMultiplier : maxAcceleration);
        Vector3 desiredVelocity = desiredDirection.sqrMagnitude > 0.001f
            ? desiredDirection.normalized * targetSpeed
            : Vector3.zero;
        Vector3 steering = desiredVelocity - velocity;
        return Vector3.ClampMagnitude(FlattenVector(steering), targetAcceleration);
    }

    private PredatorAgent FindNearestPredator(float radius)
    {
        if (simulationManager == null)
        {
            return null;
        }

        simulationManager.GetNearbyPredators(transform.position, radius, nearbyPredators);

        PredatorAgent nearest = null;
        float nearestDistanceSqr = radius * radius;

        for (int i = 0; i < nearbyPredators.Count; i++)
        {
            PredatorAgent predator = nearbyPredators[i];
            if (predator == null || predator.IsDefeated)
            {
                continue;
            }

            float distanceSqr = FlattenVector(predator.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr)
            {
                nearest = predator;
                nearestDistanceSqr = distanceSqr;
            }
        }

        return nearest;
    }

    private void UpdateSoldierState()
    {
        if (role != HumanRole.Soldier || soldierState == SoldierState.Withdraw)
        {
            return;
        }

        if (simulationManager != null && simulationManager.ShouldSoldiersWithdraw)
        {
            BeginSoldierWithdraw();
            return;
        }

        if (FindNearestPredator(soldierThreatRadius) != null || HasNearbyEvacuatingCiviliansForAlert())
        {
            if (soldierState == SoldierState.Patrol)
            {
                soldierState = SoldierState.Alert;
                pauseUntilTime = 0f;
            }
        }
    }

    private bool HasNearbyEvacuatingCiviliansForAlert()
    {
        if (simulationManager == null)
        {
            return false;
        }

        simulationManager.GetNearbyPrey(transform.position, soldierAlertEvacueeRadius, nearbyAgents);

        int civilianCount = 0;
        int evacuatingCivilianCount = 0;
        float radiusSqr = soldierAlertEvacueeRadius * soldierAlertEvacueeRadius;

        for (int i = 0; i < nearbyAgents.Count; i++)
        {
            PreyAgent other = nearbyAgents[i];
            if (other == null || other == this || other.Role != HumanRole.Civilian)
            {
                continue;
            }

            Vector3 offset = FlattenVector(other.transform.position - transform.position);
            if (offset.sqrMagnitude > radiusSqr)
            {
                continue;
            }

            civilianCount++;
            if (other.IsEvacuating)
            {
                evacuatingCivilianCount++;
            }
        }

        return evacuatingCivilianCount >= soldierAlertEvacueeMinNeighbors
            && civilianCount > 0
            && evacuatingCivilianCount / (float)civilianCount >= soldierAlertEvacueeRatio;
    }

    private int CountNearbyCombatSoldiers(PredatorAgent predator)
    {
        if (simulationManager == null || predator == null)
        {
            return 0;
        }

        simulationManager.GetNearbyPrey(predator.transform.position, soldierSupportRadius, nearbyAgents);

        int count = 0;
        float supportRadiusSqr = soldierSupportRadius * soldierSupportRadius;

        for (int i = 0; i < nearbyAgents.Count; i++)
        {
            PreyAgent other = nearbyAgents[i];
            if (other == null || other.Role != HumanRole.Soldier || other.soldierState == SoldierState.Withdraw)
            {
                continue;
            }

            Vector3 offset = FlattenVector(other.transform.position - predator.transform.position);
            if (offset.sqrMagnitude <= supportRadiusSqr)
            {
                count++;
            }
        }

        return count;
    }

    private void AddSoldierEngagementSteering(ref Vector3 desiredDirection, PredatorAgent predator)
    {
        Vector3 toPredator = FlattenVector(predator.transform.position - transform.position);
        if (toPredator.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float distance = toPredator.magnitude;
        if (distance > soldierAttackRadius * 0.75f)
        {
            desiredDirection += toPredator.normalized * soldierApproachWeight;
            return;
        }

        desiredDirection += -velocity.normalized * 0.4f;
    }

    private void AddSoldierRallySteering(ref Vector3 desiredDirection, PredatorAgent predator)
    {
        Vector3 toPredator = FlattenVector(predator.transform.position - transform.position);
        if (toPredator.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float distance = toPredator.magnitude;
        Vector3 towardPredator = toPredator / distance;

        if (distance < soldierRallyMinDistance)
        {
            desiredDirection += -towardPredator * soldierRallyRetreatWeight;
            return;
        }

        if (distance > soldierRallyMaxDistance)
        {
            desiredDirection += towardPredator * soldierRallyApproachWeight;
            return;
        }

        float strafeSign = (GetInstanceID() & 1) == 0 ? 1f : -1f;
        Vector3 lateral = Vector3.Cross(Vector3.up, towardPredator).normalized * strafeSign;
        desiredDirection += lateral * soldierRallyStrafeWeight;
        desiredDirection += -velocity.normalized * 0.25f;
    }

    private bool IsSoldierInRallyDanger(PredatorAgent predator)
    {
        if (predator == null)
        {
            return false;
        }

        float dangerDistance = Mathf.Max(soldierAttackRadius, soldierRallyMinDistance);
        return FlattenVector(predator.transform.position - transform.position).sqrMagnitude <= dangerDistance * dangerDistance;
    }

    private void TrySoldierAttack()
    {
        if (role != HumanRole.Soldier || Time.time < nextSoldierAttackTime)
        {
            return;
        }

        bool canDefensivelyAttack = soldierState == SoldierState.Rally;
        if (soldierState != SoldierState.Engage && !canDefensivelyAttack)
        {
            return;
        }

        if (soldierTarget == null || soldierTarget.IsDefeated)
        {
            soldierTarget = FindNearestPredator(soldierAttackRadius);
        }

        if (soldierTarget == null)
        {
            return;
        }

        float activeAttackRadius = canDefensivelyAttack
            ? soldierAttackRadius * soldierDefensiveAttackRadiusMultiplier
            : soldierAttackRadius;
        if (FlattenVector(soldierTarget.transform.position - transform.position).sqrMagnitude > activeAttackRadius * activeAttackRadius)
        {
            return;
        }

        float effectiveDamage = soldierAttackDamage * (1f - stress * soldierStressAttackPenalty);
        bool damageApplied = soldierTarget.TakeDamage(effectiveDamage);
        if (damageApplied && visualController != null)
        {
            visualController.TriggerAttack();
        }

        nextSoldierAttackTime = Time.time + soldierAttackCooldown;
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
            if (predator == null || predator.IsDefeated)
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
            AddStressFromPredatorDistance(Mathf.Sqrt(nearestDistanceSqr), predatorPerceptionRadius);
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

        desiredDirection += fleeDirection.normalized * predatorFleeWeight * (1f + stress * stressFleeWeightMultiplier);
        return true;
    }

    private void BeginEvacuation()
    {
        if (role != HumanRole.Civilian || humanState != HumanState.Calm)
        {
            return;
        }

        humanState = HumanState.Evacuating;
        evacuationTargetNodeIndex = -1;
        pauseUntilTime = 0f;
        route.Clear();
        routeIndex = 0;
        EnsureEvacuationTarget();
    }

    private void BeginSoldierWithdraw()
    {
        if (role != HumanRole.Soldier || soldierState == SoldierState.Withdraw)
        {
            return;
        }

        soldierState = SoldierState.Withdraw;
        humanState = HumanState.Evacuating;
        soldierTarget = null;
        evacuationTargetNodeIndex = -1;
        pauseUntilTime = 0f;
        route.Clear();
        routeIndex = 0;
        EnsureEvacuationTarget();
    }

    private void TryPanicContagion()
    {
        if (role != HumanRole.Civilian || humanState != HumanState.Calm || simulationManager == null || Time.time < nextPanicContagionCheckTime)
        {
            return;
        }

        nextPanicContagionCheckTime = Time.time + panicContagionCheckInterval;
        simulationManager.GetNearbyPrey(transform.position, panicContagionRadius, nearbyAgents);

        int civilianNeighborCount = 0;
        int evacuatingNeighborCount = 0;
        float radiusSqr = panicContagionRadius * panicContagionRadius;

        for (int i = 0; i < nearbyAgents.Count; i++)
        {
            PreyAgent other = nearbyAgents[i];
            if (other == null || other == this || other.Role != HumanRole.Civilian)
            {
                continue;
            }

            Vector3 offset = FlattenVector(other.transform.position - transform.position);
            if (offset.sqrMagnitude > radiusSqr)
            {
                continue;
            }

            civilianNeighborCount++;
            if (other.IsEvacuating)
            {
                evacuatingNeighborCount++;
            }
        }

        if (evacuatingNeighborCount < panicContagionMinNeighbors || civilianNeighborCount == 0)
        {
            return;
        }

        float evacuatingRatio = evacuatingNeighborCount / (float)civilianNeighborCount;
        AddStress(stressGainFromPanicNeighbors * evacuatingRatio);
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
            desiredDirection += cohesion * cohesionWeight * (1f - stress * stressCohesionPenalty);
        }

        if (separationCount > 0)
        {
            desiredDirection += (separation / separationCount).normalized * separationWeight * (1f + stress * stressSeparationMultiplier);
        }
    }

    private void UpdateNeeds(float deltaTime, bool isThreatened)
    {
        bool isMoving = velocity.magnitude > maxSpeed * 0.35f;
        bool isSprinting = isThreatened || humanState == HumanState.Evacuating || soldierState == SoldierState.Withdraw || soldierTarget != null;

        if (isThreatened || soldierState == SoldierState.Withdraw)
        {
            AddStress(stressGainWhenFleeing * deltaTime);
        }

        if (isMoving)
        {
            float gain = isSprinting ? fatigueGainSprinting : fatigueGainMoving;
            fatigue = Mathf.Clamp01(fatigue + gain * deltaTime);
        }

        UpdateCivilianStressEvacuation();
    }

    private void UpdateCivilianStressEvacuation()
    {
        if (role != HumanRole.Civilian)
        {
            return;
        }

        if (humanState == HumanState.Calm && stress >= stressEvacuationThreshold)
        {
            BeginEvacuation();
        }
    }

    private void AddStress(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        stress = Mathf.Clamp01(stress + amount);
    }

    private void AddStressFromVisiblePredator(PredatorAgent predator)
    {
        if (predator == null)
        {
            return;
        }

        float distance = FlattenVector(predator.transform.position - transform.position).magnitude;
        AddStressFromPredatorDistance(distance, soldierThreatRadius);
    }

    private void AddStressFromPredatorDistance(float distance, float radius)
    {
        float proximity = 1f - Mathf.Clamp01(distance / Mathf.Max(0.1f, radius));
        AddStress(stressGainNearPredator * proximity * Time.deltaTime);
    }

    private float GetEffectiveSpeed(float baseSpeed)
    {
        float fatiguePenalty = fatigue * fatigueSpeedPenalty;
        return Mathf.Max(0.1f, baseSpeed * (1f - fatiguePenalty));
    }

    private float GetEffectiveAcceleration(float baseAcceleration)
    {
        float fatiguePenalty = fatigue * fatigueAccelerationPenalty;
        return Mathf.Max(0.1f, baseAcceleration * (1f - fatiguePenalty));
    }

    private float GetEffectiveEvacuationPathMultiplier()
    {
        return evacuationPathWeightMultiplier * (1f + stress * stressEvacuationPathMultiplier);
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

        if (humanState == HumanState.Evacuating)
        {
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

    private void EnsureEvacuationTarget()
    {
        if (humanState != HumanState.Evacuating || simulationManager == null)
        {
            return;
        }

        if (HasActiveEvacuationTarget)
        {
            return;
        }

        simulationManager.TryGetEvacuationTargetNode(transform.position, out evacuationTargetNodeIndex);
    }

    private void UpdateEvacuationTargetState()
    {
        if (!HasActiveEvacuationTarget)
        {
            EnsureEvacuationTarget();
            return;
        }

        Vector3 targetPosition = navigation.Nodes[evacuationTargetNodeIndex].Position;
        if (FlattenVector(targetPosition - transform.position).sqrMagnitude > evacuationArrivalRadius * evacuationArrivalRadius)
        {
            return;
        }

        currentNodeIndex = evacuationTargetNodeIndex;
        if (simulationManager != null && simulationManager.IsEvacuationExitNode(currentNodeIndex))
        {
            Escape();
            return;
        }

        if (simulationManager != null && simulationManager.TryGetNextEvacuationNode(currentNodeIndex, out int nextNodeIndex))
        {
            evacuationTargetNodeIndex = nextNodeIndex;
            return;
        }

        evacuationTargetNodeIndex = -1;
    }

    private void TryEscapeIfArrived()
    {
        if (humanState != HumanState.Evacuating || navigation == null)
        {
            return;
        }

        UpdateEvacuationTargetState();
    }

    private void Escape()
    {
        if (isDead || humanState == HumanState.Escaped)
        {
            return;
        }

        humanState = HumanState.Escaped;
        isDead = true;

        if (simulationManager != null)
        {
            simulationManager.RecordHumanEscaped(this);
        }

        Destroy(gameObject);
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

        Gizmos.color = Color.Lerp(new Color(0.2f, 0.9f, 0.55f, 0.35f), new Color(1f, 0.1f, 0.05f, 0.45f), stress);
        Gizmos.DrawWireSphere(transform.position, Mathf.Lerp(0.8f, 2.4f, stress));
    }

}
