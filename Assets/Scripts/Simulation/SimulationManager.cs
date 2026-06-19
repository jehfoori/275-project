using System.Collections.Generic;
using UnityEngine;

public sealed class SimulationManager : MonoBehaviour
{
    [SerializeField] private SimulationBounds bounds;
    [SerializeField] private PreyAgent preyPrefab;
    [SerializeField] private PreyAgent soldierPrefab;
    [SerializeField] private Transform waypointRoot;
    [SerializeField] private Transform obstacleRoot;
    [SerializeField] private Transform evacuationRoot;
    [SerializeField] private Transform agentParent;
    [SerializeField] private int startingPreyCount = 30;
    [SerializeField] private int startingSoldierCount = 50;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private float flockingCellSize = 6f;
    [SerializeField] private float agentClearance = 2.1f;
    [SerializeField] private float maxWaypointLinkDistance = 95f;
    [SerializeField] private float linkSampleSpacing = 2.5f;
    [Header("Soldiers")]
    [SerializeField] private int soldierWithdrawCivilianThreshold = 0;
    [SerializeField] private float soldierWithdrawCivilianRatio = 0.05f;
    [Header("HUD")]
    [SerializeField] private bool showStatsHud = true;
    [SerializeField] private Vector2 statsHudOffset = new Vector2(18f, 18f);
    [SerializeField] private float statsHudWidth = 230f;

    private readonly List<PreyAgent> preyAgents = new List<PreyAgent>();
    private readonly List<PredatorAgent> predatorAgents = new List<PredatorAgent>();
    private readonly Dictionary<Vector2Int, List<PreyAgent>> preyCells = new Dictionary<Vector2Int, List<PreyAgent>>();
    private readonly Dictionary<Vector2Int, List<PredatorAgent>> predatorCells = new Dictionary<Vector2Int, List<PredatorAgent>>();
    private readonly List<Transform> evacuationPoints = new List<Transform>();
    private readonly List<int> evacuationExitNodes = new List<int>();
    private readonly CityNavigation navigation = new CityNavigation();
    private float[] evacuationCosts = new float[0];
    private int[] evacuationNextNodes = new int[0];
    private bool[] evacuationVisited = new bool[0];
    private int preySpatialIndexFrame = -1;
    private int predatorSpatialIndexFrame = -1;
    private int totalHumanCount;
    private int totalCivilianCount;
    private int totalSoldierCount;
    private int humanCasualtyCount;
    private int civilianCasualtyCount;
    private int soldierCasualtyCount;
    private int humanEscapedCount;
    private int titanDefeatedCount;
    private float simulationStartTime;
    private GUIStyle hudBoxStyle;
    private GUIStyle hudLabelStyle;
    private GUIStyle hudTitleStyle;
    private bool warnedMissingEvacuationFlow;

    public IReadOnlyList<PreyAgent> PreyAgents => preyAgents;
    public IReadOnlyList<PredatorAgent> PredatorAgents => predatorAgents;
    public SimulationBounds Bounds => bounds;
    public CityNavigation Navigation => navigation;
    public int TotalHumanCount => totalHumanCount;
    public int ActiveCivilianCount => CountActiveCivilians();
    public int HumanCasualtyCount => humanCasualtyCount;
    public int CivilianCasualtyCount => civilianCasualtyCount;
    public int SoldierCasualtyCount => soldierCasualtyCount;
    public int HumanEscapedCount => humanEscapedCount;
    public int TitanDefeatedCount => titanDefeatedCount;
    public float AverageCivilianStress => GetAverageStress(PreyAgent.HumanRole.Civilian);
    public float AverageSoldierStress => GetAverageStress(PreyAgent.HumanRole.Soldier);
    public float ElapsedTime => Mathf.Max(0f, Time.time - simulationStartTime);
    public bool ShouldSoldiersWithdraw
    {
        get
        {
            if (totalCivilianCount <= 0)
            {
                return false;
            }

            int withdrawThreshold = Mathf.Max(
                soldierWithdrawCivilianThreshold,
                Mathf.FloorToInt(totalCivilianCount * soldierWithdrawCivilianRatio));
            return CountActiveCivilians() <= withdrawThreshold;
        }
    }

    private void OnValidate()
    {
        startingPreyCount = Mathf.Max(0, startingPreyCount);
        startingSoldierCount = Mathf.Max(0, startingSoldierCount);
        flockingCellSize = Mathf.Max(0.5f, flockingCellSize);
        agentClearance = Mathf.Max(0.1f, agentClearance);
        maxWaypointLinkDistance = Mathf.Max(1f, maxWaypointLinkDistance);
        linkSampleSpacing = Mathf.Max(0.5f, linkSampleSpacing);
        soldierWithdrawCivilianThreshold = Mathf.Max(0, soldierWithdrawCivilianThreshold);
        soldierWithdrawCivilianRatio = Mathf.Clamp01(soldierWithdrawCivilianRatio);
        statsHudWidth = Mathf.Max(160f, statsHudWidth);
    }

