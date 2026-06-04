using System.Collections.Generic;
using UnityEngine;

public sealed class CameraViewController : MonoBehaviour
{
    [SerializeField] private Transform viewRoot;
    [SerializeField] private float transitionDuration = 0.45f;
    [SerializeField] private bool snapToFirstViewOnStart = true;
    [SerializeField] private bool showViewSelector = true;
    [SerializeField] private Vector2 arrowButtonSize = new Vector2(42f, 34f);
    [SerializeField] private Vector2 labelSize = new Vector2(260f, 34f);
    [SerializeField] private float selectorSpacing = 8f;
    [SerializeField] private float bottomMargin = 36f;

    private readonly List<Transform> views = new List<Transform>();
    private Vector3 transitionStartPosition;
    private Vector3 transitionEndPosition;
    private Quaternion transitionStartRotation;
    private Quaternion transitionEndRotation;
    private float transitionStartTime;
    private int currentViewIndex = -1;
    private bool isTransitioning;
    private bool hasSnappedToInitialView;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;

    private SimulationManager simulationManager;
    private class TitanTracker
    {
        public PredatorAgent agent;
        public Transform viewTransform;
    }
    private readonly List<TitanTracker> activeTrackers = new List<TitanTracker>();
    private int titanCounter = 0;

    private enum FollowMode
    {
        UnfollowAll,
        FollowTitan,
        FollowSoldier,
        FollowCitizen,
        HighlightCamera
    }
    private FollowMode currentFollowMode = FollowMode.FollowTitan;
    private PreyAgent trackedPreyAgent;
    private Transform preyFollowView;

    private enum HighlightType
    {
        TitanConsume = 0,    // Lowest priority
        SoldierFight = 1,    // Medium priority
        CitizenEscape = 2   // Highest priority
    }

    private class HighlightEvent
    {
        public HighlightType Type;
        public MonoBehaviour Actor;        // PreyAgent or PredatorAgent
        public MonoBehaviour Target;       // PreyAgent or PredatorAgent
    }

    private enum HighlightState
    {
        Idle,
        Active
    }

    private HighlightState currentHighlightState = HighlightState.Idle;
    private HighlightEvent activeHighlightEvent = null;
    private Transform highlightFollowView;
    private readonly List<HighlightEvent> highlightEvents = new List<HighlightEvent>();

    private void Awake()
    {
        RefreshViews();
        SnapToInitialViewIfNeeded();
    }

    private void OnEnable()
    {
        RefreshViews();
        SnapToInitialViewIfNeeded();
    }

    private void Start()
    {
        RefreshViews();
        SnapToInitialViewIfNeeded();
    }

    private void Update()
    {
        if (views.Count == 0)
        {
            RefreshViews();
            SnapToInitialViewIfNeeded();
        }

        UpdateTitanTracking();
        UpdatePreyTracking();
        UpdateHighlightCamera();
        UpdateTransition();
    }

    public void SetView(int index)
    {
        if (index < 0 || index >= views.Count || views[index] == null)
        {
            return;
        }

        currentViewIndex = index;
        transitionStartPosition = transform.position;
        transitionStartRotation = transform.rotation;
        transitionEndPosition = views[index].position;
        transitionEndRotation = views[index].rotation;
        transitionStartTime = Time.time;
        isTransitioning = transitionDuration > 0.001f;

        if (!isTransitioning)
        {
            SnapToView(index);
        }
    }

    public void NextView()
    {
        if (views.Count == 0)
        {
            return;
        }

        int nextIndex = currentViewIndex < 0 ? 0 : (currentViewIndex + 1) % views.Count;
        SetView(nextIndex);
    }

    public void PreviousView()
    {
        if (views.Count == 0)
        {
            return;
        }

        int nextIndex = currentViewIndex < 0 ? 0 : (currentViewIndex - 1 + views.Count) % views.Count;
        SetView(nextIndex);
    }

