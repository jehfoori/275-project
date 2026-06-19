using UnityEngine;

[DisallowMultipleComponent]
public sealed class PredatorSpawner : MonoBehaviour
{
    [SerializeField] private PredatorAgent predatorPrefab;
    [SerializeField] private SimulationManager simulationManager;
    [SerializeField] private Transform spawnRoot;
    [SerializeField] private Transform predatorParent;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool showSpawnButton = true;
    [SerializeField] private float initialDelay = 4f;
    [SerializeField] private float spawnInterval = 1f;
    [SerializeField] private float manualSpawnCooldown = 1f;
    [SerializeField] private int maxActivePredators = 20;
    [SerializeField] private float fallbackWestInset = 8f;
    [SerializeField] private float fallbackZSpread = 18f;
    [SerializeField] private Vector2 buttonPosition = new Vector2(18f, 18f);
    [SerializeField] private Vector2 buttonSize = new Vector2(170f, 38f);

    private float nextSpawnTime;
    private float nextManualSpawnTime;
    private GUIStyle buttonStyle;

    private void OnValidate()
    {
        initialDelay = Mathf.Max(0f, initialDelay);
        spawnInterval = Mathf.Max(0.1f, spawnInterval);
        manualSpawnCooldown = Mathf.Max(0f, manualSpawnCooldown);
        maxActivePredators = Mathf.Max(0, maxActivePredators);
        fallbackWestInset = Mathf.Max(0f, fallbackWestInset);
        fallbackZSpread = Mathf.Max(0f, fallbackZSpread);
        buttonSize = new Vector2(Mathf.Max(80f, buttonSize.x), Mathf.Max(24f, buttonSize.y));
    }

    public void ConfigureForWebGl(int maxPredators, bool enableAutoSpawn = false)
    {
        maxActivePredators = Mathf.Max(0, maxPredators);
        spawnOnStart = enableAutoSpawn;
    }

