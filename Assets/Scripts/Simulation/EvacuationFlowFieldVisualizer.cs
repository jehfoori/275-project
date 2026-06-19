using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class EvacuationFlowFieldVisualizer : MonoBehaviour
{
    [SerializeField] private SimulationManager simulationManager;
    [SerializeField] private float lineHeight = 0.45f;
    [SerializeField] private float arrowHeadLength = 1.4f;
    [SerializeField] private float arrowHeadWidth = 0.55f;
    [SerializeField] private float maxCostForColor = 120f;

    private bool showFlowField;
    private static Material lineMaterial;
    private readonly List<EvacuationFlowSegment> segmentCache = new List<EvacuationFlowSegment>();

    public bool ShowFlowField
    {
        get => showFlowField;
        set => showFlowField = value;
    }

    private void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!showFlowField || camera == null || !camera.CompareTag("MainCamera"))
        {
            return;
        }

        EnsureReferences();
        if (simulationManager == null || !simulationManager.TryGetEvacuationFlowSegments(segmentCache))
        {
            return;
        }

        EnsureLineMaterial();
        lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.LoadProjectionMatrix(camera.projectionMatrix);
        GL.modelview = camera.worldToCameraMatrix;
        GL.Begin(GL.LINES);

        for (int i = 0; i < segmentCache.Count; i++)
        {
            EvacuationFlowSegment segment = segmentCache[i];
            Color color = GetFlowColor(segment.Cost);
            GL.Color(color);

            Vector3 from = segment.From + Vector3.up * lineHeight;
            Vector3 to = segment.To + Vector3.up * lineHeight;
            GL.Vertex(from);
            GL.Vertex(to);

            DrawArrowHead(from, to, color);
        }

        GL.End();
        GL.PopMatrix();
    }

    private void DrawArrowHead(Vector3 from, Vector3 to, Color color)
    {
        Vector3 direction = to - from;
        float length = direction.magnitude;
        if (length <= 0.001f)
        {
            return;
        }

        Vector3 forward = direction / length;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 tip = to;
        Vector3 left = tip - forward * arrowHeadLength + right * arrowHeadWidth;
        Vector3 rightTip = tip - forward * arrowHeadLength - right * arrowHeadWidth;

        GL.Color(color);
        GL.Vertex(left);
        GL.Vertex(tip);
        GL.Vertex(rightTip);
        GL.Vertex(tip);
    }

    private Color GetFlowColor(float cost)
    {
        float normalized = Mathf.Clamp01(cost / Mathf.Max(1f, maxCostForColor));
        return Color.Lerp(new Color(0.2f, 1f, 0.45f, 0.95f), new Color(0.2f, 0.55f, 1f, 0.85f), normalized);
    }

    private static void EnsureLineMaterial()
    {
        if (lineMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)CullMode.Off);
        lineMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        lineMaterial.SetInt("_ZWrite", 0);
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
    }
}

public struct EvacuationFlowSegment
{
    public Vector3 From;
    public Vector3 To;
    public float Cost;
}
