using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(SimulationManager))]
public sealed class SimulationSetupController : MonoBehaviour
{
    [SerializeField] private SimulationManager simulationManager;
    [SerializeField] private EvacuationFlowFieldVisualizer flowFieldVisualizer;
    [SerializeField] private CityNavigationVisualizer navigationVisualizer;
    [SerializeField] private bool showSetupOnStart = true;
    [SerializeField] private int defaultCivilianCount = 120;
    [SerializeField] private int defaultSoldierCount = 50;
    [SerializeField] private int defaultTitanCount = 3;
    [SerializeField] private int defaultBatchRunCount = 1;
    [SerializeField] private Vector2 defenseButtonSize = new Vector2(118f, 28f);
    [SerializeField] private float defenseButtonSpacing = 8f;
    [SerializeField] private float defenseRowTopMargin = 18f;
    [SerializeField] private Vector2 visualizationButtonSize = new Vector2(240f, 28f);
    [SerializeField] private float flowFieldButtonTopMargin = 12f;
    [SerializeField] private float topBarStackSpacing = 10f;

    private string civilianCountText = "120";
    private string soldierCountText = "50";
    private string titanCountText = "3";
    private string batchRunCountText = "1";
    private SimulationDefenseMode selectedDefenseMode = SimulationDefenseMode.RallyDefense;
    private Vector2 batchResultsScrollPosition;
    private bool showNavigationGraph;

    private void Awake()
    {
        EnsureReferences();
        EnsureNavigationVisualizer();

        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            defaultCivilianCount = 40;
            defaultSoldierCount = 12;
            defaultTitanCount = 2;
            defaultBatchRunCount = 1;
        }

        civilianCountText = Mathf.Max(0, defaultCivilianCount).ToString();
        soldierCountText = Mathf.Max(0, defaultSoldierCount).ToString();
        titanCountText = Mathf.Max(0, defaultTitanCount).ToString();
        batchRunCountText = Mathf.Max(1, defaultBatchRunCount).ToString();

