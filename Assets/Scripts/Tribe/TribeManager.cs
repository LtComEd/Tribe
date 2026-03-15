using System.Collections.Generic;
using UnityEngine;

// ── TribeManager ─────────────────────────────────────────────────────────────

public class TribeManager : MonoBehaviour
{
    public static TribeManager Instance { get; private set; }

    [Header("References — auto-found if null")]
    public WorldGenerator worldGenerator;
    public GameObject     characterPrefab;

    [Header("Starting Population")]
    public int startingCount = 10;

    [Header("Name Lists")]
    public string[] maleFirstNames   = { "Aldric","Bran","Cedric","Duncan","Edmund",
                                         "Finn","Gareth","Harold","Ivar","Jorik" };
    public string[] femaleFirstNames = { "Aelith","Brigit","Cora","Della","Edda",
                                         "Freya","Gwen","Hilda","Ingrid","Jorunn" };
    public string[] lastNames        = { "Ashwood","Blackthorn","Coldbrook","Dunmoor",
                                         "Elmhurst","Frostholm","Greymount","Harrow",
                                         "Ironside","Kettlebrook" };

    public List<CharacterController2D> Characters { get; private set; } = new List<CharacterController2D>();
    public ResourceInventory Inventory { get; private set; } = new ResourceInventory();

    private int           _nextCharId = 0;
    private System.Random _rng;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _rng = new System.Random(42);
    }

    void Start()
    {
        // Self-heal
        if (worldGenerator == null)
            worldGenerator = FindFirstObjectByType<WorldGenerator>();

        // If characterPrefab still null after scene load, try to load from Resources or Prefabs
        if (characterPrefab == null)
        {
            characterPrefab = LoadCharacterPrefab();
            if (characterPrefab == null)
                Debug.LogWarning("[TribeManager] characterPrefab not assigned and not found. Characters will not spawn.");
        }

        if (worldGenerator != null)
            worldGenerator.OnWorldReady += OnWorldReady;
        else
            Debug.LogError("[TribeManager] WorldGenerator not found!");

        Debug.Log("[TribeManager] Start — worldGenerator=" + (worldGenerator != null ? "OK" : "NULL")
                + " characterPrefab=" + (characterPrefab != null ? "OK" : "NULL"));
    }

    GameObject LoadCharacterPrefab()
    {
        // Try loading from Assets/Prefabs/Character.prefab via Resources folder
        var loaded = Resources.Load<GameObject>("Character");
        if (loaded != null) { Debug.Log("[TribeManager] Loaded prefab from Resources."); return loaded; }
        return null;
    }

    void OnWorldReady(Vector2Int startPos)
    {
        Debug.Log("[TribeManager] OnWorldReady at " + startPos);

        Inventory.Add(ResourceType.Wood,    100f);
        Inventory.Add(ResourceType.Stone,   50f);
        Inventory.Add(ResourceType.RawMeat, 30f);
        Inventory.Add(ResourceType.Berries, 20f);

        BuildingManager.Instance?.PlaceBuilding(BuildingType.CampFire, startPos, skipResourceCheck: true);

        if (characterPrefab == null)
        {
            Debug.LogError("[TribeManager] Cannot spawn — characterPrefab is null. Assign it in Inspector or place prefab in Assets/Resources/Character.prefab");
            EventLog.Log("The tribe arrived but found no shelter. (characterPrefab missing)");
            return;
        }

        for (int i = 0; i < startingCount; i++)
        {
            Vector2Int spawnPos = startPos + new Vector2Int(_rng.Next(-3, 4), _rng.Next(-3, 4));
            var tile = WorldMap.Instance?.GetTile(spawnPos);
            if (tile == null || !tile.IsPassable) spawnPos = startPos;
            SpawnCharacter(spawnPos);
        }

        EventLog.Log("The tribe has settled near (" + startPos.x + ", " + startPos.y + "). May they endure.");
    }

    public CharacterController2D SpawnCharacter(Vector2Int tilePos, CharacterSheet sheet = null)
    {
        if (characterPrefab == null) { Debug.LogError("[TribeManager] characterPrefab null"); return null; }

        var worldPos = new Vector3(tilePos.x + 0.5f, tilePos.y + 0.5f, 0f);
        var go       = Instantiate(characterPrefab, worldPos, Quaternion.identity, transform);

        if (sheet == null) sheet = GenerateRandomSheet();

        var ctrl = go.GetComponent<CharacterController2D>();
        if (ctrl == null) { Debug.LogError("[TribeManager] CharacterController2D missing on prefab!"); return null; }

        ctrl.Initialise(sheet);
        Characters.Add(ctrl);
        Debug.Log("[TribeManager] Spawned: " + sheet.FullName);
        EventLog.Log(sheet.FullName + " joins the tribe (age " + (int)sheet.age + ", " + sheet.gender + ").");
        return ctrl;
    }

    CharacterSheet GenerateRandomSheet()
    {
        bool   isMale = _rng.NextDouble() > 0.5;
        string[] names = isMale ? maleFirstNames : femaleFirstNames;

        var sheet = new CharacterSheet
        {
            id        = _nextCharId++,
            firstName = names[_rng.Next(names.Length)],
            lastName  = lastNames[_rng.Next(lastNames.Length)],
            gender    = isMale ? Gender.Male : Gender.Female,
            age       = _rng.Next(18, 40)
        };
        sheet.RollStats(_rng);

        sheet.Aggression = (float)_rng.NextDouble();
        sheet.Ambition   = (float)_rng.NextDouble();
        sheet.Loyalty    = (float)_rng.NextDouble() * 0.5f + 0.3f;
        sheet.Curiosity  = (float)_rng.NextDouble();
        sheet.Empathy    = (float)_rng.NextDouble();
        sheet.Caution    = (float)_rng.NextDouble();

        SkillType[] pool = { SkillType.Farming, SkillType.Hunting, SkillType.Woodcutting,
                             SkillType.Foraging, SkillType.Carpentry, SkillType.Cooking };
        sheet.Skills[pool[_rng.Next(pool.Length)]] = _rng.Next(20, 50);
        sheet.Skills[pool[_rng.Next(pool.Length)]] = _rng.Next(5,  20);

        return sheet;
    }

    public void TriggerBirth(CharacterSheet mother, CharacterController2D motherCtrl)
    {
        mother.isPregnant         = false;
        mother.pregnancyProgress  = 0f;

        CharacterSheet father = null;
        if (mother.pregnancyFatherId >= 0) father = FindSheet(mother.pregnancyFatherId);

        bool   isMale  = _rng.NextDouble() > 0.5;
        string[] names = isMale ? maleFirstNames : femaleFirstNames;

        var baby = new CharacterSheet
        {
            id        = _nextCharId++,
            age       = 0f,
            gender    = isMale ? Gender.Male : Gender.Female,
            firstName = names[_rng.Next(names.Length)],
            lastName  = mother.lastName,
            motherId  = mother.id,
            fatherId  = father?.id ?? -1
        };

        if (father != null) baby.InheritStats(mother, father, _rng);
        else                baby.RollStats(_rng);

        baby.Hunger = 90f;
        mother.childIds.Add(baby.id);

        float riskRoll  = (float)_rng.NextDouble();
        float deathRisk = 0.05f - mother.ConMod * 0.01f;
        if (riskRoll < deathRisk)
        {
            EventLog.Log(mother.FullName + " died in childbirth.");
            motherCtrl.Die("childbirth complications");
        }
        else
        {
            mother.AddStatus(StatusEffect.Wounded);
        }

        SpawnCharacter(motherCtrl.CurrentTilePos, baby);
        EventLog.Log("A child is born — " + baby.FullName + ", child of " + mother.FullName + ".");
    }

    public void OnCharacterDeath(CharacterSheet sheet, CharacterController2D ctrl)
    {
        Characters.Remove(ctrl);
        if (sheet.spouseId >= 0) FindSheet(sheet.spouseId)?.AddStatus(StatusEffect.Grieving);
    }

    public CharacterSheet FindSheet(int id)
    {
        foreach (var c in Characters)
            if (c != null && c.Sheet != null && c.Sheet.id == id) return c.Sheet;
        return null;
    }

    public CharacterController2D FindController(int id)
    {
        foreach (var c in Characters)
            if (c != null && c.Sheet != null && c.Sheet.id == id) return c;
        return null;
    }

    public int Population { get { return Characters.Count; } }
}

