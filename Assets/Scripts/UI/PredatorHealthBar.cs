using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PredatorAgent))]
public sealed class PredatorHealthBar : MonoBehaviour
{
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 5.5f, 0f);
    [SerializeField] private Vector2 size = new Vector2(90f, 8f);
    [SerializeField] private float border = 2f;
    [SerializeField] private float maxVisibleDistance = 220f;
    [SerializeField] private bool showWhenFull;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);
    [SerializeField] private Color highHealthColor = new Color(0.2f, 0.9f, 0.25f, 0.95f);
    [SerializeField] private Color midHealthColor = new Color(1f, 0.78f, 0.18f, 0.95f);
    [SerializeField] private Color lowHealthColor = new Color(1f, 0.2f, 0.12f, 0.95f);

    private PredatorAgent predator;

    private void Awake()
    {
        predator = GetComponent<PredatorAgent>();
    }

    private void OnValidate()
    {
        size = new Vector2(Mathf.Max(20f, size.x), Mathf.Max(4f, size.y));
        border = Mathf.Clamp(border, 0f, Mathf.Min(size.x, size.y) * 0.4f);
        maxVisibleDistance = Mathf.Max(1f, maxVisibleDistance);
    }

    private void OnGUI()
    {
        if (predator == null || Camera.main == null)
        {
            return;
        }

        float health = predator.HealthNormalized;
        if (!showWhenFull && health >= 0.999f && !predator.IsDefeated)
        {
            return;
        }

        Camera camera = Camera.main;
        Vector3 worldPosition = transform.position + worldOffset;
        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f || screenPosition.z > maxVisibleDistance)
        {
            return;
        }

        float x = screenPosition.x - size.x * 0.5f;
        float y = Screen.height - screenPosition.y - size.y * 0.5f;
        Rect backgroundRect = new Rect(x, y, size.x, size.y);
        Rect fillRect = new Rect(
            x + border,
            y + border,
            Mathf.Max(0f, (size.x - border * 2f) * health),
            Mathf.Max(0f, size.y - border * 2f));

        DrawRect(backgroundRect, backgroundColor);
        DrawRect(fillRect, GetFillColor(health));
    }

    private Color GetFillColor(float health)
    {
        if (health <= 0.35f)
        {
            return lowHealthColor;
        }

        if (health <= 0.65f)
        {
            return midHealthColor;
        }

        return highHealthColor;
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }
}