    [ContextMenu("Refresh Camera Views")]
    private void RefreshViews()
    {
        Transform previouslyViewedTransform = currentViewIndex >= 0 && currentViewIndex < views.Count ? views[currentViewIndex] : null;

        if (viewRoot == null)
        {
            GameObject rootObject = GameObject.Find("CameraViews");
            viewRoot = rootObject != null ? rootObject.transform : null;
        }

        views.Clear();
        if (viewRoot == null)
        {
            currentViewIndex = -1;
            return;
        }

        for (int i = 0; i < viewRoot.childCount; i++)
        {
            Transform child = viewRoot.GetChild(i);
            if (child.gameObject.activeInHierarchy)
            {
                views.Add(child);
            }
        }

        // Remap currentViewIndex to the new index of the previously viewed transform
        if (previouslyViewedTransform != null)
        {
            int newIndex = views.IndexOf(previouslyViewedTransform);
            if (newIndex >= 0)
            {
                currentViewIndex = newIndex;
            }
        }
    }

    private void UpdateTransition()
    {
        if (!isTransitioning)
        {
            return;
        }

        float progress = Mathf.Clamp01((Time.time - transitionStartTime) / transitionDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

        Vector3 endPos = transitionEndPosition;
        Quaternion endRot = transitionEndRotation;

        if (currentViewIndex >= 0 && currentViewIndex < views.Count)
        {
            Transform currentView = views[currentViewIndex];
            if (currentView != null && (currentView.name.StartsWith("View_Titan_") || 
                                        currentView.name == "View_Soldier_Follow" || 
                                        currentView.name == "View_Citizen_Follow" ||
                                        currentView.name == "View_Highlight_Follow"))
            {
                endPos = currentView.position;
                endRot = currentView.rotation;
            }
        }

        transform.position = Vector3.Lerp(transitionStartPosition, endPos, easedProgress);
        transform.rotation = Quaternion.Slerp(transitionStartRotation, endRot, easedProgress);

        if (progress >= 1f)
        {
            isTransitioning = false;
        }
    }

    private void SnapToView(int index)
    {
        if (index < 0 || index >= views.Count || views[index] == null)
        {
            return;
        }

        currentViewIndex = index;
        transform.SetPositionAndRotation(views[index].position, views[index].rotation);
        isTransitioning = false;
    }

    private void OnGUI()
    {
        if (views.Count == 0)
        {
            RefreshViews();
        }

        HandleKeyboardEvent(Event.current);

        if (!showViewSelector || views.Count == 0)
        {
            return;
        }

        EnsureStyles();

        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        Color previousBackgroundColor = GUI.backgroundColor;
        GUI.depth = -100;
        GUI.color = Color.white;

        Rect safeArea = Screen.safeArea;
        float totalWidth = arrowButtonSize.x * 2f + labelSize.x + selectorSpacing * 2f;
        float startX = Mathf.Max(safeArea.xMin + 8f, safeArea.xMin + (safeArea.width - totalWidth) * 0.5f);
        float y = Mathf.Clamp(
            safeArea.yMax - labelSize.y - bottomMargin,
            safeArea.yMin + 8f,
            safeArea.yMax - labelSize.y - 8f);

        Rect previousRect = new Rect(startX, y, arrowButtonSize.x, arrowButtonSize.y);
        Rect labelRect = new Rect(previousRect.xMax + selectorSpacing, y, labelSize.x, labelSize.y);
        Rect nextRect = new Rect(labelRect.xMax + selectorSpacing, y, arrowButtonSize.x, arrowButtonSize.y);

        // Draw Mutually Exclusive Switch Buttons above the selector
        float switchButtonWidth = 105f;
        float switchButtonHeight = 28f;
        float totalSwitchWidth = switchButtonWidth * 5f + selectorSpacing * 4f;
        float switchStartX = Mathf.Max(safeArea.xMin + 8f, safeArea.xMin + (safeArea.width - totalSwitchWidth) * 0.5f);
        float switchY = y - switchButtonHeight - 8f;

        string[] modes = { "Follow Soldier", "Follow Citizen", "Follow Titan", "Highlight Camera", "Unfollow All" };
        FollowMode[] modeValues = { FollowMode.FollowSoldier, FollowMode.FollowCitizen, FollowMode.FollowTitan, FollowMode.HighlightCamera, FollowMode.UnfollowAll };

        for (int i = 0; i < modes.Length; i++)
        {
            Rect btnRect = new Rect(switchStartX + i * (switchButtonWidth + selectorSpacing), switchY, switchButtonWidth, switchButtonHeight);
            
            if (currentFollowMode == modeValues[i])
            {
                GUI.backgroundColor = new Color(0.4f, 1f, 0.6f, 1f); 
            }
            else
            {
                GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            }

            if (GUI.Button(btnRect, modes[i], buttonStyle))
            {
                SetFollowMode(modeValues[i]);
            }
        }

        GUI.backgroundColor = new Color(0.9f, 0.92f, 0.95f, 1f);
        if (GUI.Button(previousRect, "<", buttonStyle))
        {
            PreviousView();
        }

        GUI.backgroundColor = new Color(0.08f, 0.1f, 0.12f, 0.88f);
        GUI.Box(labelRect, FormatViewLabel(currentViewIndex, CurrentView), labelStyle);

        GUI.backgroundColor = new Color(0.9f, 0.92f, 0.95f, 1f);
        if (GUI.Button(nextRect, ">", buttonStyle))
        {
            NextView();
        }

        GUI.depth = previousDepth;
        GUI.color = previousColor;
        GUI.backgroundColor = previousBackgroundColor;
    }

    private void HandleKeyboardEvent(Event currentEvent)
    {
        if (currentEvent == null || currentEvent.type != EventType.KeyDown || views.Count == 0)
        {
            return;
        }

        if (currentEvent.keyCode == KeyCode.RightArrow || currentEvent.keyCode == KeyCode.E)
        {
            NextView();
            currentEvent.Use();
            return;
        }

        if (currentEvent.keyCode == KeyCode.LeftArrow || currentEvent.keyCode == KeyCode.Q)
        {
            PreviousView();
            currentEvent.Use();
        }
    }

    private static string FormatViewLabel(int index, Transform view)
    {
        string label = view != null ? view.name : "View";
        if (label.StartsWith("View_"))
        {
            label = label.Substring(5);
        }

        label = label.Replace('_', ' ');
        return index >= 0 ? $"View {index + 1}: {label}" : label;
    }

    private Transform CurrentView => currentViewIndex >= 0 && currentViewIndex < views.Count
        ? views[currentViewIndex]
        : null;

    private void SnapToInitialViewIfNeeded()
    {
        if (hasSnappedToInitialView || !snapToFirstViewOnStart || views.Count == 0)
        {
            return;
        }

        SnapToView(0);
        hasSnappedToInitialView = true;
    }

    private void EnsureStyles()
    {
        if (buttonStyle != null && labelStyle != null)
        {
            return;
        }

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip
        };
        buttonStyle.normal.textColor = Color.black;
        buttonStyle.hover.textColor = Color.black;
        buttonStyle.active.textColor = Color.black;
        buttonStyle.focused.textColor = Color.black;

        labelStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip
        };
        labelStyle.normal.textColor = Color.white;
        labelStyle.hover.textColor = Color.white;
        labelStyle.active.textColor = Color.white;
        labelStyle.focused.textColor = Color.white;
    }

    private void LateUpdate()
    {
        UpdateTitanFollowViews();
        UpdatePreyFollowView();
        UpdateHighlightFollowViewPosition(false);

        if (!isTransitioning && currentViewIndex >= 0 && currentViewIndex < views.Count)
        {
            Transform currentView = views[currentViewIndex];
            if (currentView != null && (currentView.name.StartsWith("View_Titan_") || 
                                        currentView.name == "View_Soldier_Follow" || 
                                        currentView.name == "View_Citizen_Follow" ||
                                        currentView.name == "View_Highlight_Follow"))
            {
                transform.position = currentView.position;
                transform.rotation = currentView.rotation;
            }
        }
    }

    private Transform CreateTitanFollowView()
    {
        titanCounter++;
        if (viewRoot == null)
        {
            GameObject rootObject = GameObject.Find("CameraViews");
            viewRoot = rootObject != null ? rootObject.transform : null;
        }

        if (viewRoot != null)
        {
            GameObject followObj = new GameObject($"View_Titan_{titanCounter}");
            followObj.transform.SetParent(viewRoot, false);
            return followObj.transform;
        }
        return null;
    }

    private void UpdateTitanTracking()
    {
        if (simulationManager == null)
        {
            simulationManager = FindFirstObjectByType<SimulationManager>();
            if (simulationManager == null)
            {
                return;
            }
        }

        // Identify which Titan agent we are currently following before cleanup
        PredatorAgent currentlyFollowedTitan = null;
        if (currentViewIndex >= 0 && currentViewIndex < views.Count)
        {
            Transform currentView = views[currentViewIndex];
            if (currentView != null && currentView.name.StartsWith("View_Titan_"))
            {
                for (int j = 0; j < activeTrackers.Count; j++)
                {
                    if (activeTrackers[j].viewTransform == currentView)
                    {
                        currentlyFollowedTitan = activeTrackers[j].agent;
                        break;
                    }
                }
            }
        }

        bool needsRefresh = false;

        // 1. Clean up trackers for defeated or destroyed Titans
        for (int i = activeTrackers.Count - 1; i >= 0; i--)
        {
            var tracker = activeTrackers[i];
            if (tracker.agent == null || tracker.agent.IsDefeated)
            {
                if (tracker.viewTransform != null)
                {
                    tracker.viewTransform.gameObject.SetActive(false);
                    Destroy(tracker.viewTransform.gameObject);
                }
                activeTrackers.RemoveAt(i);
                needsRefresh = true;
            }
        }

        // 2. Add trackers for newly spawned Titans
        var predators = simulationManager.PredatorAgents;
        TitanTracker latestSpawnedTracker = null;

        for (int i = 0; i < predators.Count; i++)
        {
            PredatorAgent p = predators[i];
            if (p != null && !p.IsDefeated)
            {
                bool alreadyTracked = false;
                for (int j = 0; j < activeTrackers.Count; j++)
                {
                    if (activeTrackers[j].agent == p)
                    {
                        alreadyTracked = true;
                        break;
                    }
                }

                if (!alreadyTracked)
                {
                    Transform newFollowView = CreateTitanFollowView();
                    if (newFollowView != null)
                    {
                        TitanTracker newTracker = new TitanTracker
                        {
                            agent = p,
                            viewTransform = newFollowView
                        };
                        activeTrackers.Add(newTracker);
                        latestSpawnedTracker = newTracker;
                        needsRefresh = true;
                    }
                }
            }
        }

        // 3. Handle refresh and view updates if changes occurred
        if (needsRefresh)
        {
            RefreshViews();

            // Check if the followed Titan was defeated
            bool followedTitanWasDefeated = false;
            if (currentlyFollowedTitan != null)
            {
                if (currentlyFollowedTitan == null || currentlyFollowedTitan.IsDefeated)
                {
                    followedTitanWasDefeated = true;
                }
            }

            if (currentFollowMode == FollowMode.FollowTitan)
            {
                if (latestSpawnedTracker != null && latestSpawnedTracker.viewTransform != null)
                {
                    int index = views.IndexOf(latestSpawnedTracker.viewTransform);
                    if (index >= 0)
                    {
                        SetView(index);
                    }
                }
                else if (followedTitanWasDefeated)
                {
                    // Find the next active Titan to follow
                    TitanTracker nextTracker = null;
                    for (int j = 0; j < activeTrackers.Count; j++)
                    {
                        if (activeTrackers[j].agent != null && !activeTrackers[j].agent.IsDefeated && activeTrackers[j].viewTransform != null)
                        {
                            nextTracker = activeTrackers[j];
                            break;
                        }
                    }

                    if (nextTracker != null)
                    {
                        int index = views.IndexOf(nextTracker.viewTransform);
                        if (index >= 0)
                        {
                            SetView(index);
                        }
                        else
                        {
                            SetView(0);
                        }
                    }
                    else
                    {
                        // No other Titans left, switch to Unfollow All and reset view
                        SetFollowMode(FollowMode.UnfollowAll);
                        SetView(0);
                    }
                }
                else
                {
                    if (currentViewIndex >= views.Count)
                    {
                        SetView(0);
                    }
                }
            }
            else
            {
                if (followedTitanWasDefeated || currentViewIndex >= views.Count)
                {
                    SetView(0);
                }
            }
        }
    }

    private void UpdateTitanFollowViews()
    {
        for (int i = 0; i < activeTrackers.Count; i++)
        {
            var tracker = activeTrackers[i];
            if (tracker.agent != null && tracker.viewTransform != null)
            {
                Vector3 titanPos = tracker.agent.transform.position;
                Vector3 titanForward = tracker.agent.transform.forward;

                float followDistance = 24f;
                float followHeight = 16f;
                float lookAtOffset = 5f;

                Vector3 targetPosition = titanPos - titanForward * followDistance + Vector3.up * followHeight;
                Vector3 targetLookAt = titanPos + Vector3.up * lookAtOffset;

                Vector3 currentPos = tracker.viewTransform.position;
                Quaternion currentRot = tracker.viewTransform.rotation;

                if (currentPos.sqrMagnitude < 0.001f || Vector3.Distance(currentPos, targetPosition) > 100f)
                {
                    tracker.viewTransform.position = targetPosition;
                    tracker.viewTransform.rotation = Quaternion.LookRotation(targetLookAt - targetPosition);
                }
                else
                {
                    tracker.viewTransform.position = Vector3.Lerp(currentPos, targetPosition, Time.deltaTime * 6f);
                    tracker.viewTransform.rotation = Quaternion.Slerp(currentRot, Quaternion.LookRotation(targetLookAt - targetPosition), Time.deltaTime * 6f);
                }
            }
        }
    }

    private Vector3 GetTitanSpawnLocationReference()
    {
        GameObject spawnRootObject = GameObject.Find("PredatorSpawnPoints");
        if (spawnRootObject != null && spawnRootObject.transform.childCount > 0)
        {
            return spawnRootObject.transform.GetChild(0).position;
        }
        var spawner = FindFirstObjectByType<PredatorSpawner>();
        if (spawner != null)
        {
            return spawner.transform.position;
        }
        return Vector3.zero;
    }

    private void SetFollowMode(FollowMode newMode)
    {
        if (currentFollowMode == newMode)
        {
            return;
        }

        if (currentFollowMode == FollowMode.FollowSoldier || currentFollowMode == FollowMode.FollowCitizen)
        {
            CleanupPreyFollowView();
        }
        else if (currentFollowMode == FollowMode.HighlightCamera)
        {
            CleanupHighlightFollowView();
            activeHighlightEvent = null;
            currentHighlightState = HighlightState.Idle;
        }

        currentFollowMode = newMode;

        if (newMode == FollowMode.FollowSoldier || newMode == FollowMode.FollowCitizen)
        {
            SetupFollowTargetImmediate(newMode);
        }
    }

    private void SetupFollowTargetImmediate(FollowMode mode)
    {
        CleanupPreyFollowView();

        if (simulationManager == null)
        {
            simulationManager = FindFirstObjectByType<SimulationManager>();
            if (simulationManager == null)
            {
                return;
            }
        }

        Vector3 spawnLoc = GetTitanSpawnLocationReference();
        PreyAgent.HumanRole targetRole = (mode == FollowMode.FollowSoldier) 
            ? PreyAgent.HumanRole.Soldier 
            : PreyAgent.HumanRole.Civilian;

        // Find if there's any active, non-defeated Titan
        PredatorAgent activeTitan = null;
        for (int i = 0; i < activeTrackers.Count; i++)
        {
            if (activeTrackers[i].agent != null && !activeTrackers[i].agent.IsDefeated)
            {
                activeTitan = activeTrackers[i].agent;
                break;
            }
        }

        Vector3 referencePosition = spawnLoc;
        PreyAgent excludedAgent = null;

        if (activeTitan != null)
        {
            referencePosition = activeTitan.transform.position;
            excludedAgent = activeTitan.TargetPrey;
        }

        PreyAgent closestAgent = null;
        float closestDistSqr = float.MaxValue;

        var preyList = simulationManager.PreyAgents;
        for (int i = 0; i < preyList.Count; i++)
        {
            PreyAgent prey = preyList[i];
            if (prey != null && prey.Role == targetRole && prey != excludedAgent)
            {
                float distSqr = (prey.transform.position - referencePosition).sqrMagnitude;
                if (distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    closestAgent = prey;
                }
            }
        }

        if (closestAgent != null)
        {
            trackedPreyAgent = closestAgent;

            if (viewRoot == null)
            {
                GameObject rootObject = GameObject.Find("CameraViews");
                viewRoot = rootObject != null ? rootObject.transform : null;
            }

            if (viewRoot != null)
            {
                string viewName = (targetRole == PreyAgent.HumanRole.Soldier) ? "View_Soldier_Follow" : "View_Citizen_Follow";
                GameObject followObj = new GameObject(viewName);
                followObj.transform.SetParent(viewRoot, false);
                preyFollowView = followObj.transform;

                Vector3 agentPos = trackedPreyAgent.transform.position;
                Vector3 agentForward = trackedPreyAgent.transform.forward;
                if (agentForward.sqrMagnitude < 0.001f) agentForward = Vector3.forward;

                float followDistance = 14f;
                float followHeight = 10f;
                float lookAtOffset = 1.5f;

                Vector3 targetPosition = agentPos - agentForward * followDistance + Vector3.up * followHeight;
                preyFollowView.position = targetPosition;
                preyFollowView.rotation = Quaternion.LookRotation(agentPos + Vector3.up * lookAtOffset - targetPosition);

                RefreshViews();

                int preyViewIndex = views.IndexOf(preyFollowView);
                if (preyViewIndex >= 0)
                {
                    SetView(preyViewIndex);
                }
            }
        }
    }

    private void CleanupPreyFollowView()
    {
        trackedPreyAgent = null;
        if (preyFollowView != null)
        {
            preyFollowView.gameObject.SetActive(false);
            Destroy(preyFollowView.gameObject);
            preyFollowView = null;
            RefreshViews();
        }
    }

    private void UpdatePreyTracking()
    {
        if (currentFollowMode != FollowMode.FollowSoldier && currentFollowMode != FollowMode.FollowCitizen)
        {
            return;
        }

        if (trackedPreyAgent == null || !trackedPreyAgent.gameObject.activeInHierarchy)
        {
            CleanupPreyFollowView();
            SetupFollowTargetImmediate(currentFollowMode);
            if (trackedPreyAgent == null)
            {
                SetFollowMode(FollowMode.UnfollowAll);
            }
        }
    }

    private void UpdatePreyFollowView()
    {
        if (preyFollowView != null && trackedPreyAgent != null)
        {
            Vector3 agentPos = trackedPreyAgent.transform.position;
            Vector3 agentForward = trackedPreyAgent.transform.forward;
            if (agentForward.sqrMagnitude < 0.001f) agentForward = Vector3.forward;

            float followDistance = 14f;
            float followHeight = 10f;
            float lookAtOffset = 1.5f;

            Vector3 targetPosition = agentPos - agentForward * followDistance + Vector3.up * followHeight;
            Vector3 targetLookAt = agentPos + Vector3.up * lookAtOffset;

            Vector3 currentPos = preyFollowView.position;
            Quaternion currentRot = preyFollowView.rotation;

            if (currentPos.sqrMagnitude < 0.001f || Vector3.Distance(currentPos, targetPosition) > 100f)
            {
                preyFollowView.position = targetPosition;
                preyFollowView.rotation = Quaternion.LookRotation(targetLookAt - targetPosition);
            }
            else
            {
                preyFollowView.position = Vector3.Lerp(currentPos, targetPosition, Time.deltaTime * 6f);
                preyFollowView.rotation = Quaternion.Slerp(currentRot, Quaternion.LookRotation(targetLookAt - targetPosition), Time.deltaTime * 6f);
            }
        }
    }

    private void UpdateHighlightCamera()
    {
        if (currentFollowMode != FollowMode.HighlightCamera)
        {
            return;
        }

        UpdateHighlightEventsList();

        if (currentHighlightState == HighlightState.Idle)
        {
            if (highlightEvents.Count > 0)
            {
                HighlightEvent bestEvent = GetHighestPriorityMostRecentEvent();
                if (bestEvent != null)
                {
                    activeHighlightEvent = bestEvent;
                    currentHighlightState = HighlightState.Active;
                    SetupHighlightFollowView(bestEvent);
                }
            }
        }
        else if (currentHighlightState == HighlightState.Active)
        {
            if (activeHighlightEvent == null || !IsEventActive(activeHighlightEvent))
            {
                CleanupHighlightFollowView();
                activeHighlightEvent = null;
                currentHighlightState = HighlightState.Idle;
            }
        }
    }

    private void UpdateHighlightEventsList()
    {
        if (simulationManager == null)
        {
            simulationManager = FindFirstObjectByType<SimulationManager>();
            if (simulationManager == null) return;
        }

        // 1. Clean up events that have finished
        for (int i = highlightEvents.Count - 1; i >= 0; i--)
        {
            if (!IsEventActive(highlightEvents[i]))
            {
                highlightEvents.RemoveAt(i);
            }
        }

        // 2. Scan and add new CitizenEscape events
        var preyList = simulationManager.PreyAgents;
        for (int i = 0; i < preyList.Count; i++)
        {
            PreyAgent prey = preyList[i];
            if (prey != null && prey.Role == PreyAgent.HumanRole.Civilian && prey.IsEvacuating)
            {
                bool alreadyExists = false;
                for (int j = 0; j < highlightEvents.Count; j++)
                {
                    if (highlightEvents[j].Type == HighlightType.CitizenEscape && highlightEvents[j].Actor == prey)
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (!alreadyExists)
                {
                    highlightEvents.Add(new HighlightEvent
                    {
                        Type = HighlightType.CitizenEscape,
                        Actor = prey,
                        Target = null
                    });
                }
            }
        }

        // 3. Scan and add new SoldierFight events
        for (int i = 0; i < preyList.Count; i++)
        {
            PreyAgent prey = preyList[i];
            if (prey != null && prey.Role == PreyAgent.HumanRole.Soldier && prey.SoldierTarget != null && !prey.SoldierTarget.IsDefeated)
            {
                bool alreadyExists = false;
                for (int j = 0; j < highlightEvents.Count; j++)
                {
                    if (highlightEvents[j].Type == HighlightType.SoldierFight && highlightEvents[j].Actor == prey && highlightEvents[j].Target == prey.SoldierTarget)
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (!alreadyExists)
                {
                    highlightEvents.Add(new HighlightEvent
                    {
                        Type = HighlightType.SoldierFight,
                        Actor = prey,
                        Target = prey.SoldierTarget
                    });
                }
            }
        }

        // 4. Scan and add new TitanConsume events
        for (int i = 0; i < activeTrackers.Count; i++)
        {
            PredatorAgent titan = activeTrackers[i].agent;
            if (titan != null && !titan.IsDefeated && titan.TargetPrey != null)
            {
                bool alreadyExists = false;
                for (int j = 0; j < highlightEvents.Count; j++)
                {
                    if (highlightEvents[j].Type == HighlightType.TitanConsume && highlightEvents[j].Actor == titan && highlightEvents[j].Target == titan.TargetPrey)
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (!alreadyExists)
                {
                    highlightEvents.Add(new HighlightEvent
                    {
                        Type = HighlightType.TitanConsume,
                        Actor = titan,
                        Target = titan.TargetPrey
                    });
                }
            }
        }
    }

    private HighlightEvent GetHighestPriorityMostRecentEvent()
    {
        if (highlightEvents.Count == 0) return null;

        HighlightType maxPriority = HighlightType.TitanConsume;
        bool hasAny = false;

        for (int i = 0; i < highlightEvents.Count; i++)
        {
            if (highlightEvents[i].Type > maxPriority || !hasAny)
            {
                maxPriority = highlightEvents[i].Type;
                hasAny = true;
            }
        }

        if (!hasAny) return null;

        for (int i = highlightEvents.Count - 1; i >= 0; i--)
        {
            if (highlightEvents[i].Type == maxPriority)
            {
                return highlightEvents[i];
            }
        }

        return null;
    }

    private bool IsEventActive(HighlightEvent ev)
    {
        if (ev.Actor == null || !ev.Actor.gameObject.activeInHierarchy)
        {
            return false;
        }

        switch (ev.Type)
        {
            case HighlightType.CitizenEscape:
                var civilian = ev.Actor as PreyAgent;
                return civilian != null && civilian.Role == PreyAgent.HumanRole.Civilian && civilian.IsEvacuating;

            case HighlightType.SoldierFight:
                var soldier = ev.Actor as PreyAgent;
                return soldier != null && soldier.Role == PreyAgent.HumanRole.Soldier && soldier.SoldierTarget != null && !soldier.SoldierTarget.IsDefeated;

            case HighlightType.TitanConsume:
                var titan = ev.Actor as PredatorAgent;
                return titan != null && !titan.IsDefeated && titan.TargetPrey != null;
        }

        return false;
    }

    private void SetupHighlightFollowView(HighlightEvent ev)
    {
        CleanupHighlightFollowView();

        if (viewRoot == null)
        {
            GameObject rootObject = GameObject.Find("CameraViews");
            viewRoot = rootObject != null ? rootObject.transform : null;
        }

        if (viewRoot != null)
        {
            GameObject followObj = new GameObject("View_Highlight_Follow");
            followObj.transform.SetParent(viewRoot, false);
            highlightFollowView = followObj.transform;

            UpdateHighlightFollowViewPosition(true);

            RefreshViews();

            int index = views.IndexOf(highlightFollowView);
            if (index >= 0)
            {
                SetView(index);
            }
        }
    }

    private void UpdateHighlightFollowViewPosition(bool snap)
    {
        if (highlightFollowView == null || activeHighlightEvent == null || activeHighlightEvent.Actor == null)
        {
            return;
        }

        Vector3 actorPos = activeHighlightEvent.Actor.transform.position;
        Vector3 actorForward = activeHighlightEvent.Actor.transform.forward;
        if (actorForward.sqrMagnitude < 0.001f) actorForward = Vector3.forward;

        Vector3 targetPosition = Vector3.zero;
        Vector3 targetLookAt = Vector3.zero;

        switch (activeHighlightEvent.Type)
        {
            case HighlightType.CitizenEscape:
                float citizenDist = 12f;
                float citizenHeight = 8f;
                targetPosition = actorPos - actorForward * citizenDist + Vector3.up * citizenHeight;
                targetLookAt = actorPos + Vector3.up * 1.2f;
                break;

            case HighlightType.SoldierFight:
                if (activeHighlightEvent.Target != null)
                {
                    Vector3 titanPos = activeHighlightEvent.Target.transform.position;
                    Vector3 midPoint = (actorPos + titanPos) * 0.5f;
                    Vector3 toTitan = (titanPos - actorPos).normalized;
                    Vector3 sideDir = Vector3.Cross(Vector3.up, toTitan).normalized;
                    
                    float fightDist = 18f;
                    float fightHeight = 12f;
                    targetPosition = midPoint + sideDir * fightDist + Vector3.up * fightHeight;
                    targetLookAt = midPoint + Vector3.up * 2f;
                }
                else
                {
                    targetPosition = actorPos - actorForward * 14f + Vector3.up * 10f;
                    targetLookAt = actorPos + Vector3.up * 1.5f;
                }
                break;

            case HighlightType.TitanConsume:
                if (activeHighlightEvent.Target != null)
                {
                    Vector3 victimPos = activeHighlightEvent.Target.transform.position;
                    Vector3 midPoint = (actorPos + victimPos) * 0.5f;
                    Vector3 toVictim = (victimPos - actorPos).normalized;
                    Vector3 sideDir = Vector3.Cross(Vector3.up, toVictim).normalized;

                    float consumeDist = 22f;
                    float consumeHeight = 15f;
                    targetPosition = midPoint + sideDir * consumeDist + Vector3.up * consumeHeight;
                    targetLookAt = midPoint + Vector3.up * 3f;
                }
                else
                {
                    targetPosition = actorPos - actorForward * 24f + Vector3.up * 16f;
                    targetLookAt = actorPos + Vector3.up * 5f;
                }
                break;
        }

        Vector3 currentPos = highlightFollowView.position;
        Quaternion currentRot = highlightFollowView.rotation;

        if (snap || currentPos.sqrMagnitude < 0.001f || Vector3.Distance(currentPos, targetPosition) > 100f)
        {
            highlightFollowView.position = targetPosition;
            highlightFollowView.rotation = Quaternion.LookRotation(targetLookAt - targetPosition);
        }
        else
        {
            highlightFollowView.position = Vector3.Lerp(currentPos, targetPosition, Time.deltaTime * 6f);
            highlightFollowView.rotation = Quaternion.Slerp(currentRot, Quaternion.LookRotation(targetLookAt - targetPosition), Time.deltaTime * 6f);
        }
    }

    private void CleanupHighlightFollowView()
    {
        if (highlightFollowView != null)
        {
            highlightFollowView.gameObject.SetActive(false);
            Destroy(highlightFollowView.gameObject);
            highlightFollowView = null;
            RefreshViews();
        }
    }
}
