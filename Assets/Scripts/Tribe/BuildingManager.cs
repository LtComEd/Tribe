using System.Collections.Generic;
using UnityEngine;

// ── Building Data ─────────────────────────────────────────────────────────────

[System.Serializable]
public class BuildingData
{
    public int id;
    public BuildingType buildingType;
    public Vector2Int position;
    public string name => buildingType.ToString();

    // Construction state
    public bool isConstructed = false;
    public float constructionProgress = 0f;   // 0-1
    public int assignedBuilderId = -1;

    // Operational
    public float condition = 100f;            // degrades over time
    public int occupantLimit;
    public List<int> occupantIds = new();

    // Storage (for warehouses, granaries)
    public Dictionary<ResourceType, float> storage = new();

    public bool HasRoom => occupantIds.Count < occupantLimit;

    public BuildingData(BuildingType type, Vector2Int pos)
    {
        buildingType = type;
        position     = pos;
        var def = BuildingDefinition.Get(type);
        occupantLimit = def.occupantLimit;
    }
}

public enum BuildingType
{
    // Shelter
    Tent, Hut, Longhouse, Cottage,
    // Food
    CampFire, Hearth, GrainStore, Smokehouse, Well,
    // Production
    Woodpile, StoneYard, Forge, PotteryKiln, LoomShed,
    // Agriculture
    FarmPlot, Pen,
    // Defence
    WatchTower, Palisade, Gate,
    // Community
    MeetingHall, Shrine, SchoolHut
}

// ── Building Definition (ScriptableObject-style, kept as static data for now) ─

public class BuildingDefinition
{
    public BuildingType type;
    public string displayName;
    public int occupantLimit;
    public Dictionary<ResourceType, int> buildCost;
    public float buildHours;        // game-hours to construct (1 builder)
    public int minBuilders;
    public string description;

    static readonly Dictionary<BuildingType, BuildingDefinition> _defs;

    static BuildingDefinition()
    {
        _defs = new Dictionary<BuildingType, BuildingDefinition>
        {
            [BuildingType.Tent] = new BuildingDefinition
            {
                type = BuildingType.Tent, displayName = "Tent",
                occupantLimit = 2, buildHours = 2f, minBuilders = 1,
                buildCost = new() { [ResourceType.Wood] = 10 },
                description = "Crude shelter. Keeps out wind, barely."
            },
            [BuildingType.Hut] = new BuildingDefinition
            {
                type = BuildingType.Hut, displayName = "Hut",
                occupantLimit = 4, buildHours = 8f, minBuilders = 1,
                buildCost = new() { [ResourceType.Wood] = 30, [ResourceType.Clay] = 10 },
                description = "A modest dwelling for a small family."
            },
            [BuildingType.CampFire] = new BuildingDefinition
            {
                type = BuildingType.CampFire, displayName = "Camp Fire",
                occupantLimit = 8, buildHours = 0.5f, minBuilders = 1,
                buildCost = new() { [ResourceType.Wood] = 5 },
                description = "Warmth, light, and cooked food. Heart of the camp."
            },
            [BuildingType.GrainStore] = new BuildingDefinition
            {
                type = BuildingType.GrainStore, displayName = "Grain Store",
                occupantLimit = 0, buildHours = 12f, minBuilders = 2,
                buildCost = new() { [ResourceType.Wood] = 50, [ResourceType.Stone] = 20 },
                description = "Stores food supplies for the tribe."
            },
            [BuildingType.WatchTower] = new BuildingDefinition
            {
                type = BuildingType.WatchTower, displayName = "Watch Tower",
                occupantLimit = 2, buildHours = 16f, minBuilders = 2,
                buildCost = new() { [ResourceType.Wood] = 40, [ResourceType.Stone] = 30 },
                description = "Reveals a large area around it. Guards spot threats early."
            },
            [BuildingType.Forge] = new BuildingDefinition
            {
                type = BuildingType.Forge, displayName = "Forge",
                occupantLimit = 2, buildHours = 24f, minBuilders = 2,
                buildCost = new() { [ResourceType.Stone] = 60, [ResourceType.Clay] = 20 },
                description = "Smelt ore into metal. Requires IronOre + Coal."
            },
            [BuildingType.FarmPlot] = new BuildingDefinition
            {
                type = BuildingType.FarmPlot, displayName = "Farm Plot",
                occupantLimit = 4, buildHours = 6f, minBuilders = 1,
                buildCost = new() { [ResourceType.Wood] = 5 },
                description = "Tilled earth for crops. Only on fertile tiles."
            },
        };
    }

