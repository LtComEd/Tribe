using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central 400x400 world data store. Pure data — no rendering here.
/// WorldRenderer reads from this to drive the Tilemap.
/// </summary>
[DefaultExecutionOrder(-90)]
public class WorldMap : MonoBehaviour
{
    public static WorldMap Instance { get; private set; }

    public const int WIDTH  = 400;
    public const int HEIGHT = 400;

    private TileData[,] _tiles;
    private Dictionary<int, BuildingData> _buildings = new();
    private int _nextBuildingId = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _tiles = new TileData[WIDTH, HEIGHT];
    }

    // ── Tile Access ─────────────────────────────────────────────────────────

    public TileData GetTile(int x, int y)
    {
        if (!InBounds(x, y)) return null;
        return _tiles[x, y];
    }

    public TileData GetTile(Vector2Int pos) => GetTile(pos.x, pos.y);

    public void SetTile(int x, int y, TileData tile)
    {
        if (!InBounds(x, y)) return;
        _tiles[x, y] = tile;
    }

    public bool InBounds(int x, int y) =>
        x >= 0 && x < WIDTH && y >= 0 && y < HEIGHT;

    // ── Neighbour Queries (used by pathfinder) ───────────────────────────────

    private static readonly Vector2Int[] CardinalDirs =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };
    private static readonly Vector2Int[] AllDirs =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
        new Vector2Int(1,1), new Vector2Int(-1,1),
        new Vector2Int(1,-1), new Vector2Int(-1,-1)
    };

    public List<TileData> GetNeighbours(int x, int y, bool diagonal = false)
    {
        var dirs = diagonal ? AllDirs : CardinalDirs;
        var result = new List<TileData>(dirs.Length);
        foreach (var d in dirs)
        {
            var t = GetTile(x + d.x, y + d.y);
            if (t != null) result.Add(t);
        }
        return result;
    }

    // ── Area Queries ─────────────────────────────────────────────────────────

    /// Returns tiles within Chebyshev distance (square radius)
    public List<TileData> GetTilesInRadius(Vector2Int center, int radius)
    {
        var result = new List<TileData>();
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            var t = GetTile(center.x + dx, center.y + dy);
            if (t != null) result.Add(t);
        }
        return result;
    }

    /// Find the nearest passable tile with a given resource type
    public TileData FindNearestResource(Vector2Int origin, ResourceType type, int searchRadius = 50)
    {
        TileData best = null;
        float bestDist = float.MaxValue;

        foreach (var tile in GetTilesInRadius(origin, searchRadius))
        {
            if (!tile.IsPassable) continue;
            foreach (var res in tile.resources)
            {
                if (res.resourceType == type && !res.isDepleted)
                {
                    float d = Vector2Int.Distance(origin, tile.Position);
                    if (d < bestDist) { bestDist = d; best = tile; }
                }
            }
        }
        return best;
    }

    /// Find a suitable build location near origin
    public TileData FindBuildableTile(Vector2Int origin, int searchRadius = 10)
    {
        // Search outward in rings
        for (int r = 0; r <= searchRadius; r++)
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        {
            if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // ring only
            var t = GetTile(origin.x + dx, origin.y + dy);
            if (t != null && t.IsBuildable) return t;
        }
        return null;
    }

    // ── Buildings ────────────────────────────────────────────────────────────

    public int PlaceBuilding(BuildingData building, Vector2Int pos)
    {
        var tile = GetTile(pos);
        if (tile == null || !tile.IsBuildable) return -1;

        building.id = _nextBuildingId++;
        building.position = pos;
        _buildings[building.id] = building;
        tile.buildingId = building.id;
        return building.id;
    }

    public BuildingData GetBuilding(int id) =>
        _buildings.TryGetValue(id, out var b) ? b : null;

    public BuildingData GetBuildingAt(Vector2Int pos)
    {
        var tile = GetTile(pos);
        if (tile == null || tile.buildingId < 0) return null;
        return GetBuilding(tile.buildingId);
    }

    public void RemoveBuilding(int id)
    {
        if (!_buildings.TryGetValue(id, out var b)) return;
        var tile = GetTile(b.position);
        if (tile != null) tile.buildingId = -1;
        _buildings.Remove(id);
    }

    // ── Tick: Resource Regeneration ──────────────────────────────────────────

    void Start()
    {
        // Subscribe here (Start runs after all Awakes, so SimulationManager.Instance is set)
        if (SimulationManager.Instance)
            SimulationManager.Instance.OnDayTick += RegenerateResources;
        else
            Debug.LogWarning("[WorldMap] SimulationManager not found — resource regen disabled.");
    }

    void OnDestroy()
    {
        if (SimulationManager.Instance)
            SimulationManager.Instance.OnDayTick -= RegenerateResources;
    }

    void RegenerateResources()
    {
        for (int x = 0; x < WIDTH; x++)
        for (int y = 0; y < HEIGHT; y++)
        {
            var tile = _tiles[x, y];
            if (tile == null) continue;
            foreach (var res in tile.resources)
                res.Regenerate(1f); // 1 day delta
        }
    }
}
