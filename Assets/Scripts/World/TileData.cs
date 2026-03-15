using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TileData
{
    public int x, y;
    public Vector2Int Position { get { return new Vector2Int(x, y); } }

    public TileType type;
    public float elevation;
    public float moisture;
    public float temperature;
    public float fertility;

    public bool IsPassable
    {
        get { return type != TileType.DeepWater && type != TileType.Mountain; }
    }

    public bool IsBuildable
    {
        get
        {
            return IsPassable
                && type != TileType.ShallowWater
                && type != TileType.Swamp
                && buildingId < 0;
        }
    }

    public float MovementCost
    {
        get
        {
            switch (type)
            {
                case TileType.Grass:        return 1.0f;
                case TileType.Sand:         return 1.5f;
                case TileType.Forest:       return 1.8f;
                case TileType.Swamp:        return 2.5f;
                case TileType.ShallowWater: return 4.0f;
                case TileType.Mountain:     return 3.5f;
                case TileType.Snow:         return 2.0f;
                case TileType.Farmland:     return 1.0f;
                default:                    return 1.0f;
            }
        }
    }

    public List<ResourceNode> resources = new List<ResourceNode>();
    public int buildingId = -1;
    public bool isReserved = false;
    public FogState fogState = FogState.Unexplored;
}

[System.Serializable]
public class ResourceNode
{
    public ResourceType resourceType;
    public float quantity;
    public float maxQuantity;
    public float regenRate;
    public bool isDepleted { get { return quantity <= 0; } }

    public ResourceNode(ResourceType type, float max, float regen)
    {
        resourceType = type;
        maxQuantity  = max;
        quantity     = max;
        regenRate    = regen;
    }

    public float Harvest(float amount)
    {
        float harvested = Mathf.Min(amount, quantity);
        quantity -= harvested;
        return harvested;
    }

    public void Regenerate(float deltaTime)
    {
        quantity = Mathf.Min(maxQuantity, quantity + regenRate * deltaTime);
    }
}

public enum TileType
{
    DeepWater, ShallowWater, Sand, Grass, Forest,
    Mountain, Snow, Swamp, Desert, Farmland
}

public enum ResourceType
{
    Wood, Stone, Flint, Clay, Sand,
    WildGame, Fish, Berries, Mushrooms, Herbs,
    IronOre, CopperOre, Coal, Limestone,
    Plank, CutStone, Charcoal,
    RawMeat, CookedMeat, Bread, Ale
}

public enum FogState { Unexplored, Remembered, Visible }