    public static BuildingDefinition Get(BuildingType type)
    {
        _defs.TryGetValue(type, out var def);
        return def ?? new BuildingDefinition { type = type, occupantLimit = 2, buildHours = 4f };
    }

    public static IEnumerable<BuildingDefinition> All => _defs.Values;
}

// ── Building Manager ──────────────────────────────────────────────────────────

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    [Header("References")]
    public WorldMap worldMap;
    public WorldRenderer worldRenderer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Placement ─────────────────────────────────────────────────────────────

    /// Try to place a building. Returns the BuildingData or null if invalid.
    public BuildingData PlaceBuilding(BuildingType type, Vector2Int pos, bool skipResourceCheck = false)
    {
        var tile = worldMap.GetTile(pos);
        if (tile == null || !tile.IsBuildable)
        {
            EventLog.Log($"Cannot place {type} at {pos} — tile not buildable.");
            return null;
        }

        // Farm plots require fertile ground
        if (type == BuildingType.FarmPlot && tile.fertility < 0.4f)
        {
            EventLog.Log($"Cannot place farm at {pos} — land too infertile.");
            return null;
        }

        var def = BuildingDefinition.Get(type);
        if (!skipResourceCheck)
        {
            var tribe = TribeManager.Instance;
            foreach (var cost in def.buildCost)
            {
                if (tribe == null || tribe.Inventory.GetAmount(cost.Key) < cost.Value)
                {
                    EventLog.Log($"Not enough {cost.Key} to build {type}.");
                    return null;
                }
            }
            // Deduct resources
            foreach (var cost in def.buildCost)
                tribe.Inventory.Remove(cost.Key, cost.Value);
        }

        var building = new BuildingData(type, pos);
        int id = worldMap.PlaceBuilding(building, pos);
        if (id < 0) return null;

        worldRenderer.PlaceBuildingSprite(building);
        EventLog.Log($"Construction started: {def.displayName} at {pos}.");
        return building;
    }

    /// Called by a builder character each game-hour they work on a site
    public void ProgressConstruction(BuildingData building, CharacterSheet builder, float hours = 1f)
    {
        if (building == null || building.isConstructed) return;

        var def = BuildingDefinition.Get(building.buildingType);
        float progress = hours / def.buildHours;

        // Carpenter skill speeds construction
        float skillBonus = 1f + builder.GetSkill(SkillType.Carpentry) / 200f;
        building.constructionProgress += progress * skillBonus;
        builder.ImproveSkill(SkillType.Carpentry, 0.4f);

        if (building.constructionProgress >= 1f)
        {
            building.constructionProgress = 1f;
            building.isConstructed = true;
            EventLog.Log($"{builder.FullName} completed {building.name} at {building.position}.");
        }
    }

    // ── Decay ─────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (SimulationManager.Instance)
            SimulationManager.Instance.OnMonthTick += DecayBuildings;
    }
    void OnDisable()
    {
        if (SimulationManager.Instance)
            SimulationManager.Instance.OnMonthTick -= DecayBuildings;
    }

    void DecayBuildings()
    {
        // TODO: iterate all placed buildings and degrade condition
        // Requires maintainence work to keep condition above threshold
        // Below 30% condition — building becomes unusable
        // Below 0% — collapses, tile becomes buildable again
    }
}
