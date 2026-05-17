using System.Collections.Generic;
using UnityEngine;

public sealed class SimulationManager : MonoBehaviour
{
    [SerializeField] private SimulationBounds bounds;
    [SerializeField] private PreyAgent preyPrefab;
    [SerializeField] private Transform waypointRoot;
    [SerializeField] private Transform obstacleRoot;
    [SerializeField] private Transform agentParent;
    [SerializeField] private int startingPreyCount = 30;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private float flockingCellSize = 6f;
    [SerializeField] private float agentClearance = 2.1f;
    [SerializeField] private float maxWaypointLinkDistance = 95f;
    [SerializeField] private float linkSampleSpacing = 2.5f;

    private readonly List<PreyAgent> preyAgents = new List<PreyAgent>();
    private readonly List<PredatorAgent> predatorAgents = new List<PredatorAgent>();
    private readonly Dictionary<Vector2Int, List<PreyAgent>> preyCells = new Dictionary<Vector2Int, List<PreyAgent>>();
    private readonly Dictionary<Vector2Int, List<PredatorAgent>> predatorCells = new Dictionary<Vector2Int, List<PredatorAgent>>();
    private readonly CityNavigation navigation = new CityNavigation();
    private int preySpatialIndexFrame = -1;
    private int predatorSpatialIndexFrame = -1;

    public IReadOnlyList<PreyAgent> PreyAgents => preyAgents;
    public IReadOnlyList<PredatorAgent> PredatorAgents => predatorAgents;
    public SimulationBounds Bounds => bounds;
    public CityNavigation Navigation => navigation;

    private void OnValidate()
    {
        startingPreyCount = Mathf.Max(0, startingPreyCount);
        flockingCellSize = Mathf.Max(0.5f, flockingCellSize);
        agentClearance = Mathf.Max(0.1f, agentClearance);
        maxWaypointLinkDistance = Mathf.Max(1f, maxWaypointLinkDistance);
        linkSampleSpacing = Mathf.Max(0.5f, linkSampleSpacing);
    }