// ── ResourceInventory ─────────────────────────────────────────────────────────

public class ResourceInventory
{
    private Dictionary<ResourceType, float> _stock = new Dictionary<ResourceType, float>();

    public void Add(ResourceType type, float amount)
    {
        if (!_stock.ContainsKey(type)) _stock[type] = 0f;
        _stock[type] += amount;
    }

    public bool Remove(ResourceType type, float amount)
    {
        if (GetAmount(type) < amount) return false;
        _stock[type] -= amount;
        return true;
    }

    public float GetAmount(ResourceType type)
    {
        float val;
        _stock.TryGetValue(type, out val);
        return val;
    }

    public bool Has(ResourceType type, float amount) { return GetAmount(type) >= amount; }

    public Dictionary<ResourceType, float> GetAll() { return new Dictionary<ResourceType, float>(_stock); }
}

// ── EventLog ──────────────────────────────────────────────────────────────────

public static class EventLog
{
    public static event System.Action<string> OnNewEntry;
    private static readonly List<string> _history = new List<string>();
    public static System.Collections.ObjectModel.ReadOnlyCollection<string> History
    {
        get { return _history.AsReadOnly(); }
    }

    public static void Log(string message)
    {
        string ts    = SimulationManager.Instance != null ? "[" + SimulationManager.Instance.GetDateString() + "] " : "";
        string entry = ts + message;
        _history.Add(entry);
        OnNewEntry?.Invoke(entry);
        Debug.Log("[EventLog] " + entry);
    }
}

// ── UIManager ─────────────────────────────────────────────────────────────────

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private OnScreenUI _onScreenUI;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _onScreenUI = GetComponent<OnScreenUI>();
        if (_onScreenUI == null) _onScreenUI = FindFirstObjectByType<OnScreenUI>();
        if (_onScreenUI == null) _onScreenUI = gameObject.AddComponent<OnScreenUI>();
    }

    public void ShowCharacterSheet(CharacterController2D ctrl)
    {
        _onScreenUI?.SelectCharacter(ctrl);
    }

    public void HideCharacterSheet()
    {
        _onScreenUI?.SelectCharacter(null);
    }
}
