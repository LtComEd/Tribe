using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-70)]
public class WorldGenerator : MonoBehaviour
{
    [Header("References — auto-found if null")]
    public WorldMap     worldMap;
    public WorldRenderer worldRenderer;

    [Header("Seed")]
    public int  seed       = 42;
    public bool randomSeed = true;

    [Header("Noise — Elevation")]
    public float elevationScale      = 80f;
    public int   elevationOctaves    = 5;
    public float elevationPersist    = 0.5f;
    public float elevationLacunarity = 2f;

    [Header("Noise — Moisture")]
    public float moistureScale   = 120f;
    public int   moistureOctaves = 4;

    [Header("Noise — Temperature")]
    public float tempScale = 150f;

    [Header("Thresholds")]
    [Range(0,1)] public float deepWaterLevel    = 0.25f;
    [Range(0,1)] public float shallowWaterLevel = 0.35f;
    [Range(0,1)] public float sandLevel         = 0.40f;
    [Range(0,1)] public float mountainLevel     = 0.72f;
    [Range(0,1)] public float snowLevel         = 0.85f;

    [Header("Resources")]
    public float forestDensity = 0.25f;
    public float stoneDensity  = 0.15f;
    public float oreDensity    = 0.05f;

    [Header("Starting Area")]
    public int startSearchRadius = 80;

    public System.Action<Vector2Int> OnWorldReady;

    public void Generate()
    {
        // Self-heal references
        if (worldMap      == null) worldMap      = WorldMap.Instance;
        if (worldRenderer == null) worldRenderer = FindFirstObjectByType<WorldRenderer>();

        Debug.Log("[WorldGen] Generate() — worldMap=" + (worldMap != null ? "OK" : "NULL")
                + " worldRenderer=" + (worldRenderer != null ? "OK" : "NULL"));

        if (worldMap == null)
        {
            Debug.LogError("[WorldGen] WorldMap is null — cannot generate.");
            return;
        }
        if (worldRenderer == null)
        {
            Debug.LogError("[WorldGen] WorldRenderer is null — cannot generate.");
            return;
        }

        if (randomSeed) seed = Random.Range(0, 999999);
        Random.InitState(seed);
        Debug.Log("[WorldGen] Seed: " + seed);

        StartCoroutine(GenerateCoroutine());
    }

    IEnumerator GenerateCoroutine()
    {
        Debug.Log("[WorldGen] Building noise maps...");
        var elev  = BuildNoiseMap(seed,        elevationScale,  elevationOctaves, elevationPersist, elevationLacunarity);
        var moist = BuildNoiseMap(seed + 1000, moistureScale,   moistureOctaves,  0.5f, 2f);
        var temp  = BuildNoiseMap(seed + 2000, tempScale,       3,                0.5f, 2f);
        yield return null;

        Debug.Log("[WorldGen] Assigning tiles...");
        for (int x = 0; x < WorldMap.WIDTH; x++)
        {
            if (x == 0 || x == 100 || x == 200 || x == 300 || x == 399)
                Debug.Log("[WorldGen] Tile column: " + x + "/400");
            for (int y = 0; y < WorldMap.HEIGHT; y++)
            {
                float latitudeTemp = 1f - (float)y / WorldMap.HEIGHT;
                float finalTemp    = Mathf.Clamp01(temp[x,y] * 0.6f + latitudeTemp * 0.4f);

                var tile = new TileData
                {
                    x           = x,
                    y           = y,
                    elevation   = elev[x,y],
                    moisture    = moist[x,y],
                    temperature = finalTemp
                };
                tile.type      = ClassifyTile(tile);
                tile.fertility = ComputeFertility(tile);
                SeedResources(tile);
                worldMap.SetTile(x, y, tile);
            }
            if (x % 20 == 0) yield return null;
        }
        Debug.Log("[WorldGen] Tile assignment done.");

        Debug.Log("[WorldGen] Finding start location...");
        Vector2Int startPos = FindStartLocation();
        yield return null;

        Debug.Log("[WorldGen] Rendering world...");
        worldRenderer.RenderAll();
        yield return null;

        Debug.Log("[WorldGen] Done. StartPos=" + startPos);
        OnWorldReady?.Invoke(startPos);
    }

    TileType ClassifyTile(TileData t)
    {
        float e = t.elevation, m = t.moisture, tp = t.temperature;
        if (e < deepWaterLevel)    return TileType.DeepWater;
        if (e < shallowWaterLevel) return TileType.ShallowWater;
        if (e > snowLevel)         return TileType.Snow;
        if (e > mountainLevel)     return TileType.Mountain;
        if (e < sandLevel)
            return (tp > 0.7f && m < 0.3f) ? TileType.Desert : TileType.Sand;
        if (m > 0.65f && tp < 0.45f) return TileType.Swamp;
        if (m > 0.45f)               return TileType.Forest;
        if (m < 0.25f && tp > 0.55f) return TileType.Desert;
        return TileType.Grass;
    }

    float ComputeFertility(TileData t)
    {
        switch (t.type)
        {
            case TileType.Grass:    return Mathf.Clamp01(t.moisture * 0.7f + 0.3f);
            case TileType.Farmland: return 1f;
            case TileType.Forest:   return 0.5f;
            case TileType.Swamp:    return 0.4f;
            default:                return 0f;
        }
    }

