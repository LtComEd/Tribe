using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(-80)]
public class WorldRenderer : MonoBehaviour
{
    [Header("Tilemaps — auto-found if null")]
    public Tilemap terrainTilemap;
    public Tilemap buildingTilemap;
    public Tilemap fogTilemap;

    [Header("Camera — auto-found if null")]
    public Camera mainCamera;

    public float minZoom   = 3f;
    public float maxZoom   = 40f;
    public float panSpeed  = 20f;
    public float zoomSpeed = 4f;

    [Header("Camera Follow")]
    public bool  followTarget     = false;
    public float followSmoothing  = 5f;
    private Transform _followTarget = null;

    // Palette always created at runtime — no Inspector slot needed
    private TilePalette _palette;
    private WorldMap    _worldMap;
    private bool        _ready = false;

    void Awake()
    {
        _palette = ScriptableObject.CreateInstance<TilePalette>();
    }

    void Start()
    {
        // Self-heal all references
        _worldMap = WorldMap.Instance;

        // Find tilemaps by name if not assigned
        if (terrainTilemap  == null) terrainTilemap  = FindTilemap("TerrainTilemap");
        if (buildingTilemap == null) buildingTilemap = FindTilemap("BuildingTilemap");
        if (fogTilemap      == null) fogTilemap      = FindTilemap("FogTilemap");
        if (mainCamera      == null) mainCamera      = Camera.main;

        Debug.Log("[WorldRenderer] Start — terrain=" + (terrainTilemap  != null ? "OK" : "NULL")
                                    + " worldMap="   + (_worldMap        != null ? "OK" : "NULL")
                                    + " camera="     + (mainCamera       != null ? "OK" : "NULL"));

        TileTextureFactory.GenerateAll(_palette);
        Debug.Log("[WorldRenderer] Tiles generated.");
    }

    static Tilemap FindTilemap(string goName)
    {
        // Unity 6: find by name across all scene objects
        var go = GameObject.Find(goName);
        if (go != null)
        {
            var tm = go.GetComponent<Tilemap>();
            if (tm != null)
            {
                Debug.Log("[WorldRenderer] Found " + goName + " by name.");
                return tm;
            }
        }
        Debug.LogWarning("[WorldRenderer] Could not find Tilemap: " + goName);
        return null;
    }

    public void RenderAll()
    {
        // Re-heal in case Start ran before tilemaps were ready
        if (terrainTilemap == null) terrainTilemap = FindTilemap("TerrainTilemap");
        if (_worldMap      == null) _worldMap      = WorldMap.Instance;

        if (terrainTilemap == null)
        {
            Debug.LogError("[WorldRenderer] RenderAll failed — terrainTilemap still null.");
            return;
        }
        if (_worldMap == null)
        {
            Debug.LogError("[WorldRenderer] RenderAll failed — WorldMap still null.");
            return;
        }

        Debug.Log("[WorldRenderer] RenderAll starting — " + WorldMap.WIDTH + "x" + WorldMap.HEIGHT + " tiles...");

        terrainTilemap.ClearAllTiles();
        if (fogTilemap != null) fogTilemap.ClearAllTiles();

        int total        = WorldMap.WIDTH * WorldMap.HEIGHT;
        var positions    = new Vector3Int[total];
        var terrainTiles = new TileBase[total];

        int idx = 0;
        for (int x = 0; x < WorldMap.WIDTH; x++)
        for (int y = 0; y < WorldMap.HEIGHT; y++)
        {
            positions[idx]    = new Vector3Int(x, y, 0);
            var td            = _worldMap.GetTile(x, y);
            terrainTiles[idx] = TileTextureFactory.GetTileFor(_palette, td != null ? td.type : TileType.Grass);
            idx++;
        }

        terrainTilemap.SetTiles(positions, terrainTiles);

        // No fog on first boot — clear it so tiles are visible
        if (fogTilemap != null) fogTilemap.ClearAllTiles();

        _ready = true;
        Debug.Log("[WorldRenderer] RenderAll complete. Tiles placed: " + total);
    }

    public void PlaceBuildingSprite(BuildingData b)
    {
        if (b == null || buildingTilemap == null) return;
        var tile    = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = _palette != null && _palette.tileGrass != null ? _palette.tileGrass.sprite : null;
        tile.color  = GetBuildingColor(b.buildingType);
        buildingTilemap.SetTile(new Vector3Int(b.position.x, b.position.y, 0), tile);
    }

