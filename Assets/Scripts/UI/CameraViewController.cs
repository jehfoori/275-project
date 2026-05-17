using System.Collections.Generic;
using UnityEngine;

public sealed class CameraViewController : MonoBehaviour
{
    [SerializeField] private Transform viewRoot;
    [SerializeField] private float transitionDuration = 0.45f;
    [SerializeField] private bool snapToFirstViewOnStart = true;
    [SerializeField] private bool showButtons = true;
    [SerializeField] private Vector2 buttonSize = new Vector2(140f, 34f);
    [SerializeField] private float buttonSpacing = 8f;
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
    private GUIStyle activeButtonStyle;

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

        if (!showButtons || views.Count == 0)
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
        float totalWidth = views.Count * buttonSize.x + Mathf.Max(0, views.Count - 1) * buttonSpacing;
        float startX = Mathf.Max(safeArea.xMin + 8f, safeArea.xMin + (safeArea.width - totalWidth) * 0.5f);
        float y = Mathf.Clamp(
            safeArea.yMax - buttonSize.y - bottomMargin,
            safeArea.yMin + 8f,
            safeArea.yMax - buttonSize.y - 8f);

        for (int i = 0; i < views.Count; i++)
        {
            Rect rect = new Rect(startX + i * (buttonSize.x + buttonSpacing), y, buttonSize.x, buttonSize.y);
            string label = FormatViewLabel(i, views[i]);
            bool isCurrentView = i == currentViewIndex && !isTransitioning;
            GUI.backgroundColor = isCurrentView ? new Color(0.35f, 0.55f, 0.9f, 1f) : new Color(0.95f, 0.95f, 0.95f, 1f);

            if (GUI.Button(rect, label, isCurrentView ? activeButtonStyle : buttonStyle) && !isCurrentView)
            {
                SetView(i);
            }
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

        int viewIndex = KeyCodeToViewIndex(currentEvent.keyCode);
        if (viewIndex < 0 || viewIndex >= views.Count)
        {
            return;
        }

        SetView(viewIndex);
        currentEvent.Use();
    }

    private static int KeyCodeToViewIndex(KeyCode keyCode)
    {
        if (keyCode >= KeyCode.Alpha1 && keyCode <= KeyCode.Alpha9)
        {
            return keyCode - KeyCode.Alpha1;
        }

        if (keyCode >= KeyCode.Keypad1 && keyCode <= KeyCode.Keypad9)
        {
            return keyCode - KeyCode.Keypad1;
        }

        return -1;
    }

    private static string FormatViewLabel(int index, Transform view)
    {
        string label = view != null ? view.name : "View";
        if (label.StartsWith("View_"))
        {
            label = label.Substring(5);
        }

        label = label.Replace('_', ' ');
        return $"{index + 1}. {label}";
    }

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
        if (buttonStyle != null && activeButtonStyle != null)
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

        activeButtonStyle = new GUIStyle(buttonStyle);
        activeButtonStyle.normal.textColor = Color.white;
        activeButtonStyle.hover.textColor = Color.white;
        activeButtonStyle.active.textColor = Color.white;
        activeButtonStyle.focused.textColor = Color.white;
    }
}