    public void ConfigureForWebGl(int preyCount, int soldierCount)
    {
        startingPreyCount = Mathf.Max(0, preyCount);
        startingSoldierCount = Mathf.Max(0, soldierCount);
    }

    private void Awake()
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            ConfigureForWebGl(40, 12);
        }

        simulationStartTime = Time.time;
        BuildNavigation();
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnInitialHumans();
        }
    }

    [ContextMenu("Rebuild City Navigation")]
    private void BuildNavigation()
    {
        EnsureSceneReferences();
        navigation.Build(waypointRoot, obstacleRoot, agentClearance, maxWaypointLinkDistance, linkSampleSpacing);
        CacheEvacuationPoints();
        BuildEvacuationFlowField();
    }

    [ContextMenu("Spawn Initial Humans")]
    private void SpawnInitialHumans()
    {
        if (bounds == null || preyPrefab == null)
        {
            Debug.LogWarning("SimulationManager needs bounds and a prey prefab before it can spawn humans.", this);
            return;
        }

        BuildNavigation();
        Transform parent = agentParent != null ? agentParent : transform;

        for (int i = 0; i < startingPreyCount; i++)
        {
            SpawnHuman(preyPrefab, PreyAgent.HumanRole.Civilian, parent);
        }

        PreyAgent activeSoldierPrefab = soldierPrefab != null ? soldierPrefab : preyPrefab;
        for (int i = 0; i < startingSoldierCount; i++)
        {
            SpawnHuman(activeSoldierPrefab, PreyAgent.HumanRole.Soldier, parent);
        }
    }

    private void SpawnHuman(PreyAgent prefab, PreyAgent.HumanRole role, Transform parent)
    {
        Vector3 spawnPosition = GetSpawnPosition();
        Quaternion rotation = Quaternion.LookRotation(bounds.RandomGroundDirection(), Vector3.up);
        PreyAgent human = Instantiate(prefab, spawnPosition, rotation, parent);
        human.SetRole(role);
        RegisterPrey(human);
        human.Initialize(bounds, this, navigation);
    }

    public void RegisterPrey(PreyAgent prey)
    {
        if (prey != null && !preyAgents.Contains(prey))
        {
            preyAgents.Add(prey);
            totalHumanCount++;
            if (prey.Role == PreyAgent.HumanRole.Civilian)
            {
                totalCivilianCount++;
            }
            else if (prey.Role == PreyAgent.HumanRole.Soldier)
            {
                totalSoldierCount++;
            }

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

    public void RecordHumanCasualty(PreyAgent prey)
    {
        if (prey == null || !preyAgents.Contains(prey))
        {
            return;
        }

        humanCasualtyCount++;
        if (prey.Role == PreyAgent.HumanRole.Civilian)
        {
            civilianCasualtyCount++;
        }
        else if (prey.Role == PreyAgent.HumanRole.Soldier)
        {
            soldierCasualtyCount++;
        }

        UnregisterPrey(prey);
    }

    public void RecordHumanEscaped(PreyAgent prey)
    {
        if (prey == null || !preyAgents.Contains(prey))
        {
            return;
        }

        humanEscapedCount++;
        UnregisterPrey(prey);
    }

    public bool TryGetEvacuationTargetNode(Vector3 position, out int targetNodeIndex)
    {
        targetNodeIndex = -1;

        if (!HasEvacuationFlowField)
        {
            WarnMissingEvacuationFlow();
            return false;
        }

        float bestScore = float.PositiveInfinity;
        IReadOnlyList<CityNavigation.Node> nodes = navigation.Nodes;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (!HasFiniteEvacuationCost(i))
            {
                continue;
            }

            float distance = FlattenVector(nodes[i].Position - position).magnitude;
            float score = distance + evacuationCosts[i];
            if (score < bestScore)
            {
                targetNodeIndex = i;
                bestScore = score;
            }
        }

        return targetNodeIndex >= 0;
    }

    public bool TryGetNextEvacuationNode(int currentNodeIndex, out int nextNodeIndex)
    {
        nextNodeIndex = -1;

        if (!HasEvacuationFlowField || currentNodeIndex < 0 || currentNodeIndex >= evacuationNextNodes.Length)
        {
            return false;
        }

        nextNodeIndex = evacuationNextNodes[currentNodeIndex];
        return nextNodeIndex >= 0 && nextNodeIndex < navigation.Nodes.Count;
    }

    public bool IsEvacuationExitNode(int nodeIndex)
    {
        return HasEvacuationFlowField
            && nodeIndex >= 0
            && nodeIndex < evacuationNextNodes.Length
            && evacuationNextNodes[nodeIndex] == nodeIndex
            && Mathf.Approximately(evacuationCosts[nodeIndex], 0f);
    }

    public void RecordTitanDefeated(PredatorAgent predator)
    {
        if (predator == null || !predatorAgents.Contains(predator))
        {
            return;
        }

        titanDefeatedCount++;
        UnregisterPredator(predator);
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

    private int CountActiveCivilians()
    {
        int count = 0;

        for (int i = 0; i < preyAgents.Count; i++)
        {
            PreyAgent prey = preyAgents[i];
            if (prey != null && prey.Role == PreyAgent.HumanRole.Civilian)
            {
                count++;
            }
        }

        return count;
    }

    private float GetAverageStress(PreyAgent.HumanRole role)
    {
        float totalStress = 0f;
        int count = 0;

        for (int i = 0; i < preyAgents.Count; i++)
        {
            PreyAgent prey = preyAgents[i];
            if (prey == null || prey.Role != role)
            {
                continue;
            }

            totalStress += prey.Stress;
            count++;
        }

        return count > 0 ? totalStress / count : 0f;
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

        if (evacuationRoot == null)
        {
            GameObject evacuationObject = GameObject.Find("EvacuationPoints");
            evacuationRoot = evacuationObject != null ? evacuationObject.transform : null;
        }
    }

    private void CacheEvacuationPoints()
    {
        evacuationPoints.Clear();
        warnedMissingEvacuationFlow = false;

        if (evacuationRoot == null)
        {
            return;
        }

        for (int i = 0; i < evacuationRoot.childCount; i++)
        {
            Transform child = evacuationRoot.GetChild(i);
            if (child.gameObject.activeInHierarchy)
            {
                evacuationPoints.Add(child);
            }
        }
    }

    private void BuildEvacuationFlowField()
    {
        EnsureEvacuationFlowCapacity();
        evacuationExitNodes.Clear();

        int nodeCount = navigation.Nodes.Count;
        for (int i = 0; i < nodeCount; i++)
        {
            evacuationCosts[i] = float.PositiveInfinity;
            evacuationNextNodes[i] = -1;
            evacuationVisited[i] = false;
        }

        if (!navigation.HasWaypoints || evacuationPoints.Count == 0)
        {
            return;
        }

        for (int i = 0; i < evacuationPoints.Count; i++)
        {
            Transform point = evacuationPoints[i];
            if (point == null || !point.gameObject.activeInHierarchy)
            {
                continue;
            }

            int exitNodeIndex = navigation.GetNearestNodeIndex(point.position);
            if (exitNodeIndex < 0 || evacuationExitNodes.Contains(exitNodeIndex))
            {
                continue;
            }

            evacuationExitNodes.Add(exitNodeIndex);
            evacuationCosts[exitNodeIndex] = 0f;
            evacuationNextNodes[exitNodeIndex] = exitNodeIndex;
        }

        for (int i = 0; i < nodeCount; i++)
        {
            int currentNodeIndex = GetUnvisitedEvacuationNodeWithLowestCost();
            if (currentNodeIndex < 0)
            {
                break;
            }

            evacuationVisited[currentNodeIndex] = true;
            CityNavigation.Node currentNode = navigation.Nodes[currentNodeIndex];
            List<int> neighbors = currentNode.Neighbors;

            for (int neighborIndex = 0; neighborIndex < neighbors.Count; neighborIndex++)
            {
                int neighborNodeIndex = neighbors[neighborIndex];
                if (neighborNodeIndex < 0 || neighborNodeIndex >= nodeCount || evacuationVisited[neighborNodeIndex])
                {
                    continue;
                }

                float edgeCost = FlattenVector(navigation.Nodes[neighborNodeIndex].Position - currentNode.Position).magnitude;
                float candidateCost = evacuationCosts[currentNodeIndex] + edgeCost;
                if (candidateCost >= evacuationCosts[neighborNodeIndex])
                {
                    continue;
                }

                evacuationCosts[neighborNodeIndex] = candidateCost;
                evacuationNextNodes[neighborNodeIndex] = currentNodeIndex;
            }
        }
    }

    private void EnsureEvacuationFlowCapacity()
    {
        int nodeCount = navigation.Nodes.Count;
        if (evacuationCosts.Length == nodeCount)
        {
            return;
        }

        evacuationCosts = new float[nodeCount];
        evacuationNextNodes = new int[nodeCount];
        evacuationVisited = new bool[nodeCount];
    }

    private int GetUnvisitedEvacuationNodeWithLowestCost()
    {
        int bestIndex = -1;
        float bestCost = float.PositiveInfinity;

        for (int i = 0; i < evacuationCosts.Length; i++)
        {
            if (evacuationVisited[i] || evacuationCosts[i] >= bestCost)
            {
                continue;
            }

            bestIndex = i;
            bestCost = evacuationCosts[i];
        }

        return bestIndex;
    }

    private bool HasEvacuationFlowField => evacuationCosts.Length == navigation.Nodes.Count
        && evacuationNextNodes.Length == navigation.Nodes.Count
        && evacuationExitNodes.Count > 0;

    private bool HasFiniteEvacuationCost(int nodeIndex)
    {
        return nodeIndex >= 0
            && nodeIndex < evacuationCosts.Length
            && !float.IsInfinity(evacuationCosts[nodeIndex])
            && evacuationNextNodes[nodeIndex] >= 0;
    }

    private void WarnMissingEvacuationFlow()
    {
        if (warnedMissingEvacuationFlow)
        {
            return;
        }

        if (evacuationPoints.Count == 0)
        {
            Debug.LogWarning("SimulationManager could not find any active children under EvacuationPoints.", this);
        }
        else
        {
            Debug.LogWarning("SimulationManager could not build an evacuation flow field from the current waypoint graph.", this);
        }

        warnedMissingEvacuationFlow = true;
    }

    private Vector3 FlattenVector(Vector3 vector)
    {
        return bounds != null ? bounds.ProjectVectorToGround(vector) : new Vector3(vector.x, 0f, vector.z);
    }

    private void OnGUI()
    {
        if (!showStatsHud)
        {
            return;
        }

        EnsureHudStyles();

        const float lineHeight = 21f;
        const float padding = 12f;
        float height = padding * 2f + 8f * lineHeight;
        Rect safeArea = Screen.safeArea;
        Rect hudRect = new Rect(
            safeArea.xMax - statsHudWidth - statsHudOffset.x,
            safeArea.yMin + statsHudOffset.y,
            statsHudWidth,
            height);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.68f);
        GUI.Box(hudRect, GUIContent.none, hudBoxStyle);
        GUI.color = previousColor;

        Rect lineRect = new Rect(hudRect.x + padding, hudRect.y + padding, hudRect.width - padding * 2f, lineHeight);
        GUI.Label(lineRect, "Simulation", hudTitleStyle);

        lineRect.y += lineHeight;
        GUI.Label(lineRect, $"Civilian Casualties: {civilianCasualtyCount} / {totalCivilianCount}", hudLabelStyle);

        lineRect.y += lineHeight;
        GUI.Label(lineRect, $"Soldier Casualties: {soldierCasualtyCount} / {totalSoldierCount}", hudLabelStyle);

        lineRect.y += lineHeight;
        GUI.Label(lineRect, $"Humans Escaped: {humanEscapedCount} / {totalHumanCount}", hudLabelStyle);

        lineRect.y += lineHeight;
        GUI.Label(lineRect, $"Titans Defeated: {titanDefeatedCount}", hudLabelStyle);

        lineRect.y += lineHeight;
        GUI.Label(lineRect, $"Elapsed Time: {FormatElapsedTime(ElapsedTime)}", hudLabelStyle);

        lineRect.y += lineHeight;
        GUI.Label(lineRect, $"Avg Civilian Stress: {AverageCivilianStress:P0}", hudLabelStyle);

        lineRect.y += lineHeight;
        GUI.Label(lineRect, $"Avg Soldier Stress: {AverageSoldierStress:P0}", hudLabelStyle);
    }

    private void EnsureHudStyles()
    {
        if (hudBoxStyle != null && hudLabelStyle != null && hudTitleStyle != null)
        {
            return;
        }

        Texture2D backgroundTexture = Texture2D.whiteTexture;
        hudBoxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(0, 0, 0, 0)
        };
        hudBoxStyle.normal.background = backgroundTexture;
        hudBoxStyle.normal.textColor = Color.white;

        hudLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip
        };
        hudLabelStyle.normal.textColor = Color.white;

        hudTitleStyle = new GUIStyle(hudLabelStyle)
        {
            fontStyle = FontStyle.Bold
        };
    }

    private static string FormatElapsedTime(float elapsedSeconds)
    {
        int totalSeconds = Mathf.FloorToInt(elapsedSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
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