    void SeedResources(TileData tile)
    {
        switch (tile.type)
        {
            case TileType.Forest:
                tile.resources.Add(new ResourceNode(ResourceType.Wood,    Random.Range(80f, 200f), 0.5f));
                tile.resources.Add(new ResourceNode(ResourceType.Berries, Random.Range(0f,  30f),  0.2f));
                if (Random.value < 0.15f)
                    tile.resources.Add(new ResourceNode(ResourceType.WildGame, Random.Range(5f, 20f), 0.05f));
                break;
            case TileType.Grass:
                if (Random.value < stoneDensity)
                    tile.resources.Add(new ResourceNode(ResourceType.Flint, Random.Range(10f, 40f), 0.01f));
                if (Random.value < forestDensity * 0.3f)
                    tile.resources.Add(new ResourceNode(ResourceType.Wood,  Random.Range(20f, 60f), 0.1f));
                break;
            case TileType.Mountain:
                tile.resources.Add(new ResourceNode(ResourceType.Stone,   Random.Range(200f, 500f), 0.02f));
                if (Random.value < oreDensity * 2f)
                    tile.resources.Add(new ResourceNode(ResourceType.IronOre, Random.Range(30f, 100f), 0f));
                break;
            case TileType.Sand:
                tile.resources.Add(new ResourceNode(ResourceType.Sand, Random.Range(50f, 150f), 0.5f));
                tile.resources.Add(new ResourceNode(ResourceType.Clay, Random.Range(20f, 80f),  0.1f));
                break;
            case TileType.ShallowWater:
                tile.resources.Add(new ResourceNode(ResourceType.Fish, Random.Range(20f, 60f), 0.3f));
                break;
            case TileType.Swamp:
                tile.resources.Add(new ResourceNode(ResourceType.Clay,  Random.Range(40f, 100f), 0.1f));
                tile.resources.Add(new ResourceNode(ResourceType.Herbs, Random.Range(10f, 40f),  0.2f));
                break;
        }
    }

    Vector2Int FindStartLocation()
    {
        Vector2Int best      = new Vector2Int(WorldMap.WIDTH / 2, WorldMap.HEIGHT / 2);
        float      bestScore = float.MinValue;

        for (int i = 0; i < 200; i++)
        {
            int x = WorldMap.WIDTH  / 2 + Random.Range(-startSearchRadius, startSearchRadius);
            int y = WorldMap.HEIGHT / 2 + Random.Range(-startSearchRadius, startSearchRadius);
            float score = ScoreStartLocation(x, y);
            if (score > bestScore) { bestScore = score; best = new Vector2Int(x, y); }
        }
        return best;
    }

    float ScoreStartLocation(int cx, int cy)
    {
        float score = 0f;
        bool  hasWater = false;

        for (int dx = -8; dx <= 8; dx++)
        for (int dy = -8; dy <= 8; dy++)
        {
            var t = worldMap.GetTile(cx + dx, cy + dy);
            if (t == null) return -9999f;
            switch (t.type)
            {
                case TileType.Grass:        score += 3f;  break;
                case TileType.Forest:       score += 2f;  break;
                case TileType.ShallowWater: score += 1f;  hasWater = true; break;
                case TileType.DeepWater:    score -= 5f;  break;
                case TileType.Mountain:     score -= 2f;  break;
                case TileType.Swamp:        score -= 1f;  break;
            }
            score += t.fertility * 0.5f;
        }
        if (hasWater) score += 10f;

        var center = worldMap.GetTile(cx, cy);
        if (center == null || !center.IsPassable) score -= 50f;
        return score;
    }

    float[,] BuildNoiseMap(int s, float scale, int octaves, float persistence, float lacunarity)
    {
        var map    = new float[WorldMap.WIDTH, WorldMap.HEIGHT];
        var rng    = new System.Random(s);
        var offsetX = new float[octaves];
        var offsetY = new float[octaves];
        for (int i = 0; i < octaves; i++)
        {
            offsetX[i] = rng.Next(-10000, 10000);
            offsetY[i] = rng.Next(-10000, 10000);
        }

        float maxV = float.MinValue, minV = float.MaxValue;
        for (int x = 0; x < WorldMap.WIDTH; x++)
        for (int y = 0; y < WorldMap.HEIGHT; y++)
        {
            float amp = 1f, freq = 1f, val = 0f;
            for (int o = 0; o < octaves; o++)
            {
                float sx = (x + offsetX[o]) / scale * freq;
                float sy = (y + offsetY[o]) / scale * freq;
                val  += Mathf.PerlinNoise(sx, sy) * amp;
                amp  *= persistence;
                freq *= lacunarity;
            }
            map[x,y] = val;
            if (val > maxV) maxV = val;
            if (val < minV) minV = val;
        }

        float range = maxV - minV;
        for (int x = 0; x < WorldMap.WIDTH; x++)
        for (int y = 0; y < WorldMap.HEIGHT; y++)
            map[x,y] = (map[x,y] - minV) / range;

        return map;
    }
}
