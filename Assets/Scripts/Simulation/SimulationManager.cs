using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class SimulationManager : MonoBehaviour
{
    public enum SimulationPhase
    {
        AwaitingStart,
        Running,
        Complete
    }

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
    [Header("Run Limits")]
    [SerializeField] private float simulationTimeoutSeconds = 300f;

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
    private int civilianEscapedCount;
    private int soldierEscapedCount;
    private int titanDefeatedCount;
    private float simulationStartTime;
    private SimulationDefenseMode defenseMode = SimulationDefenseMode.RallyDefense;
    private SimulationPhase phase = SimulationPhase.AwaitingStart;
    private bool setupFlowEnabled;
    private PredatorSpawner predatorSpawner;
    private bool batchModeActive;
    private bool sandboxMode;
    private int initialTitanCountForRun;
    private int batchTotalRuns;
    private int batchCompletedRuns;
    private int batchCivilianCount;
    private int batchSoldierCount;
    private int batchTitanCount;
    private readonly List<SimulationRunResult> batchResults = new List<SimulationRunResult>();
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
    public int CivilianEscapedCount => civilianEscapedCount;
    public int SoldierEscapedCount => soldierEscapedCount;
    public int TitanDefeatedCount => titanDefeatedCount;
    public SimulationDefenseMode DefenseMode => defenseMode;
    public SimulationPhase Phase => phase;
    public bool UsesSetupFlow => setupFlowEnabled;
    public bool IsSandboxMode => sandboxMode;
    public bool IsBatchModeActive => batchModeActive;
    public int BatchTotalRuns => batchTotalRuns;
    public int BatchCompletedRuns => batchCompletedRuns;
    public IReadOnlyList<SimulationRunResult> BatchResults => batchResults;
    public float BatchAverageCivilianSurvival => GetBatchAverageCivilianSurvival();
    public float BatchAverageSoldierSurvival => GetBatchAverageSoldierSurvival();
    public bool BatchHasSoldierSurvival => batchResults.Exists(result => result.HasSoldierSurvival);
    public float BatchAverageTitansDefeated => GetBatchAverageTitansDefeated();
    public float CivilianSurvivalPercent => totalCivilianCount > 0
        ? civilianEscapedCount / (float)totalCivilianCount * 100f
        : 0f;
    public float SoldierSurvivalPercent => totalSoldierCount > 0
        ? soldierEscapedCount / (float)totalSoldierCount * 100f
        : 0f;
    public string SoldierSurvivalPercentLabel => defenseMode == SimulationDefenseMode.NoSoldiers || totalSoldierCount <= 0
        ? "N/A"
        : $"{SoldierSurvivalPercent:F1}%";
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

    public bool AreAllTitansDefeated => !sandboxMode
        && initialTitanCountForRun > 0
        && predatorAgents.Count == 0;

    public bool HasActiveHumans => preyAgents.Count > 0;

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
        simulationTimeoutSeconds = Mathf.Max(0f, simulationTimeoutSeconds);
    }

    public void ConfigureForWebGl(int preyCount, int soldierCount)
    {
        startingPreyCount = Mathf.Max(0, preyCount);
        startingSoldierCount = Mathf.Max(0, soldierCount);
    }

    public void EnableSetupFlow()
    {
        setupFlowEnabled = true;
        spawnOnStart = false;
        phase = SimulationPhase.AwaitingStart;
        Debug.Log("Simulation setup flow enabled. Configure agents in the setup panel, then press Start.");
    }

    public void StartSimulation(SimulationDefenseMode mode, int civilians, int soldiers, int titans, int batchRuns = 1)
    {
        sandboxMode = false;
        batchRuns = Mathf.Max(1, batchRuns);
        batchModeActive = batchRuns > 1;
        batchTotalRuns = batchRuns;
        batchCompletedRuns = 0;
        batchResults.Clear();
        batchCivilianCount = Mathf.Max(0, civilians);
        batchSoldierCount = Mathf.Max(0, soldiers);
        batchTitanCount = Mathf.Max(0, titans);

        if (batchModeActive)
        {
            Debug.Log(
                $"Batch simulation started ({FormatDefenseMode(mode)}): "
                + $"{batchCivilianCount} civilians, {batchSoldierCount} soldiers, {batchTitanCount} titans, "
                + $"{batchTotalRuns} runs.");
        }

        StartSingleRun(mode, batchCivilianCount, batchSoldierCount, batchTitanCount);
    }

    public void StartSandbox(SimulationDefenseMode mode, int civilians, int soldiers)
    {
        sandboxMode = true;
        batchModeActive = false;
        batchTotalRuns = 0;
        batchCompletedRuns = 0;
        batchResults.Clear();

        StartSingleRun(mode, civilians, soldiers, 0);
        Debug.Log(
            $"Sandbox mode started ({FormatDefenseMode(defenseMode)}): "
            + $"{totalCivilianCount} civilians, {totalSoldierCount} soldiers. Use Spawn Predator to add titans.");
    }

    public void ResetToSetup()
    {
        ClearSimulation();
        sandboxMode = false;
        batchModeActive = false;
        batchTotalRuns = 0;
        batchCompletedRuns = 0;
        batchResults.Clear();
        phase = SimulationPhase.AwaitingStart;
    }

    public bool TryGetEvacuationFlowSegments(List<EvacuationFlowSegment> segments)
    {
        segments.Clear();

        if (!HasEvacuationFlowField)
        {
            return false;
        }

        IReadOnlyList<CityNavigation.Node> nodes = navigation.Nodes;
        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            if (!HasFiniteEvacuationCost(nodeIndex))
            {
                continue;
            }

            int nextNodeIndex = evacuationNextNodes[nodeIndex];
            if (nextNodeIndex < 0 || nextNodeIndex >= nodes.Count || nextNodeIndex == nodeIndex)
            {
                continue;
            }

            segments.Add(new EvacuationFlowSegment
            {
                From = nodes[nodeIndex].Position,
                To = nodes[nextNodeIndex].Position,
                Cost = evacuationCosts[nodeIndex]
            });
        }

        return segments.Count > 0;
    }

    public float NavigationNodeRadius => agentClearance;

    public bool TryGetNavigationGraphVisualization(
        List<NavigationLinkSegment> links,
        List<Vector3> nodes)
    {
        links.Clear();
        nodes.Clear();

        if (!navigation.HasWaypoints)
        {
            return false;
        }

        IReadOnlyList<CityNavigation.Node> navigationNodes = navigation.Nodes;
        IReadOnlyList<CityNavigation.Link> navigationLinks = navigation.Links;

        for (int i = 0; i < navigationNodes.Count; i++)
        {
            nodes.Add(navigationNodes[i].Position);
        }

        for (int i = 0; i < navigationLinks.Count; i++)
        {
            CityNavigation.Link link = navigationLinks[i];
            if (link.From < 0 || link.From >= navigationNodes.Count || link.To < 0 || link.To >= navigationNodes.Count)
            {
                continue;
            }

            links.Add(new NavigationLinkSegment
            {
                From = navigationNodes[link.From].Position,
                To = navigationNodes[link.To].Position,
                IsClear = link.IsClear
            });
        }

        return links.Count > 0 || nodes.Count > 0;
    }

    private void StartSingleRun(SimulationDefenseMode mode, int civilians, int soldiers, int titans)
    {
        ClearSimulation();
        defenseMode = mode;
        if (mode == SimulationDefenseMode.NoSoldiers)
        {
            soldiers = 0;
        }

        startingPreyCount = Mathf.Max(0, civilians);
        startingSoldierCount = Mathf.Max(0, soldiers);
        SpawnInitialHumans();
        SpawnInitialTitans(Mathf.Max(0, titans));

        phase = SimulationPhase.Running;
        simulationStartTime = Time.time;

        if (!batchModeActive)
        {
            Debug.Log(
                $"Simulation started ({FormatDefenseMode(defenseMode)}): "
                + $"{totalCivilianCount} civilians, {totalSoldierCount} soldiers, {titans} titans.");
        }
        else
        {
            Debug.Log(
                $"Batch run {batchCompletedRuns + 1}/{batchTotalRuns} started ({FormatDefenseMode(defenseMode)}).");
        }
    }

    private void Awake()
    {
        simulationStartTime = Time.time;
        BuildNavigation();
        EnsurePredatorSpawner();
        TryEnableSetupFlowFromScene();

        if (Application.platform == RuntimePlatform.WebGLPlayer && !setupFlowEnabled)
        {
            ConfigureForWebGl(40, 12);
        }
    }

    private void Start()
    {
        if (setupFlowEnabled)
        {
            return;
        }

        // Never auto-start when a setup controller exists; wait for explicit configuration.
        if (FindFirstObjectByType<SimulationSetupController>() != null)
        {
            Debug.LogWarning(
                "Simulation setup controller was found after startup. Enable the setup panel instead of auto-spawning.",
                this);
            return;
        }

        if (spawnOnStart)
        {
            StartLegacySimulation();
            return;
        }

        Debug.LogWarning(
            "SimulationManager has spawnOnStart disabled and no setup controller was found. "
            + "Starting a legacy run with the configured default population.",
            this);
        StartLegacySimulation();
    }

    private void TryEnableSetupFlowFromScene()
    {
        if (setupFlowEnabled)
        {
            return;
        }

        SimulationSetupController setupController = GetComponent<SimulationSetupController>();
        if (setupController == null)
        {
            setupController = FindFirstObjectByType<SimulationSetupController>();
        }

        if (setupController != null && setupController.isActiveAndEnabled)
        {
            setupController.BindSimulationManager(this);
        }
    }

    private void StartLegacySimulation()
    {
        defenseMode = SimulationDefenseMode.RallyDefense;
        SpawnInitialHumans();
        phase = SimulationPhase.Running;
        Debug.Log(
            $"Legacy simulation started ({FormatDefenseMode(defenseMode)}): "
            + $"{totalCivilianCount} civilians, {totalSoldierCount} soldiers.");
    }

    private void Update()
    {
        if (phase != SimulationPhase.Running || sandboxMode)
        {
            return;
        }

        if (simulationTimeoutSeconds > 0f && ElapsedTime >= simulationTimeoutSeconds)
        {
            CompleteRunAfterTimeout();
            return;
        }

        if (AreAllTitansDefeated && HasActiveHumans)
        {
            CompleteRunAfterTitanVictory();
            return;
        }

        if (preyAgents.Count == 0 && totalHumanCount > 0)
        {
            HandleSimulationComplete();
        }
    }

    private void CompleteRunAfterTitanVictory()
    {
        int survivorsMarked = MarkRemainingHumansAsEscaped();
        Debug.Log(
            $"All titans defeated. Marked {survivorsMarked} surviving humans as escaped and ending the run.");
        HandleSimulationComplete();
    }

    private void CompleteRunAfterTimeout()
    {
        int humanCasualties = ForceEliminateRemainingHumansAsCasualties();
        int titansEliminated = ForceEliminateRemainingTitans();
        Debug.Log(
            $"Simulation timed out after {FormatElapsedTime(ElapsedTime)}. "
            + $"Marked {humanCasualties} remaining humans as casualties and "
            + $"removed {titansEliminated} remaining titans (not counted as defeated).");
        HandleSimulationComplete();
    }

    private int ForceEliminateRemainingHumansAsCasualties()
    {
        int eliminatedCount = 0;
        List<PreyAgent> remainingHumans = new List<PreyAgent>(preyAgents);

        for (int i = 0; i < remainingHumans.Count; i++)
        {
            PreyAgent prey = remainingHumans[i];
            if (prey == null || !preyAgents.Contains(prey))
            {
                continue;
            }

            RecordHumanCasualty(prey);
            Destroy(prey.gameObject);
            eliminatedCount++;
        }

        return eliminatedCount;
    }

    private int ForceEliminateRemainingTitans()
    {
        int eliminatedCount = 0;
        List<PredatorAgent> remainingTitans = new List<PredatorAgent>(predatorAgents);

        for (int i = 0; i < remainingTitans.Count; i++)
        {
            PredatorAgent predator = remainingTitans[i];
            if (predator == null || !predatorAgents.Contains(predator))
            {
                continue;
            }

            UnregisterPredator(predator);
            Destroy(predator.gameObject);
            eliminatedCount++;
        }

        return eliminatedCount;
    }

    private int MarkRemainingHumansAsEscaped()
    {
        int markedCount = 0;
        List<PreyAgent> survivors = new List<PreyAgent>(preyAgents);

        for (int i = 0; i < survivors.Count; i++)
        {
            PreyAgent prey = survivors[i];
            if (prey == null || !preyAgents.Contains(prey))
            {
                continue;
            }

            humanEscapedCount++;
            if (prey.Role == PreyAgent.HumanRole.Civilian)
            {
                civilianEscapedCount++;
            }
            else if (prey.Role == PreyAgent.HumanRole.Soldier)
            {
                soldierEscapedCount++;
            }

            UnregisterPrey(prey);
            Destroy(prey.gameObject);
            markedCount++;
        }

        return markedCount;
    }

    private void HandleSimulationComplete()
    {
        SimulationRunResult result = CaptureCurrentRunResult();
        batchResults.Add(result);
        LogSingleRunResult(result);

        if (batchModeActive)
        {
            batchCompletedRuns++;
            if (batchCompletedRuns < batchTotalRuns)
            {
                StartSingleRun(defenseMode, batchCivilianCount, batchSoldierCount, batchTitanCount);
                return;
            }

            batchModeActive = false;
            LogBatchSummary();
        }

        phase = SimulationPhase.Complete;
    }

    private SimulationRunResult CaptureCurrentRunResult()
    {
        bool hasSoldierSurvival = defenseMode != SimulationDefenseMode.NoSoldiers && totalSoldierCount > 0;
        return new SimulationRunResult
        {
            RunIndex = batchResults.Count + 1,
            CivilianSurvivalPercent = CivilianSurvivalPercent,
            SoldierSurvivalPercent = hasSoldierSurvival ? SoldierSurvivalPercent : 0f,
            HasSoldierSurvival = hasSoldierSurvival,
            TitansDefeated = titanDefeatedCount
        };
    }

    private void LogSingleRunResult(SimulationRunResult result)
    {
        string soldierLabel = result.HasSoldierSurvival
            ? $"{result.SoldierSurvivalPercent:F1}%"
            : "N/A";

        if (batchModeActive)
        {
            Debug.Log(
                $"Batch run {result.RunIndex}/{batchTotalRuns} complete: "
                + $"civilian survival {result.CivilianSurvivalPercent:F1}%, "
                + $"soldier survival {soldierLabel}, "
                + $"titans defeated {result.TitansDefeated}.");
            return;
        }

        Debug.Log(
            $"Simulation complete: civilian survival {result.CivilianSurvivalPercent:F1}%, "
            + $"soldier survival {soldierLabel}, "
            + $"titans defeated {result.TitansDefeated}.");
    }

    private void LogBatchSummary()
    {
        StringBuilder summary = new StringBuilder();
        summary.AppendLine(
            $"=== Batch simulation complete ({FormatDefenseMode(defenseMode)}, {batchTotalRuns} runs) ===");

        for (int i = 0; i < batchResults.Count; i++)
        {
            SimulationRunResult result = batchResults[i];
            string soldierLabel = result.HasSoldierSurvival
                ? $"{result.SoldierSurvivalPercent:F1}%"
                : "N/A";
            summary.AppendLine(
                $"Run {result.RunIndex}: civilian survival {result.CivilianSurvivalPercent:F1}%, "
                + $"soldier survival {soldierLabel}, titans defeated {result.TitansDefeated}");
        }

        string averageSoldierLabel = BatchHasSoldierSurvival
            ? $"{BatchAverageSoldierSurvival:F1}%"
            : "N/A";
        summary.Append(
            $"Averages: civilian survival {BatchAverageCivilianSurvival:F1}%, "
            + $"soldier survival {averageSoldierLabel}, "
            + $"titans defeated {BatchAverageTitansDefeated:F1}");

        Debug.Log(summary.ToString());
    }

    private float GetBatchAverageCivilianSurvival()
    {
        if (batchResults.Count == 0)
        {
            return 0f;
        }

        float total = 0f;
        for (int i = 0; i < batchResults.Count; i++)
        {
            total += batchResults[i].CivilianSurvivalPercent;
        }

        return total / batchResults.Count;
    }

    private float GetBatchAverageSoldierSurvival()
    {
        float total = 0f;
        int count = 0;

        for (int i = 0; i < batchResults.Count; i++)
        {
            SimulationRunResult result = batchResults[i];
            if (!result.HasSoldierSurvival)
            {
                continue;
            }

            total += result.SoldierSurvivalPercent;
            count++;
        }

        return count > 0 ? total / count : 0f;
    }

    private float GetBatchAverageTitansDefeated()
    {
        if (batchResults.Count == 0)
        {
            return 0f;
        }

        float total = 0f;
        for (int i = 0; i < batchResults.Count; i++)
        {
            total += batchResults[i].TitansDefeated;
        }

        return total / batchResults.Count;
    }

    [ContextMenu("Rebuild City Navigation")]
    private void BuildNavigation()
    {
        EnsureSceneReferences();
        navigation.Build(waypointRoot, obstacleRoot, agentClearance, maxWaypointLinkDistance, linkSampleSpacing);
        CacheEvacuationPoints();
        BuildEvacuationFlowField();
    }

    private void ClearSimulation()
    {
        for (int i = preyAgents.Count - 1; i >= 0; i--)
        {
            PreyAgent prey = preyAgents[i];
            if (prey != null)
            {
                Destroy(prey.gameObject);
            }
        }

        preyAgents.Clear();

        for (int i = predatorAgents.Count - 1; i >= 0; i--)
        {
            PredatorAgent predator = predatorAgents[i];
            if (predator != null)
            {
                Destroy(predator.gameObject);
            }
        }

        predatorAgents.Clear();

        totalHumanCount = 0;
        totalCivilianCount = 0;
        totalSoldierCount = 0;
        humanCasualtyCount = 0;
        civilianCasualtyCount = 0;
        soldierCasualtyCount = 0;
        humanEscapedCount = 0;
        civilianEscapedCount = 0;
        soldierEscapedCount = 0;
        titanDefeatedCount = 0;
        initialTitanCountForRun = 0;
        preySpatialIndexFrame = -1;
        predatorSpatialIndexFrame = -1;
    }

    private void SpawnInitialTitans(int titanCount)
    {
        initialTitanCountForRun = 0;

        if (titanCount <= 0)
        {
            return;
        }

        EnsurePredatorSpawner();
        if (predatorSpawner == null)
        {
            Debug.LogWarning("SimulationManager could not find PredatorSpawner to spawn titans.", this);
            return;
        }

        initialTitanCountForRun = predatorSpawner.SpawnTitans(titanCount);
    }

    private void EnsurePredatorSpawner()
    {
        if (predatorSpawner == null)
        {
            predatorSpawner = GetComponent<PredatorSpawner>();
        }

        if (predatorSpawner == null)
        {
            predatorSpawner = FindFirstObjectByType<PredatorSpawner>();
        }
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
        if (prey.Role == PreyAgent.HumanRole.Civilian)
        {
            civilianEscapedCount++;
        }
        else if (prey.Role == PreyAgent.HumanRole.Soldier)
        {
            soldierEscapedCount++;
        }

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
        if (!showStatsHud || phase != SimulationPhase.Running)
        {
            return;
        }

        EnsureHudStyles();

        const float lineHeight = 21f;
        const float padding = 12f;
        float height = padding * 2f + 9f * lineHeight;
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
        GUI.Label(lineRect, sandboxMode ? "Sandbox" : "Simulation", hudTitleStyle);

        lineRect.y += lineHeight;
        GUI.Label(lineRect, $"Defense: {FormatDefenseMode(defenseMode)}", hudLabelStyle);

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

    private static string FormatDefenseMode(SimulationDefenseMode mode)
    {
        switch (mode)
        {
            case SimulationDefenseMode.NoSoldiers:
                return "No Soldiers";
            case SimulationDefenseMode.NaiveDefense:
                return "Naive Defense";
            default:
                return "Rally Defense";
        }
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
