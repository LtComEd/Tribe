using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Generates placeholder tile and sprite assets at runtime using solid colors.
/// Attach to any GameObject. Call GenerateAll() once before WorldRenderer uses tiles.
///
/// This lets the game boot and run visually without any imported art.
/// Swap textures for real sprites at any time — nothing else changes.
/// </summary>
[CreateAssetMenu(fileName = "TilePalette", menuName = "TribeGame/Tile Palette")]
public class TilePalette : ScriptableObject
{
    // Filled at runtime by TileTextureFactory, OR assigned in Inspector with real sprites
    [Header("Terrain Tiles (auto-filled if null)")]
    public Tile tileDeepWater;
    public Tile tileShallowWater;
    public Tile tileSand;
    public Tile tileGrass;
    public Tile tileForest;
    public Tile tileMountain;
    public Tile tileSnow;
    public Tile tileSwamp;
    public Tile tileDesert;
    public Tile tileFarmland;

    [Header("Overlay Tiles")]
    public Tile fogUnexplored;
    public Tile fogRemembered;

    [Header("Character Sprites (auto-filled if null)")]
    public Sprite spriteAdult;
    public Sprite spriteChild;
    public Sprite spriteElder;
}

/// <summary>
/// Generates all tile and sprite assets procedurally.
/// Call GenerateAll() during bootstrap before world generation starts.
/// </summary>
public static class TileTextureFactory
{
    const int TILE_SIZE = 16; // pixels per tile texture

    // ── Public API ────────────────────────────────────────────────────────────

    public static void GenerateAll(TilePalette palette)
    {
        if (palette == null) { Debug.LogError("[TileFactory] Palette is null"); return; }

        // Terrain tiles — each gets a distinct readable color
        palette.tileDeepWater    = palette.tileDeepWater    ?? MakeTile(new Color(0.10f, 0.18f, 0.45f));
        palette.tileShallowWater = palette.tileShallowWater ?? MakeTile(new Color(0.26f, 0.45f, 0.75f));
        palette.tileSand         = palette.tileSand         ?? MakeTile(new Color(0.85f, 0.80f, 0.55f));
        palette.tileGrass        = palette.tileGrass        ?? MakeTile(new Color(0.35f, 0.62f, 0.25f));
        palette.tileForest       = palette.tileForest       ?? MakeForestTile();
        palette.tileMountain     = palette.tileMountain     ?? MakeTile(new Color(0.50f, 0.47f, 0.42f));
        palette.tileSnow         = palette.tileSnow         ?? MakeTile(new Color(0.90f, 0.93f, 0.97f));
        palette.tileSwamp        = palette.tileSwamp        ?? MakeTile(new Color(0.28f, 0.38f, 0.22f));
        palette.tileDesert       = palette.tileDesert       ?? MakeTile(new Color(0.90f, 0.74f, 0.40f));
        palette.tileFarmland     = palette.tileFarmland     ?? MakeFarmlandTile();

        // Fog overlays
        palette.fogUnexplored    = palette.fogUnexplored    ?? MakeTile(new Color(0f, 0f, 0f, 1f));
        palette.fogRemembered    = palette.fogRemembered    ?? MakeTile(new Color(0f, 0f, 0f, 0.55f));

        // Character sprites
        palette.spriteAdult  = palette.spriteAdult  ?? MakeCharacterSprite(new Color(0.9f, 0.75f, 0.55f));
        palette.spriteChild  = palette.spriteChild  ?? MakeCharacterSprite(new Color(0.8f, 0.65f, 0.45f), 0.7f);
        palette.spriteElder  = palette.spriteElder  ?? MakeCharacterSprite(new Color(0.75f, 0.72f, 0.70f));

        Debug.Log("[TileFactory] Placeholder tiles generated.");
    }

    // ── Tile Builders ─────────────────────────────────────────────────────────

