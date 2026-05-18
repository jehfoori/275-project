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
        if (viewRoot == null)
        {
            GameObject rootObject = GameObject.Find("CameraViews");
            viewRoot = rootObject != null ? rootObject.transform : null;
        }

        views.Clear();
        if (viewRoot == null)
        {
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
    }

    private void UpdateTransition()
    {
        if (!isTransitioning)
        {
            return;
        }

        float progress = Mathf.Clamp01((Time.time - transitionStartTime) / transitionDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

        transform.position = Vector3.Lerp(transitionStartPosition, transitionEndPosition, easedProgress);
        transform.rotation = Quaternion.Slerp(transitionStartRotation, transitionEndRotation, easedProgress);

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
}
