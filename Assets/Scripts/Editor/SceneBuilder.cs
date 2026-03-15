using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class SceneBuilder
{
    [MenuItem("TribeGame/0. Verify Menu Works")]
    public static void VerifyMenu()
    {
        Debug.Log("[SceneBuilder] Menu OK.");
        EditorUtility.DisplayDialog("OK", "Menu is working.", "OK");
    }

    [MenuItem("TribeGame/1. Build Scene")]
    public static void BuildScene()
    {
        Debug.Log("[SceneBuilder] BUILD STARTED");
        try
        {
            // Clean
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(c.gameObject);
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(l.gameObject);

            // Camera
            var camGO = CreateGO("Main Camera");
            camGO.tag = "MainCamera";
            camGO.AddComponent<AudioListener>();
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 20f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.08f, 0.10f, 0.06f);
            cam.nearClipPlane    = -200f;
            cam.farClipPlane     =  200f;
            cam.transform.position = new Vector3(200f, 200f, -10f);
            TryAddURPData(camGO);

            // Grid + Tilemaps
            var gridGO = CreateGO("Grid");
            gridGO.AddComponent<Grid>().cellSize = Vector3.one;
            var terrainTM  = AddTilemap(gridGO, "TerrainTilemap",  0);
            var buildingTM = AddTilemap(gridGO, "BuildingTilemap", 1);
            var fogTM      = AddTilemap(gridGO, "FogTilemap",      2);

            // Systems
            var simGO      = CreateGO<SimulationManager>("SimulationManager");
            var worldMapGO = CreateGO<WorldMap>("WorldMap");
            var worldGenGO = CreateGO<WorldGenerator>("WorldGenerator");
            var rendererGO = CreateGO<WorldRenderer>("WorldRenderer");
            var tribeGO    = CreateGO<TribeManager>("TribeManager");
            var buildingGO = CreateGO<BuildingManager>("BuildingManager");
            var uiGO       = CreateGO<UIManager>("UIManager");
            uiGO.AddComponent<OnScreenUI>();
            var bootGO     = CreateGO<GameBootstrap>("GameManager");

            // Wire
            var wg   = worldGenGO.GetComponent<WorldGenerator>();
            var wr   = rendererGO.GetComponent<WorldRenderer>();
            var wm   = worldMapGO.GetComponent<WorldMap>();
            var tm   = tribeGO.GetComponent<TribeManager>();
            var bm   = buildingGO.GetComponent<BuildingManager>();
            var boot = bootGO.GetComponent<GameBootstrap>();
            var sim  = simGO.GetComponent<SimulationManager>();

            wg.worldMap        = wm;
            wg.worldRenderer   = wr;
            wr.terrainTilemap  = terrainTM;
            wr.buildingTilemap = buildingTM;
            wr.fogTilemap      = fogTM;
            wr.mainCamera      = cam;
            tm.worldGenerator  = wg;
            bm.worldMap        = wm;
            bm.worldRenderer   = wr;
            boot.simulationManager = sim;
            boot.worldGenerator    = wg;
            boot.tribeManager      = tm;

            EditorUtility.SetDirty(wg);
            EditorUtility.SetDirty(wr);
            EditorUtility.SetDirty(tm);
            EditorUtility.SetDirty(bm);
            EditorUtility.SetDirty(boot);

            // Character prefab — sprite saved as PNG asset so it persists
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Sprites"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Sprites");

            // Save the circle texture as a real PNG asset
            string spritePath = "Assets/Prefabs/Sprites/CharacterSprite.png";
            SaveCircleSpritePNG(spritePath, 32, new Color(0.9f, 0.75f, 0.55f));
            AssetDatabase.Refresh();

            // Load the saved sprite back as an asset
            var spriteAsset = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (spriteAsset == null)
            {
                // Set import settings so Unity treats it as a sprite
                var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType        = TextureImporterType.Sprite;
                    importer.spriteImportMode   = SpriteImportMode.Single;
                    importer.filterMode         = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
                AssetDatabase.Refresh();
                spriteAsset = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            }

            var charGO = new GameObject("Character");
            var sr = charGO.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Default";
            sr.sortingOrder     = 10;
            sr.sprite           = spriteAsset;
            charGO.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            charGO.AddComponent<CharacterController2D>();
            charGO.AddComponent<BrainController>();
            var col = charGO.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius    = 0.4f;

            string prefabPath = "Assets/Prefabs/Character.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(charGO, prefabPath);
            UnityEngine.Object.DestroyImmediate(charGO);
            tm.characterPrefab = prefab;
            EditorUtility.SetDirty(tm);

            // Also copy to Resources for runtime fallback loading
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CopyAsset(prefabPath, "Assets/Resources/Character.prefab");
            AssetDatabase.Refresh();

            Debug.Log("[SceneBuilder] Prefab saved: " + prefabPath
                    + "  sprite=" + (spriteAsset != null ? "OK" : "NULL"));

            // Save scene
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            string scenePath = SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(scenePath))
                scenePath = EditorUtility.SaveFilePanelInProject("Save Scene", "MainScene", "unity", "Save scene");
            if (!string.IsNullOrEmpty(scenePath))
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);

            Debug.Log("[SceneBuilder] BUILD COMPLETE");
            EditorUtility.DisplayDialog("Done!",
                "Scene built.\n\nPress PLAY.\n\n" +
                "Controls:\nWASD = Pan camera\nScroll = Zoom\nSpace = Pause\n1/2/3/4 = Speed\n" +
                "Click name in list = select + follow\n>> Go = jump to character",
                "Play!");
        }
        catch (Exception e)
        {
            Debug.LogError("[SceneBuilder] FAILED: " + e.Message + "\n" + e.StackTrace);
            EditorUtility.DisplayDialog("Failed", e.Message, "OK");
        }
    }

    [MenuItem("TribeGame/2. Print Scene Status")]
    public static void PrintStatus()
    {
        Debug.Log("=== Scene Status ===");
        Check<SimulationManager>("SimulationManager");
        Check<WorldMap>("WorldMap");
        Check<WorldGenerator>("WorldGenerator");
        Check<WorldRenderer>("WorldRenderer");
        Check<TribeManager>("TribeManager");
        Check<BuildingManager>("BuildingManager");
        Check<UIManager>("UIManager");
        Check<GameBootstrap>("GameBootstrap");
        Check<Camera>("Camera");
        Check<Grid>("Grid");
        var wr = UnityEngine.Object.FindFirstObjectByType<WorldRenderer>();
        if (wr != null)
        {
            Debug.Log("  terrainTilemap  = " + R(wr.terrainTilemap));
            Debug.Log("  buildingTilemap = " + R(wr.buildingTilemap));
            Debug.Log("  mainCamera      = " + R(wr.mainCamera));
        }
        var tm = UnityEngine.Object.FindFirstObjectByType<TribeManager>();
        if (tm != null)
        {
            Debug.Log("  characterPrefab = " + R(tm.characterPrefab));
            if (tm.characterPrefab != null)
            {
                var prefabSR = tm.characterPrefab.GetComponent<SpriteRenderer>();
                Debug.Log("  prefab.sprite   = " + (prefabSR != null && prefabSR.sprite != null ? "OK " + prefabSR.sprite.name : "NULL !!!"));
            }
        }
        Debug.Log("===================");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject CreateGO(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        return go;
    }

    static GameObject CreateGO<T>(string name) where T : Component
    {
        var go = CreateGO(name);
        go.AddComponent<T>();
        return go;
    }

    static Tilemap AddTilemap(GameObject parent, string name, int order)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent.transform);
        var tm = go.AddComponent<Tilemap>();
        var tr = go.AddComponent<TilemapRenderer>();
        tr.sortingOrder = order;
        return tm;
    }

    /// Save a circle sprite as a real PNG file on disk so Unity can import it as a sprite asset.
    static void SaveCircleSpritePNG(string assetPath, int size, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px  = new Color[size * size];
        float cx = size / 2f - 0.5f, r = cx - 1f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx, dy = y - cx;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist <= r)
            {
                // Slightly darker outline ring
                float alpha = dist > r - 1.5f ? 1f - (dist - (r - 1.5f)) / 1.5f : 1f;
                Color c = dist > r - 2f ? color * 0.7f : color;
                c.a = alpha;
                px[y * size + x] = c;
            }
            else
            {
                px[y * size + x] = Color.clear;
            }
        }

        tex.SetPixels(px);
        tex.Apply();

        string fullPath = Application.dataPath + "/../" + assetPath;
        System.IO.File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    static void TryAddURPData(GameObject go)
    {
        string[] names = {
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime",
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, UnityEngine.Rendering.Universal"
        };
        foreach (var n in names)
        {
            var t = Type.GetType(n);
            if (t != null && go.GetComponent(t) == null) { go.AddComponent(t); return; }
        }
    }

    static void Check<T>(string label) where T : Component
    {
        var o = UnityEngine.Object.FindFirstObjectByType<T>();
        Debug.Log(o != null ? "  OK  " + label : "  !!  MISSING: " + label);
    }

    static string R(UnityEngine.Object o) => o != null ? "OK  " + o.name : "!! NULL";
}