    static Tile MakeTile(Color color)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = SpriteFromTexture(SolidTexture(color));
        tile.colliderType = Tile.ColliderType.None;
        return tile;
    }

    /// Forest: dark green base with slightly lighter pixel noise for tree feel
    static Tile MakeForestTile()
    {
        var tex = new Texture2D(TILE_SIZE, TILE_SIZE, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp
        };

        var baseColor  = new Color(0.18f, 0.42f, 0.15f);
        var canopyColor = new Color(0.12f, 0.35f, 0.10f);

        var pixels = new Color[TILE_SIZE * TILE_SIZE];
        var rng = new System.Random(1);
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = rng.NextDouble() < 0.35 ? canopyColor : baseColor;

        tex.SetPixels(pixels);
        tex.Apply();

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = SpriteFromTexture(tex);
        tile.colliderType = Tile.ColliderType.None;
        return tile;
    }

    /// Farmland: brown with row lines
    static Tile MakeFarmlandTile()
    {
        var tex = new Texture2D(TILE_SIZE, TILE_SIZE, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp
        };

        var soil  = new Color(0.50f, 0.35f, 0.18f);
        var furrow = new Color(0.38f, 0.25f, 0.12f);

        var pixels = new Color[TILE_SIZE * TILE_SIZE];
        for (int y = 0; y < TILE_SIZE; y++)
        for (int x = 0; x < TILE_SIZE; x++)
            pixels[y * TILE_SIZE + x] = (y % 3 == 0) ? furrow : soil;

        tex.SetPixels(pixels);
        tex.Apply();

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = SpriteFromTexture(tex);
        tile.colliderType = Tile.ColliderType.None;
        return tile;
    }

    // ── Character Sprite ──────────────────────────────────────────────────────

    /// Simple silhouette: circle head + rectangle body
    static Sprite MakeCharacterSprite(Color skinColor, float scale = 1f)
    {
        int size = 16;
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp
        };

        var pixels = new Color[size * size];
        // transparent background
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        Color bodyColor  = new Color(skinColor.r * 0.6f, skinColor.g * 0.5f, skinColor.b * 0.4f);
        Color shirtColor = new Color(0.3f, 0.45f, 0.7f);

        // Head: rows 10-14, cols 5-10
        for (int y = 10; y <= 14; y++)
        for (int x = 5;  x <= 10; x++)
            pixels[y * size + x] = skinColor;

        // Body/shirt: rows 4-9, cols 4-11
        for (int y = 4; y <= 9; y++)
        for (int x = 4; x <= 11; x++)
            pixels[y * size + x] = shirtColor;

        // Legs: rows 0-3
        for (int y = 0; y <= 3; y++)
        {
            pixels[y * size + 5]  = bodyColor;
            pixels[y * size + 6]  = bodyColor;
            pixels[y * size + 9]  = bodyColor;
            pixels[y * size + 10] = bodyColor;
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), size / scale);
    }

    // ── Texture Helpers ───────────────────────────────────────────────────────

    static Texture2D SolidTexture(Color color)
    {
        var tex = new Texture2D(TILE_SIZE, TILE_SIZE, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp
        };

        var pixels = new Color[TILE_SIZE * TILE_SIZE];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static Sprite SpriteFromTexture(Texture2D tex)
    {
        return Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            tex.width); // PPU = tile size → 1 tile = 1 Unity unit
    }

    // ── TileBase for a specific TileType ─────────────────────────────────────

    public static Tile GetTileFor(TilePalette palette, TileType type)
    {
        switch (type)
        {
            case TileType.DeepWater:    return palette.tileDeepWater;
            case TileType.ShallowWater: return palette.tileShallowWater;
            case TileType.Sand:         return palette.tileSand;
            case TileType.Grass:        return palette.tileGrass;
            case TileType.Forest:       return palette.tileForest;
            case TileType.Mountain:     return palette.tileMountain;
            case TileType.Snow:         return palette.tileSnow;
            case TileType.Swamp:        return palette.tileSwamp;
            case TileType.Desert:       return palette.tileDesert;
            case TileType.Farmland:     return palette.tileFarmland;
            default:                    return palette.tileGrass;
        }
    }
}
