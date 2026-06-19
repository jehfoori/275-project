using UnityEngine;

/// <summary>
/// Applies global WebGL player settings (quality, frame rate).
/// Agent counts are tuned in SimulationManager and PredatorSpawner Awake().
/// </summary>
public static class WebGlRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyWebGlProfile()
    {
        if (Application.platform != RuntimePlatform.WebGLPlayer)
        {
            return;
        }

        QualitySettings.SetQualityLevel(0, applyExpensiveChanges: true);
        Application.targetFrameRate = 60;
        Debug.Log("WebGL profile applied: quality reduced, target frame rate set to 60.");
    }
}