    private void Awake()
    {
        BuildNavigation();
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnInitialPrey();
        }
    }

    [ContextMenu("Rebuild City Navigation")]
    private void BuildNavigation()
    {
        EnsureSceneReferences();
        navigation.Build(waypointRoot, obstacleRoot, agentClearance, maxWaypointLinkDistance, linkSampleSpacing);
    }

    [ContextMenu("Spawn Initial Prey")]
    private void SpawnInitialPrey()
    {
        if (bounds == null || preyPrefab == null)
        {
            Debug.LogWarning("SimulationManager needs bounds and a prey prefab before it can spawn agents.", this);
            return;
        }

        BuildNavigation();
        Transform parent = agentParent != null ? agentParent : transform;

        for (int i = 0; i < startingPreyCount; i++)
        {
            Vector3 spawnPosition = GetSpawnPosition();
            Quaternion rotation = Quaternion.LookRotation(bounds.RandomGroundDirection(), Vector3.up);
            PreyAgent prey = Instantiate(preyPrefab, spawnPosition, rotation, parent);
            RegisterPrey(prey);
            prey.Initialize(bounds, this, navigation);
        }
    }

    public void RegisterPrey(PreyAgent prey)
    {
        if (prey != null && !preyAgents.Contains(prey))
        {
            preyAgents.Add(prey);
            preySpatialIndexFrame = -1;
        }
    }

    public void UnregisterPrey(PreyAgent prey)
    {
        preyAgents.Remove(prey);
        preySpatialIndexFrame = -1;
    }

    public void RegisterPredator(PredatorAgent predator)
    {
        if (predator != null && !predatorAgents.Contains(predator))
        {
            predatorAgents.Add(predator);
            predatorSpatialIndexFrame = -1;
        }
    }

    public void UnregisterPredator(PredatorAgent predator)
    {
        predatorAgents.Remove(predator);
        predatorSpatialIndexFrame = -1;
    }

    public void GetNearbyPrey(Vector3 position, float radius, List<PreyAgent> results)
    {
        results.Clear();

        if (radius <= 0f || preyAgents.Count == 0)
        {
            return;
        }

        EnsurePreySpatialIndex();

        int cellRadius = Mathf.CeilToInt(radius / flockingCellSize);
        Vector2Int centerCell = GetFlockingCell(position);

        for (int x = centerCell.x - cellRadius; x <= centerCell.x + cellRadius; x++)
        {
            for (int y = centerCell.y - cellRadius; y <= centerCell.y + cellRadius; y++)
            {
                if (!preyCells.TryGetValue(new Vector2Int(x, y), out List<PreyAgent> cellAgents))
                {
                    continue;
                }

                results.AddRange(cellAgents);
            }
        }
    }

    public void GetNearbyPredators(Vector3 position, float radius, List<PredatorAgent> results)
    {
        results.Clear();

        if (radius <= 0f || predatorAgents.Count == 0)
        {
            return;
        }

        EnsurePredatorSpatialIndex();

        int cellRadius = Mathf.CeilToInt(radius / flockingCellSize);
        Vector2Int centerCell = GetFlockingCell(position);

        for (int x = centerCell.x - cellRadius; x <= centerCell.x + cellRadius; x++)
        {
            for (int y = centerCell.y - cellRadius; y <= centerCell.y + cellRadius; y++)
            {
                if (!predatorCells.TryGetValue(new Vector2Int(x, y), out List<PredatorAgent> cellAgents))
                {
                    continue;
                }

                results.AddRange(cellAgents);
            }
        }
    }

    private Vector3 GetSpawnPosition()
    {
        if (navigation.TryGetRandomWalkablePoint(agentClearance, out Vector3 waypointSpawn))
        {
            return bounds.ProjectPointToGround(waypointSpawn);
        }

        for (int i = 0; i < 64; i++)
        {
            Vector3 candidate = bounds.RandomGroundPointInside();
            if (!navigation.IsPointBlocked(candidate, agentClearance))
            {
                return candidate;
            }
        }

        return bounds.RandomGroundPointInside();
    }

    private void EnsurePreySpatialIndex()
    {
        if (preySpatialIndexFrame == Time.frameCount)
        {
            return;
        }

        preyCells.Clear();

        for (int i = 0; i < preyAgents.Count; i++)
        {
            PreyAgent prey = preyAgents[i];
            if (prey == null)
            {
                continue;
            }

            Vector2Int cell = GetFlockingCell(prey.transform.position);
            if (!preyCells.TryGetValue(cell, out List<PreyAgent> cellAgents))
            {
                cellAgents = new List<PreyAgent>();
                preyCells.Add(cell, cellAgents);
            }

            cellAgents.Add(prey);
        }

        preySpatialIndexFrame = Time.frameCount;
    }

    private void EnsurePredatorSpatialIndex()
    {
        if (predatorSpatialIndexFrame == Time.frameCount)
        {
            return;
        }

        predatorCells.Clear();

        for (int i = 0; i < predatorAgents.Count; i++)
        {
            PredatorAgent predator = predatorAgents[i];
            if (predator == null)
            {
                continue;
            }

            Vector2Int cell = GetFlockingCell(predator.transform.position);
            if (!predatorCells.TryGetValue(cell, out List<PredatorAgent> cellAgents))
            {
                cellAgents = new List<PredatorAgent>();
                predatorCells.Add(cell, cellAgents);
            }

            cellAgents.Add(predator);
        }

        predatorSpatialIndexFrame = Time.frameCount;
    }

    private Vector2Int GetFlockingCell(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / flockingCellSize),
            Mathf.FloorToInt(position.z / flockingCellSize));
    }

    private void EnsureSceneReferences()
    {
        if (waypointRoot == null)
        {
            GameObject waypointObject = GameObject.Find("CrowdWaypoints");
            waypointRoot = waypointObject != null ? waypointObject.transform : null;
        }

        if (obstacleRoot == null)
        {
            GameObject cityObject = GameObject.Find("City");
            obstacleRoot = cityObject != null ? cityObject.transform : null;
        }
    }

    private void OnDrawGizmos()
    {
        EnsureSceneReferences();
        navigation.Build(waypointRoot, obstacleRoot, agentClearance, maxWaypointLinkDistance, linkSampleSpacing);

        DrawObstacleGizmos();
        DrawNavigationGizmos();
    }

    private void DrawNavigationGizmos()
    {
        IReadOnlyList<CityNavigation.Node> nodes = navigation.Nodes;
        IReadOnlyList<CityNavigation.Link> links = navigation.Links;

        for (int i = 0; i < links.Count; i++)
        {
            CityNavigation.Link link = links[i];
            if (link.From < 0 || link.From >= nodes.Count || link.To < 0 || link.To >= nodes.Count)
            {
                continue;
            }

            Gizmos.color = link.IsClear
                ? new Color(0.25f, 0.95f, 0.4f, 0.45f)
                : new Color(1f, 0.2f, 0.15f, 0.25f);
            Gizmos.DrawLine(nodes[link.From].Position + Vector3.up * 0.15f, nodes[link.To].Position + Vector3.up * 0.15f);
        }

        Gizmos.color = new Color(0.2f, 0.65f, 1f, 0.85f);
        for (int i = 0; i < nodes.Count; i++)
        {
            Gizmos.DrawWireSphere(nodes[i].Position + Vector3.up * 0.25f, agentClearance);
        }
    }

    private void DrawObstacleGizmos()
    {
        IReadOnlyList<BoxCollider> obstacles = navigation.Obstacles;
        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;

        Gizmos.color = new Color(1f, 0.45f, 0.2f, 0.2f);
        for (int i = 0; i < obstacles.Count; i++)
        {
            BoxCollider obstacle = obstacles[i];
            if (obstacle == null)
            {
                continue;
            }

            Gizmos.matrix = obstacle.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(obstacle.center, obstacle.size);
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
