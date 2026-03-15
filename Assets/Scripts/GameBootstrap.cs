using UnityEngine;

/// <summary>
/// Scene entry point. Self-heals missing references via FindFirstObjectByType.
/// Prints a full diagnostic on Start so you can see exactly what is wired.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Auto-found if blank")]
    public SimulationManager simulationManager;
    public WorldGenerator    worldGenerator;
    public TribeManager      tribeManager;

    void Start()
    {
        Debug.Log("[Bootstrap] ===== START =====");

        // Auto-find everything — works even if Inspector refs lost on scene reload
        if (!simulationManager) simulationManager = FindFirstObjectByType<SimulationManager>();
        if (!worldGenerator)    worldGenerator    = FindFirstObjectByType<WorldGenerator>();
        if (!tribeManager)      tribeManager      = FindFirstObjectByType<TribeManager>();

        // Log every system
        LogRef("SimulationManager", simulationManager);
        LogRef("WorldGenerator",    worldGenerator);
        LogRef("TribeManager",      tribeManager);

        var wm = FindFirstObjectByType<WorldMap>();
        var wr = FindFirstObjectByType<WorldRenderer>();
        var bm = FindFirstObjectByType<BuildingManager>();
        LogRef("WorldMap",       wm);
        LogRef("WorldRenderer",  wr);
        LogRef("BuildingManager",bm);

        if (wr != null)
        {
            Debug.Log("[Bootstrap]   wr.terrainTilemap  = " + (wr.terrainTilemap  != null ? "OK" : "NULL !!!"));
            Debug.Log("[Bootstrap]   wr.buildingTilemap = " + (wr.buildingTilemap != null ? "OK" : "NULL !!!"));
            Debug.Log("[Bootstrap]   wr.fogTilemap      = " + (wr.fogTilemap      != null ? "OK" : "NULL !!!"));
            Debug.Log("[Bootstrap]   wr.mainCamera      = " + (wr.mainCamera      != null ? "OK" : "NULL !!!"));
        }

        if (worldGenerator != null)
        {
            Debug.Log("[Bootstrap]   wg.worldMap      = " + (worldGenerator.worldMap      != null ? "OK" : "NULL !!!"));
            Debug.Log("[Bootstrap]   wg.worldRenderer = " + (worldGenerator.worldRenderer != null ? "OK" : "NULL !!!"));
        }

        if (tribeManager != null)
            Debug.Log("[Bootstrap]   tm.characterPrefab = " + (tribeManager.characterPrefab != null ? "OK" : "NULL !!!"));

        Debug.Log("[Bootstrap] ==================");

        if (worldGenerator == null)
        {
            Debug.LogError("[Bootstrap] WorldGenerator missing! Run TribeGame > Build Scene on a fresh EMPTY scene, then SAVE.");
            return;
        }

        Debug.Log("[Bootstrap] Starting world generation...");
        worldGenerator.Generate();
    }

    void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space))  simulationManager?.SetPaused(!simulationManager.isPaused);
        if (Input.GetKeyDown(KeyCode.Alpha1)) simulationManager?.SetTimeScale(1f);
        if (Input.GetKeyDown(KeyCode.Alpha2)) simulationManager?.SetTimeScale(3f);
        if (Input.GetKeyDown(KeyCode.Alpha3)) simulationManager?.SetTimeScale(10f);
        if (Input.GetKeyDown(KeyCode.Alpha4)) simulationManager?.SetTimeScale(20f);
#else
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame)  simulationManager?.SetPaused(!simulationManager.isPaused);
            if (kb.digit1Key.wasPressedThisFrame) simulationManager?.SetTimeScale(1f);
            if (kb.digit2Key.wasPressedThisFrame) simulationManager?.SetTimeScale(3f);
            if (kb.digit3Key.wasPressedThisFrame) simulationManager?.SetTimeScale(10f);
            if (kb.digit4Key.wasPressedThisFrame) simulationManager?.SetTimeScale(20f);
        }
#endif
    }

    static void LogRef(string label, Object obj)
    {
        if (obj != null)
            Debug.Log($"[Bootstrap]   OK  {label}");
        else
            Debug.LogWarning($"[Bootstrap]   !!  {label} is MISSING");
    }
}