    public void RemoveBuildingSprite(Vector2Int pos)
    {
        if (buildingTilemap != null)
            buildingTilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), null);
    }

    public void RefreshTile(Vector2Int pos)
    {
        if (terrainTilemap == null || _worldMap == null) return;
        var td = _worldMap.GetTile(pos);
        if (td == null) return;
        terrainTilemap.SetTile(new Vector3Int(pos.x, pos.y, 0),
            TileTextureFactory.GetTileFor(_palette, td.type));
    }

    public void RevealTiles(IEnumerable<Vector2Int> visible)
    {
        if (fogTilemap == null) return;
        foreach (var pos in visible)
        {
            var t = _worldMap.GetTile(pos);
            if (t == null || t.fogState == FogState.Visible) continue;
            t.fogState = FogState.Visible;
            fogTilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), null);
        }
    }

    static Color GetBuildingColor(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.CampFire:   return new Color(1.00f, 0.45f, 0.10f);
            case BuildingType.Tent:       return new Color(0.85f, 0.80f, 0.60f);
            case BuildingType.Hut:        return new Color(0.72f, 0.55f, 0.38f);
            case BuildingType.GrainStore: return new Color(0.90f, 0.80f, 0.20f);
            case BuildingType.Forge:      return new Color(0.40f, 0.35f, 0.30f);
            default:                      return Color.white;
        }
    }

    // ── Camera Follow API ────────────────────────────────────────────────────

    /// Instantly jump camera to a world position (call from UI when selecting a character)
    public void JumpToWorldPos(Vector3 worldPos)
    {
        if (mainCamera == null) return;
        mainCamera.transform.position = new Vector3(worldPos.x, worldPos.y, mainCamera.transform.position.z);
        followTarget = false;
        _followTarget = null;
    }

    /// Jump to a tile position
    public void JumpToTile(Vector2Int tile)
    {
        JumpToWorldPos(new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f));
    }

    /// Smoothly follow a transform (pass character's transform)
    /// snapNow=true to instantly center camera, false to drift in smoothly
    public void FollowTransform(Transform t, bool snapNow = false)
    {
        _followTarget = t;
        followTarget  = t != null;
        if (t != null && snapNow)
            mainCamera.transform.position = new Vector3(
                t.position.x, t.position.y, mainCamera.transform.position.z);
    }

    public void StopFollowing()
    {
        _followTarget = null;
        followTarget  = false;
    }

    void Update()
    {
        if (!_ready || mainCamera == null) return;

        if (followTarget && _followTarget != null)
            SmoothFollow();
        else
            HandleCameraInput();
    }

    void SmoothFollow()
    {
        if (_followTarget == null) { StopFollowing(); return; }

        Vector3 target = new Vector3(_followTarget.position.x, _followTarget.position.y,
                                     mainCamera.transform.position.z);
        mainCamera.transform.position = Vector3.Lerp(
            mainCamera.transform.position, target, followSmoothing * Time.unscaledDeltaTime);

        // Allow zoom while following
#if ENABLE_LEGACY_INPUT_MANAGER
        float scroll = Input.GetAxis("Mouse ScrollWheel");
#else
        float scroll = 0f;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null) scroll = mouse.scroll.ReadValue().y * 0.01f;
#endif
        if (Mathf.Abs(scroll) > 0.001f)
            mainCamera.orthographicSize = Mathf.Clamp(
                mainCamera.orthographicSize - scroll * zoomSpeed, minZoom, maxZoom);
    }

    void HandleCameraInput()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        float h      = Input.GetAxis("Horizontal");
        float v      = Input.GetAxis("Vertical");
        float scroll = Input.GetAxis("Mouse ScrollWheel");
#else
        // New Input System: use keyboard/mouse directly
        float h      = 0f, v = 0f, scroll = 0f;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v += 1f;
        }
        if (mouse != null) scroll = mouse.scroll.ReadValue().y * 0.01f;
#endif
        if (h != 0f || v != 0f)
        {
            float speed = panSpeed * (mainCamera.orthographicSize / 15f) * Time.unscaledDeltaTime;
            mainCamera.transform.Translate(new Vector3(h, v, 0f) * speed, Space.World);
        }
        if (Mathf.Abs(scroll) > 0.001f)
            mainCamera.orthographicSize = Mathf.Clamp(
                mainCamera.orthographicSize - scroll * zoomSpeed, minZoom, maxZoom);

        float halfH = mainCamera.orthographicSize;
        float halfW = halfH * mainCamera.aspect;
        float cx = Mathf.Clamp(mainCamera.transform.position.x, halfW,  WorldMap.WIDTH  - halfW);
        float cy = Mathf.Clamp(mainCamera.transform.position.y, halfH, WorldMap.HEIGHT - halfH);
        mainCamera.transform.position = new Vector3(cx, cy, mainCamera.transform.position.z);
    }
}