        if (simulationManager != null)
        {
            BindSimulationManager(simulationManager);
        }
    }

    public void BindSimulationManager(SimulationManager manager)
    {
        if (manager == null)
        {
            return;
        }

        simulationManager = manager;
        if (showSetupOnStart)
        {
            simulationManager.EnableSetupFlow();
        }
    }

    private void OnGUI()
    {
        EnsureReferences();

        if (simulationManager == null)
        {
            return;
        }

        int previousDepth = GUI.depth;
        GUI.depth = -50;

        SimulationGuiStyles.Ensure();

        Rect safeArea = Screen.safeArea;
        float topBarY = DrawTopBar(safeArea);

        if (simulationManager.Phase == SimulationManager.SimulationPhase.AwaitingStart)
        {
            DrawSetupPanel(topBarY);
        }
        else if (simulationManager.Phase == SimulationManager.SimulationPhase.Complete)
        {
            DrawResultsPanel();
        }

        GUI.depth = previousDepth;
    }

    private float DrawTopBar(Rect safeArea)
    {
        float y = GetTopBarStartY(safeArea);

        if (simulationManager.Phase == SimulationManager.SimulationPhase.AwaitingStart)
        {
            y = DrawDefenseModeButtonRow(safeArea, y, true);
            y += topBarStackSpacing;
            y = DrawNavigationGraphButtonRow(safeArea, y);
            y += topBarStackSpacing;
            y = DrawFlowFieldButtonRow(safeArea, y);
            y += topBarStackSpacing;
            y = DrawEnterSandboxButtonRow(safeArea, y);
            return y;
        }

        y = DrawDefenseModeButtonRow(safeArea, y, CanChangeDefenseMode());
        y += topBarStackSpacing;
        y = DrawNavigationGraphButtonRow(safeArea, y);
        y += topBarStackSpacing;
        y = DrawFlowFieldButtonRow(safeArea, y);

        if (simulationManager.IsSandboxMode && simulationManager.Phase == SimulationManager.SimulationPhase.Running)
        {
            y += topBarStackSpacing;
            y = DrawSandboxBanner(safeArea, y);
        }

        if (simulationManager.Phase == SimulationManager.SimulationPhase.Running
            && simulationManager.IsBatchModeActive)
        {
            y += topBarStackSpacing;
            DrawBatchProgressPanelAtY(safeArea, y);
            y += 72f;
        }

        return y;
    }

    private float DrawEnterSandboxButtonRow(Rect safeArea, float y)
    {
        const float width = 300f;
        const float height = 34f;
        float x = safeArea.xMin + (safeArea.width - width) * 0.5f;

        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.55f, 0.9f, 0.45f, 1f);
        if (GUI.Button(new Rect(x, y, width, height), "Enter Sandbox", SimulationGuiStyles.PanelButton))
        {
            TryStartSandbox();
        }

        GUI.backgroundColor = previousBackground;
        return y + height;
    }

    private bool CanChangeDefenseMode()
    {
        return simulationManager.Phase != SimulationManager.SimulationPhase.Running;
    }

    private float DrawSandboxBanner(Rect safeArea, float y)
    {
        const float height = 58f;
        float width = Mathf.Min(520f, safeArea.width - 24f);
        Rect bannerRect = new Rect(
            safeArea.xMin + (safeArea.width - width) * 0.5f,
            y,
            width,
            height);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.Box(bannerRect, GUIContent.none);
        GUI.color = previousColor;

        const float horizontalPadding = 14f;
        float contentWidth = bannerRect.width - horizontalPadding * 2f;

        GUI.Label(
            new Rect(bannerRect.x + horizontalPadding, bannerRect.y + 7f, contentWidth, 22f),
            "Sandbox Mode",
            SimulationGuiStyles.ResultsLabel);
        GUI.Label(
            new Rect(bannerRect.x + horizontalPadding, bannerRect.y + 30f, contentWidth, 22f),
            "Free play with no results screen. Use Spawn Predator to add titans.",
            SimulationGuiStyles.BannerLabel);
        return y + height;
    }

    private SimulationDefenseMode GetActiveDefenseMode()
    {
        if (simulationManager.Phase == SimulationManager.SimulationPhase.Running)
        {
            return simulationManager.DefenseMode;
        }

        return selectedDefenseMode;
    }

    private float DrawDefenseModeButtonRow(Rect layoutRegion, float y, bool interactive)
    {
        string[] labels = { "No Soldiers", "Naive Defense", "Rally Defense" };
        SimulationDefenseMode[] modes =
        {
            SimulationDefenseMode.NoSoldiers,
            SimulationDefenseMode.NaiveDefense,
            SimulationDefenseMode.RallyDefense
        };

        SimulationDefenseMode activeMode = GetActiveDefenseMode();
        float totalWidth = labels.Length * defenseButtonSize.x + (labels.Length - 1) * defenseButtonSpacing;
        float startX = layoutRegion.xMin + (layoutRegion.width - totalWidth) * 0.5f;

        GUI.enabled = interactive;
        for (int i = 0; i < labels.Length; i++)
        {
            Rect buttonRect = new Rect(
                startX + i * (defenseButtonSize.x + defenseButtonSpacing),
                y,
                defenseButtonSize.x,
                defenseButtonSize.y);

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = activeMode == modes[i]
                ? new Color(0.4f, 1f, 0.6f, 1f)
                : new Color(0.85f, 0.85f, 0.85f, 0.9f);

            if (GUI.Button(buttonRect, labels[i], SimulationGuiStyles.ToggleButton))
            {
                SetDefenseMode(modes[i]);
            }

            GUI.backgroundColor = previousBackground;
        }

        GUI.enabled = true;
        return y + defenseButtonSize.y;
    }

    private float DrawNavigationGraphButtonRow(Rect safeArea, float y)
    {
        EnsureNavigationVisualizer();

        return DrawVisualizationToggleRow(
            safeArea,
            y,
            "Navigation Graph",
            showNavigationGraph,
            () =>
            {
                showNavigationGraph = !showNavigationGraph;
                EnsureNavigationVisualizer();
                if (navigationVisualizer != null)
                {
                    navigationVisualizer.ShowNavigationGraph = showNavigationGraph;
                }
            });
    }

    private float DrawFlowFieldButtonRow(Rect safeArea, float y)
    {
        return DrawVisualizationToggleRow(
            safeArea,
            y,
            "Evacuation Flow Field",
            flowFieldVisualizer != null && flowFieldVisualizer.ShowFlowField,
            () =>
            {
                if (flowFieldVisualizer != null)
                {
                    flowFieldVisualizer.ShowFlowField = !flowFieldVisualizer.ShowFlowField;
                }
            });
    }

    private float DrawVisualizationToggleRow(Rect safeArea, float y, string label, bool isActive, System.Action onToggle)
    {
        float x = Mathf.Max(safeArea.xMin + 8f, safeArea.xMin + (safeArea.width - visualizationButtonSize.x) * 0.5f);
        string buttonLabel = isActive ? $"{label}: On" : $"{label}: Off";

        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = isActive
            ? new Color(0.4f, 1f, 0.6f, 1f)
            : new Color(0.85f, 0.85f, 0.85f, 0.9f);

        if (GUI.Button(new Rect(x, y, visualizationButtonSize.x, visualizationButtonSize.y), buttonLabel, SimulationGuiStyles.ToggleButton))
        {
            onToggle?.Invoke();
        }

        GUI.backgroundColor = previousBackground;
        return y + visualizationButtonSize.y;
    }

    private void DrawBatchProgressPanelAtY(Rect safeArea, float y)
    {
        const float width = 390f;
        const float height = 72f;
        Rect panelRect = new Rect(
            safeArea.xMin + (safeArea.width - width) * 0.5f,
            y,
            width,
            height);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.Box(panelRect, GUIContent.none);
        GUI.color = previousColor;

        int currentRun = Mathf.Clamp(simulationManager.BatchCompletedRuns + 1, 1, simulationManager.BatchTotalRuns);
        const float horizontalPadding = 16f;
        const float titleHeight = 24f;
        const float subtitleHeight = 20f;
        float contentWidth = panelRect.width - horizontalPadding * 2f;
        float contentY = panelRect.y + 12f;

        GUI.Label(
            new Rect(panelRect.x + horizontalPadding, contentY, contentWidth, titleHeight),
            $"Batch Run {currentRun} / {simulationManager.BatchTotalRuns}",
            SimulationGuiStyles.PanelTitle);
        contentY += titleHeight + 4f;
        GUI.Label(
            new Rect(panelRect.x + horizontalPadding, contentY, contentWidth, subtitleHeight),
            "Running automated trials with the same configuration.",
            SimulationGuiStyles.PanelLabel);
    }

    private static float GetTopBarStartY(Rect safeArea)
    {
        // Game view chrome can clip IMGUI placed flush against safeArea.yMin.
        return safeArea.yMin + 12f;
    }

    private void DrawSetupPanel(float topBarBottomY)
    {
        Rect safeArea = Screen.safeArea;
        const float width = 400f;
        const float height = 388f;
        float panelTop = topBarBottomY + 16f;
        float maxBottom = safeArea.yMax - 12f;
        if (panelTop + height > maxBottom)
        {
            panelTop = Mathf.Max(topBarBottomY + 8f, maxBottom - height);
        }

        Rect panelRect = new Rect(
            safeArea.xMin + (safeArea.width - width) * 0.5f,
            panelTop,
            width,
            height);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.Box(panelRect, GUIContent.none);
        GUI.color = previousColor;

        float x = panelRect.x + 24f;
        float y = panelRect.y + 20f;
        float fieldWidth = panelRect.width - 48f;
        float rowHeight = 28f;

        GUI.Label(new Rect(x, y, fieldWidth, 24f), "Simulation Setup", SimulationGuiStyles.PanelTitle);
        y += 30f;
        GUI.Label(new Rect(x, y, fieldWidth, 18f), "Defense Mode", SimulationGuiStyles.ResultsLabel);
        y += 22f;

        const float descriptionHeight = 44f;
        Color previousBoxColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.08f);
        GUI.Box(new Rect(x, y, fieldWidth, descriptionHeight), GUIContent.none);
        GUI.color = previousBoxColor;
        GUI.Label(
            new Rect(x + 10f, y + 6f, fieldWidth - 20f, descriptionHeight - 12f),
            GetDefenseModeDescription(selectedDefenseMode),
            SimulationGuiStyles.DefenseDescriptionLabel);
        y += descriptionHeight + 10f;

        GUI.Label(new Rect(x, y, 140f, rowHeight), "Civilians", SimulationGuiStyles.PanelLabel);
        civilianCountText = GUI.TextField(new Rect(x + 150f, y, fieldWidth - 150f, rowHeight), civilianCountText);
        y += rowHeight + 6f;

        bool soldiersLocked = selectedDefenseMode == SimulationDefenseMode.NoSoldiers;
        GUI.enabled = !soldiersLocked;
        GUI.Label(new Rect(x, y, 140f, rowHeight), "Soldiers", SimulationGuiStyles.PanelLabel);
        soldierCountText = GUI.TextField(new Rect(x + 150f, y, fieldWidth - 150f, rowHeight), soldiersLocked ? "0" : soldierCountText);
        y += rowHeight + 6f;
        GUI.enabled = true;

        GUI.Label(new Rect(x, y, 140f, rowHeight), "Titans", SimulationGuiStyles.PanelLabel);
        titanCountText = GUI.TextField(new Rect(x + 150f, y, fieldWidth - 150f, rowHeight), titanCountText);
        y += rowHeight + 6f;

        GUI.Label(new Rect(x, y, 140f, rowHeight), "Batch Runs", SimulationGuiStyles.PanelLabel);
        batchRunCountText = GUI.TextField(new Rect(x + 150f, y, fieldWidth - 150f, rowHeight), batchRunCountText);
        y += rowHeight + 16f;

        int batchRuns = ParseBatchCount(batchRunCountText, defaultBatchRunCount);
        string startLabel = batchRuns > 1 ? $"Start Batch ({batchRuns} runs)" : "Start Simulation";

        Rect startRect = new Rect(x, y, fieldWidth, 34f);
        GUI.backgroundColor = new Color(0.35f, 0.75f, 1f, 1f);
        if (GUI.Button(startRect, startLabel, SimulationGuiStyles.PanelButton))
        {
            TryStartSimulation(batchRuns);
        }

        GUI.backgroundColor = Color.white;
    }

    private void TryStartSandbox()
    {
        int civilians = ParseCount(civilianCountText, defaultCivilianCount);
        int soldiers = selectedDefenseMode == SimulationDefenseMode.NoSoldiers
            ? 0
            : ParseCount(soldierCountText, defaultSoldierCount);
        simulationManager.StartSandbox(selectedDefenseMode, civilians, soldiers);
    }

    private void DrawResultsPanel()
    {
        bool isBatch = simulationManager.BatchResults.Count > 1;
        Rect safeArea = Screen.safeArea;
        float width = isBatch ? 500f : 430f;
        float height = isBatch ? 420f : 250f;
        Rect panelRect = new Rect(
            safeArea.xMin + (safeArea.width - width) * 0.5f,
            safeArea.yMin + (safeArea.height - height) * 0.5f,
            width,
            height);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.Box(panelRect, GUIContent.none);
        GUI.color = previousColor;

        float x = panelRect.x + 24f;
        float y = panelRect.y + 20f;
        float fieldWidth = panelRect.width - 48f;
        float rowHeight = 26f;

        string title = isBatch ? "Batch Simulation Complete" : "Simulation Complete";
        GUI.Label(new Rect(x, y, fieldWidth, 28f), title, SimulationGuiStyles.ResultsTitle);
        y += 34f;
        GUI.Label(new Rect(x, y, fieldWidth, rowHeight), $"Config: {FormatDefenseMode(simulationManager.DefenseMode)}", SimulationGuiStyles.ResultsLabel);
        y += rowHeight;

        if (isBatch)
        {
            string averageSoldierLabel = simulationManager.BatchHasSoldierSurvival
                ? $"{simulationManager.BatchAverageSoldierSurvival:F1}%"
                : "N/A";
            GUI.Label(new Rect(x, y, fieldWidth, rowHeight), $"Average Civilian Survival: {simulationManager.BatchAverageCivilianSurvival:F1}%", SimulationGuiStyles.ResultsLabel);
            y += rowHeight;
            GUI.Label(new Rect(x, y, fieldWidth, rowHeight), $"Average Soldier Survival: {averageSoldierLabel}", SimulationGuiStyles.ResultsLabel);
            y += rowHeight;
            GUI.Label(new Rect(x, y, fieldWidth, rowHeight), $"Average Titans Defeated: {simulationManager.BatchAverageTitansDefeated:F1}", SimulationGuiStyles.ResultsLabel);
            y += rowHeight + 8f;

            Rect scrollRect = new Rect(x, y, fieldWidth, 170f);
            Rect contentRect = new Rect(0f, 0f, fieldWidth - 24f, simulationManager.BatchResults.Count * 24f + 8f);
            batchResultsScrollPosition = GUI.BeginScrollView(scrollRect, batchResultsScrollPosition, contentRect);

            float contentY = 4f;
            for (int i = 0; i < simulationManager.BatchResults.Count; i++)
            {
                SimulationRunResult result = simulationManager.BatchResults[i];
                string soldierLabel = result.HasSoldierSurvival
                    ? $"{result.SoldierSurvivalPercent:F1}%"
                    : "N/A";
                GUI.Label(
                    new Rect(0f, contentY, contentRect.width, 22f),
                    $"Run {result.RunIndex}: civilian {result.CivilianSurvivalPercent:F1}%, soldier {soldierLabel}, titans {result.TitansDefeated}",
                    SimulationGuiStyles.PanelLabel);
                contentY += 24f;
            }

            GUI.EndScrollView();
            y = scrollRect.yMax + 12f;
        }
        else
        {
            SimulationRunResult result = simulationManager.BatchResults.Count > 0
                ? simulationManager.BatchResults[0]
                : default;
            string soldierLabel = result.HasSoldierSurvival
                ? $"{result.SoldierSurvivalPercent:F1}%"
                : "N/A";

            GUI.Label(new Rect(x, y, fieldWidth, rowHeight), $"Civilian Survival: {result.CivilianSurvivalPercent:F1}%", SimulationGuiStyles.ResultsLabel);
            y += rowHeight;
            GUI.Label(new Rect(x, y, fieldWidth, rowHeight), $"Soldier Survival: {soldierLabel}", SimulationGuiStyles.ResultsLabel);
            y += rowHeight;
            GUI.Label(new Rect(x, y, fieldWidth, rowHeight), $"Titans Defeated: {result.TitansDefeated}", SimulationGuiStyles.ResultsLabel);
            y += rowHeight + 10f;
        }

        Rect restartRect = new Rect(x, y, fieldWidth, 34f);
        GUI.backgroundColor = new Color(0.4f, 1f, 0.6f, 1f);
        if (GUI.Button(restartRect, "New Simulation", SimulationGuiStyles.PanelButton))
        {
            selectedDefenseMode = simulationManager.DefenseMode;
            simulationManager.ResetToSetup();
        }

        GUI.backgroundColor = Color.white;
    }

    private void SetDefenseMode(SimulationDefenseMode mode)
    {
        selectedDefenseMode = mode;
        if (mode == SimulationDefenseMode.NoSoldiers)
        {
            soldierCountText = "0";
        }
        else if (soldierCountText == "0")
        {
            soldierCountText = Mathf.Max(0, defaultSoldierCount).ToString();
        }
    }

    private void TryStartSimulation(int batchRuns)
    {
        int civilians = ParseCount(civilianCountText, defaultCivilianCount);
        int soldiers = selectedDefenseMode == SimulationDefenseMode.NoSoldiers
            ? 0
            : ParseCount(soldierCountText, defaultSoldierCount);
        int titans = ParseCount(titanCountText, defaultTitanCount);
        simulationManager.StartSimulation(selectedDefenseMode, civilians, soldiers, titans, batchRuns);
    }

    private static int ParseCount(string text, int fallback)
    {
        return int.TryParse(text, out int value) ? Mathf.Max(0, value) : Mathf.Max(0, fallback);
    }

    private static int ParseBatchCount(string text, int fallback)
    {
        return int.TryParse(text, out int value) ? Mathf.Max(1, value) : Mathf.Max(1, fallback);
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

    private static string GetDefenseModeDescription(SimulationDefenseMode mode)
    {
        switch (mode)
        {
            case SimulationDefenseMode.NoSoldiers:
                return "No defending soldiers spawn. Civilians evacuate on their own.";
            case SimulationDefenseMode.NaiveDefense:
                return "Soldiers engage titans immediately without coordinated rallying.";
            default:
                return "Soldiers rally and engage titans cooperatively before attacking.";
        }
    }

    private void EnsureReferences()
    {
        if (simulationManager == null)
        {
            simulationManager = GetComponent<SimulationManager>();
        }

        if (simulationManager == null)
        {
            simulationManager = FindFirstObjectByType<SimulationManager>();
        }

        if (flowFieldVisualizer == null)
        {
            flowFieldVisualizer = GetComponent<EvacuationFlowFieldVisualizer>();
        }

        if (flowFieldVisualizer == null)
        {
            flowFieldVisualizer = FindFirstObjectByType<EvacuationFlowFieldVisualizer>();
        }

        if (navigationVisualizer == null)
        {
            navigationVisualizer = GetComponent<CityNavigationVisualizer>();
        }

        if (navigationVisualizer == null)
        {
            navigationVisualizer = FindFirstObjectByType<CityNavigationVisualizer>();
        }
    }

    private void EnsureNavigationVisualizer()
    {
        EnsureReferences();

        if (navigationVisualizer != null)
        {
            navigationVisualizer.ShowNavigationGraph = showNavigationGraph;
            return;
        }

        if (simulationManager == null)
        {
            return;
        }

        navigationVisualizer = simulationManager.GetComponent<CityNavigationVisualizer>();
        if (navigationVisualizer == null)
        {
            navigationVisualizer = simulationManager.gameObject.AddComponent<CityNavigationVisualizer>();
        }

        navigationVisualizer.ShowNavigationGraph = showNavigationGraph;
    }
}

