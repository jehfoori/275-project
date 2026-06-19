using UnityEngine;

public static class SimulationGuiStyles
{
    public static GUIStyle ToggleButton { get; private set; }
    public static GUIStyle PanelTitle { get; private set; }
    public static GUIStyle PanelLabel { get; private set; }
    public static GUIStyle PanelButton { get; private set; }
    public static GUIStyle ResultsTitle { get; private set; }
    public static GUIStyle ResultsLabel { get; private set; }
    public static GUIStyle BannerLabel { get; private set; }
    public static GUIStyle DefenseDescriptionLabel { get; private set; }

    public static void Ensure()
    {
        if (ToggleButton != null)
        {
            return;
        }

        ToggleButton = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip
        };
        ToggleButton.normal.textColor = Color.black;
        ToggleButton.hover.textColor = Color.black;
        ToggleButton.active.textColor = Color.black;
        ToggleButton.focused.textColor = Color.black;

        PanelTitle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        PanelTitle.normal.textColor = Color.white;

        PanelLabel = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft
        };
        PanelLabel.normal.textColor = Color.white;

        PanelButton = new GUIStyle(ToggleButton)
        {
            fontSize = 14
        };

        ResultsTitle = new GUIStyle(PanelTitle)
        {
            fontSize = 20
        };

        ResultsLabel = new GUIStyle(PanelLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };

        BannerLabel = new GUIStyle(PanelLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        BannerLabel.normal.textColor = Color.white;

        DefenseDescriptionLabel = new GUIStyle(PanelLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.UpperCenter,
            wordWrap = true,
            clipping = TextClipping.Overflow
        };
        DefenseDescriptionLabel.normal.textColor = new Color(0.9f, 0.92f, 0.95f, 1f);
    }

    public static void DrawToggleButton(Rect rect, string label, bool isActive)
    {
        Ensure();
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = isActive
            ? new Color(0.4f, 1f, 0.6f, 1f)
            : new Color(0.85f, 0.85f, 0.85f, 0.9f);
        GUI.Button(rect, label, ToggleButton);
        GUI.backgroundColor = previous;
    }
}
