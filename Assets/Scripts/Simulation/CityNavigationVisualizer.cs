using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class CityNavigationVisualizer : MonoBehaviour
{
    [SerializeField] private SimulationManager simulationManager;
    [SerializeField] private float lineHeight = 0.2f;
    [SerializeField] private int nodeCircleSegments = 16;

    private bool showNavigationGraph;
    private static Material lineMaterial;
    private readonly List<NavigationLinkSegment> linkCache = new List<NavigationLinkSegment>();
    private readonly List<Vector3> nodeCache = new List<Vector3>();

    public bool ShowNavigationGraph
    {
        get => showNavigationGraph;
        set => showNavigationGraph = value;
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
        if (!showNavigationGraph || camera == null || !camera.CompareTag("MainCamera"))
        {
            return;
        }

        EnsureReferences();
        if (simulationManager == null || !simulationManager.TryGetNavigationGraphVisualization(linkCache, nodeCache))
        {
            return;
        }

        EnsureLineMaterial();
        lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.LoadProjectionMatrix(camera.projectionMatrix);
        GL.modelview = camera.worldToCameraMatrix;
        GL.Begin(GL.LINES);

        DrawLinks();
        DrawNodes();

        GL.End();
        GL.PopMatrix();
    }

    private void DrawLinks()
    {
        for (int i = 0; i < linkCache.Count; i++)
        {
            NavigationLinkSegment segment = linkCache[i];
            GL.Color(segment.IsClear
                ? new Color(0.25f, 0.95f, 0.4f, 0.85f)
                : new Color(1f, 0.2f, 0.15f, 0.35f));

            Vector3 from = segment.From + Vector3.up * lineHeight;
            Vector3 to = segment.To + Vector3.up * lineHeight;
            GL.Vertex(from);
            GL.Vertex(to);
        }
    }

    private void DrawNodes()
    {
        float radius = simulationManager.NavigationNodeRadius;
        Color nodeColor = new Color(0.2f, 0.65f, 1f, 0.9f);

        for (int i = 0; i < nodeCache.Count; i++)
        {
            DrawHorizontalCircle(nodeCache[i] + Vector3.up * lineHeight, radius, nodeColor);
        }
    }

    private void DrawHorizontalCircle(Vector3 center, float radius, Color color)
    {
        if (radius <= 0.001f || nodeCircleSegments < 3)
        {
            return;
        }

        GL.Color(color);
        Vector3 previousPoint = center + new Vector3(radius, 0f, 0f);
        float step = Mathf.PI * 2f / nodeCircleSegments;

        for (int segment = 1; segment <= nodeCircleSegments; segment++)
        {
            float angle = step * segment;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            GL.Vertex(previousPoint);
            GL.Vertex(nextPoint);
            previousPoint = nextPoint;
        }
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

public struct NavigationLinkSegment
{
    public Vector3 From;
    public Vector3 To;
    public bool IsClear;
}