    private void Awake()
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            ConfigureForWebGl(8, enableAutoSpawn: false);
        }

        EnsureReferences(true);
        nextSpawnTime = Time.time + initialDelay;
    }

    private void Update()
    {
        if (!spawnOnStart || predatorPrefab == null || simulationManager == null || maxActivePredators <= 0)
        {
            return;
        }

        if (Time.time < nextSpawnTime || simulationManager.PredatorAgents.Count >= maxActivePredators)
        {
            return;
        }

        TrySpawnPredator(false);
        nextSpawnTime = Time.time + spawnInterval;
    }

    [ContextMenu("Spawn Predator")]
    private void SpawnPredatorFromContextMenu()
    {
        TrySpawnPredator(true);
    }

    public int SpawnTitans(int count)
    {
        int spawned = 0;

        for (int i = 0; i < count; i++)
        {
            if (TrySpawnPredator(true))
            {
                spawned++;
            }
        }

        return spawned;
    }

    public bool TrySpawnPredator(bool ignoreManualCooldown)
    {
        EnsureReferences(true);

        if (predatorPrefab == null || simulationManager == null)
        {
            Debug.LogWarning("PredatorSpawner needs a predator prefab and SimulationManager before it can spawn predators.", this);
            return false;
        }

        if (maxActivePredators <= 0 || simulationManager.PredatorAgents.Count >= maxActivePredators)
        {
            return false;
        }

        if (!ignoreManualCooldown && Time.time < nextManualSpawnTime)
        {
            return false;
        }

        Vector3 spawnPosition = GetSpawnPosition();
        Quaternion rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        Transform parent = predatorParent != null ? predatorParent : transform;

        PredatorAgent predator = Instantiate(predatorPrefab, spawnPosition, rotation, parent);
        simulationManager.RegisterPredator(predator);
        predator.Initialize(simulationManager.Bounds, simulationManager, simulationManager.Navigation);
        nextManualSpawnTime = Time.time + manualSpawnCooldown;
        return true;
    }

    private void OnGUI()
    {
        if (!showSpawnButton || ShouldHideManualSpawnButton())
        {
            return;
        }

        EnsureButtonStyle();

        Rect safeArea = Screen.safeArea;
        Rect buttonRect = new Rect(
            safeArea.xMin + buttonPosition.x,
            safeArea.yMin + buttonPosition.y,
            buttonSize.x,
            buttonSize.y);

        bool canSpawn = CanManuallySpawn();
        string label = canSpawn
            ? "Spawn Predator"
            : GetUnavailableButtonLabel();

        Color previousBackgroundColor = GUI.backgroundColor;
        GUI.backgroundColor = canSpawn ? new Color(0.85f, 0.25f, 0.18f, 1f) : new Color(0.45f, 0.45f, 0.45f, 1f);

        if (GUI.Button(buttonRect, label, buttonStyle) && canSpawn)
        {
            TrySpawnPredator(false);
        }

        GUI.backgroundColor = previousBackgroundColor;
    }

    private bool ShouldHideManualSpawnButton()
    {
        if (simulationManager == null)
        {
            return false;
        }

        if (simulationManager.IsSandboxMode)
        {
            return simulationManager.Phase == SimulationManager.SimulationPhase.AwaitingStart;
        }

        return simulationManager.UsesSetupFlow
            || simulationManager.Phase == SimulationManager.SimulationPhase.AwaitingStart;
    }

    private bool CanManuallySpawn()
    {
        return predatorPrefab != null
            && simulationManager != null
            && maxActivePredators > 0
            && simulationManager.PredatorAgents.Count < maxActivePredators
            && Time.time >= nextManualSpawnTime;
    }

    private string GetUnavailableButtonLabel()
    {
        if (simulationManager != null && simulationManager.PredatorAgents.Count >= maxActivePredators)
        {
            return "Predator Limit";
        }

        float cooldownRemaining = nextManualSpawnTime - Time.time;
        if (cooldownRemaining > 0f)
        {
            return $"Spawn ({cooldownRemaining:0.0}s)";
        }

        return "Spawn Unavailable";
    }

    private void EnsureButtonStyle()
    {
        if (buttonStyle != null)
        {
            return;
        }

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip
        };
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;
        buttonStyle.focused.textColor = Color.white;
    }

    private Vector3 GetSpawnPosition()
    {
        if (spawnRoot != null && spawnRoot.childCount > 0)
        {
            Transform spawnPoint = spawnRoot.GetChild(Random.Range(0, spawnRoot.childCount));
            return simulationManager.Bounds != null
                ? simulationManager.Bounds.ProjectPointToGround(spawnPoint.position)
                : spawnPoint.position;
        }

        SimulationBounds bounds = simulationManager.Bounds;
        if (bounds == null)
        {
            return transform.position;
        }

        if (simulationManager.Navigation != null && simulationManager.Navigation.HasWaypoints)
        {
            Vector3 westmostWaypoint = simulationManager.Navigation.Nodes[0].Position;
            for (int i = 1; i < simulationManager.Navigation.Nodes.Count; i++)
            {
                Vector3 nodePosition = simulationManager.Navigation.Nodes[i].Position;
                if (nodePosition.x < westmostWaypoint.x)
                {
                    westmostWaypoint = nodePosition;
                }
            }

            Vector3 waypointSpawn = westmostWaypoint + Vector3.left * fallbackWestInset;
            waypointSpawn.z += Random.Range(-fallbackZSpread, fallbackZSpread);
            waypointSpawn = bounds.ProjectPointToGround(waypointSpawn);

            if (simulationManager.Navigation.IsPointBlocked(waypointSpawn, 4f))
            {
                waypointSpawn = simulationManager.Navigation.ProjectOutsideObstacles(waypointSpawn, 4f);
            }

            return bounds.ProjectPointToGround(waypointSpawn);
        }

        Vector3 halfSize = bounds.Size * 0.5f;
        float x = bounds.Center.x - halfSize.x + fallbackWestInset;
        float z = bounds.Center.z + Random.Range(-fallbackZSpread, fallbackZSpread);
        return bounds.ProjectPointToGround(new Vector3(x, bounds.GroundY, z));
    }

    private void EnsureReferences(bool createMissingParent)
    {
        if (simulationManager == null)
        {
            simulationManager = GetComponent<SimulationManager>();
        }

        if (simulationManager == null)
        {
            simulationManager = FindFirstObjectByType<SimulationManager>();
        }

        if (spawnRoot == null)
        {
            GameObject spawnRootObject = GameObject.Find("PredatorSpawnPoints");
            spawnRoot = spawnRootObject != null ? spawnRootObject.transform : null;
        }

        if (predatorParent == null && createMissingParent)
        {
            GameObject predatorsObject = GameObject.Find("Predators");
            if (predatorsObject == null)
            {
                predatorsObject = new GameObject("Predators");
            }

            predatorParent = predatorsObject.transform;
        }
    }

    private void OnDrawGizmosSelected()
    {
        EnsureReferences(false);

        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.8f);
        if (spawnRoot != null && spawnRoot.childCount > 0)
        {
            for (int i = 0; i < spawnRoot.childCount; i++)
            {
                Gizmos.DrawWireSphere(spawnRoot.GetChild(i).position, 2f);
            }

            return;
        }

        if (simulationManager == null || simulationManager.Bounds == null)
        {
            return;
        }

        Vector3 fallback = GetSpawnPosition();
        Gizmos.DrawWireSphere(fallback, 3f);
    }
}
