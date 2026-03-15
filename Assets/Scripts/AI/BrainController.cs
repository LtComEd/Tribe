using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI brain for a character. Currently implements 3 starter behaviors:
///   1. Seek food when hungry
///   2. Sleep/rest when exhausted
///   3. Gather wood / build when needs are met
///
/// Architecture is DORMANT-READY: full Goal → Action planning slots are
/// defined but mostly empty. Wire in over time as systems mature.
/// </summary>
[RequireComponent(typeof(CharacterController2D))]
public class BrainController : MonoBehaviour
{
    // ── State Machine ─────────────────────────────────────────────────────────

    public enum BrainState
    {
        Idle,
        SeekingFood,
        Sleeping,
        Working,
        Socialising,
        Fleeing,
        Building,
        // Future states (dormant)
        Patrolling, Researching, Trading, Romancing, Grieving
    }

    public BrainState CurrentState { get; private set; } = BrainState.Idle;

    // ── References ────────────────────────────────────────────────────────────

    private CharacterController2D _ctrl;
    private CharacterSheet _sheet;

    // ── Decision throttle ─────────────────────────────────────────────────────
    // Don't re-evaluate every frame — only on hour tick or when action completes

    private bool _needsDecision = true;

    // ── Current Action ───────────────────────────────────────────────────────

    private Coroutine _actionCoroutine;

    // ── Init ──────────────────────────────────────────────────────────────────

    void Awake()
    {
        _ctrl = GetComponent<CharacterController2D>();
    }

    void Start()
    {
        // Sheet is set after Initialise() — wait a frame
        StartCoroutine(LateStart());
    }

    IEnumerator LateStart()
    {
        yield return null; // let CharacterController2D.Initialise() finish
        _sheet = _ctrl.Sheet;

        if (SimulationManager.Instance)
            SimulationManager.Instance.OnHourTick += OnHourTick;
    }

    void OnDestroy()
    {
        if (SimulationManager.Instance)
            SimulationManager.Instance.OnHourTick -= OnHourTick;
    }

    // ── Tick ──────────────────────────────────────────────────────────────────

    void OnHourTick()
    {
        if (_sheet == null || !_sheet.isAlive) return;
        _needsDecision = true;
    }

    void Update()
    {
        if (_sheet == null || !_sheet.isAlive) return;
        if (!_needsDecision) return;
        if (_ctrl.IsBusy) return;   // let current action finish

        _needsDecision = false;
        EvaluateAndAct();
    }

    // ── Core Decision Loop ────────────────────────────────────────────────────

    void EvaluateAndAct()
    {
        // Priority order: survival → rest → work
        // Each method returns true if it claims the tick

        if (TrySurvival())  return;
        if (TryRest())      return;
        if (TryWork())      return;

        // Default: wander slightly
        SetState(BrainState.Idle);
        if (Random.value < 0.3f) Wander();
    }

    // ── Survival: Food ───────────────────────────────────────────────────────

    bool TrySurvival()
    {
        if (_sheet.Hunger >= 35f) return false;

        SetState(BrainState.SeekingFood);

        // Check tribe inventory first
        var inv = TribeManager.Instance?.Inventory;
        if (inv != null && (inv.Has(ResourceType.CookedMeat, 1f) || inv.Has(ResourceType.Berries, 1f)))
        {
            EatFromInventory(inv);
            return true;
        }

        // Forage nearby berries / meat
        var map = WorldMap.Instance;
        if (map == null) return false;

        var foodTile = map.FindNearestResource(_ctrl.CurrentTilePos, ResourceType.Berries, 30)
                    ?? map.FindNearestResource(_ctrl.CurrentTilePos, ResourceType.WildGame, 40)
                    ?? map.FindNearestResource(_ctrl.CurrentTilePos, ResourceType.Fish, 30);

        if (foodTile != null)
        {
            _ctrl.MoveTo(foodTile.Position, () =>
            {
                float gathered = _ctrl.GatherResource(ResourceType.Berries)
                               + _ctrl.GatherResource(ResourceType.WildGame)
                               + _ctrl.GatherResource(ResourceType.Fish);
                if (gathered > 0f)
                {
                    _sheet.Hunger = Mathf.Min(100f, _sheet.Hunger + gathered * 8f);
                    EventLog.Log($"{_sheet.FullName} foraged and ate. (Hunger: {_sheet.Hunger:F0})");
                }
                _needsDecision = true;
            });
            return true;
        }

        // Nothing found — log distress
        if (_sheet.Hunger < 15f)
            EventLog.Log($"{_sheet.FullName} is starving and cannot find food!");

        return false;
    }

    void EatFromInventory(ResourceInventory inv)
    {
        float eaten = 0f;
        if (inv.Remove(ResourceType.CookedMeat, 1f)) eaten += 20f;
        else if (inv.Remove(ResourceType.Berries, 1f)) eaten += 10f;

        _sheet.Hunger = Mathf.Min(100f, _sheet.Hunger + eaten);
        _needsDecision = true;
    }

    // ── Rest ──────────────────────────────────────────────────────────────────

    bool TryRest()
    {
        int hour = SimulationManager.Instance?.Hour ?? 12;
        bool nightTime = hour >= 21 || hour < 6;

        if (!_sheet.IsExhausted && !nightTime) return false;
        if (_sheet.Rest >= 90f) return false;

        SetState(BrainState.Sleeping);

        // Find a shelter or just sleep on the spot
        // TODO: navigate to assigned sleeping building
        if (_actionCoroutine != null) StopCoroutine(_actionCoroutine);
        _actionCoroutine = StartCoroutine(SleepRoutine());
        return true;
    }

    IEnumerator SleepRoutine()
    {
        // Sleep for up to 8 game hours (simulated as waiting a coroutine for real seconds)
        // Each iteration represents 1 game-hour of sleep
        int hoursSlept = 0;
        int maxSleep = _sheet.IsExhausted ? 10 : 6;

        while (hoursSlept < maxSleep && _sheet.Rest < 95f)
        {
            _sheet.Rest = Mathf.Min(100f, _sheet.Rest + 15f);
            _sheet.Stamina = Mathf.Min(_sheet.MaxStamina, _sheet.Stamina + 20f);
            hoursSlept++;
            // Wait for next simulation hour (real time = secondsPerGameHour / timeScale)
            float waitTime = SimulationManager.Instance != null
                ? SimulationManager.Instance.secondsPerGameHour / SimulationManager.Instance.timeScale
                : 1f;
            yield return new WaitForSeconds(waitTime);
        }

        _needsDecision = true;
    }

    // ── Work ──────────────────────────────────────────────────────────────────

    bool TryWork()
    {
        // Respect day/night — no work at night unless guard
        int hour = SimulationManager.Instance?.Hour ?? 12;
        if (hour < 6 || hour >= 20) return false;

        SetState(BrainState.Working);

        switch (_sheet.currentJob)
        {
            case JobType.Woodcutter: return TryGatherJob(ResourceType.Wood, SkillType.Woodcutting, 10f);
            case JobType.Gatherer:
            case JobType.Forager:   return TryGatherJob(ResourceType.Berries, SkillType.Foraging, 5f);
            case JobType.Hunter:    return TryGatherJob(ResourceType.WildGame, SkillType.Hunting, 8f);
            case JobType.Miner:     return TryGatherJob(ResourceType.Stone, SkillType.Mining, 12f);
            case JobType.Fisher:    return TryGatherJob(ResourceType.Fish, SkillType.Fishing, 6f);
            case JobType.Carpenter: return TryBuildJob();
            case JobType.Farmer:    return TryFarmJob();
            default:
                // Idle workers default to gathering food or wood
                return TryGatherJob(ResourceType.Berries, SkillType.Foraging, 5f)
                    || TryGatherJob(ResourceType.Wood, SkillType.Woodcutting, 8f);
        }
    }

    bool TryGatherJob(ResourceType type, SkillType skill, float depositAmount)
    {
        var map = WorldMap.Instance;
        if (map == null) return false;

        var target = map.FindNearestResource(_ctrl.CurrentTilePos, type, 50);
        if (target == null) return false;

        _ctrl.MoveTo(target.Position, () =>
        {
            float gathered = _ctrl.GatherResource(type,
                efficiency: 1f + _sheet.GetSkill(skill) / 100f);

            if (gathered > 0f)
            {
                TribeManager.Instance?.Inventory.Add(type, gathered);
                EventLog.Log($"{_sheet.FullName} gathered {gathered:F1}x {type}.");
            }
            _needsDecision = true;
        });
        return true;
    }

    bool TryBuildJob()
    {
        // Find any unfinished building and contribute an hour of work
        var map = WorldMap.Instance;
        if (map == null) return false;

        // Search nearby tiles for an under-construction building
        var nearbyTiles = map.GetTilesInRadius(_ctrl.CurrentTilePos, 20);
        foreach (var tile in nearbyTiles)
        {
            if (tile.buildingId < 0) continue;
            var building = map.GetBuilding(tile.buildingId);
            if (building == null || building.isConstructed) continue;

            _ctrl.MoveTo(tile.Position, () =>
            {
                BuildingManager.Instance?.ProgressConstruction(building, _sheet, 1f);
                _needsDecision = true;
            });
            return true;
        }
        return false;
    }

    bool TryFarmJob()
    {
        // For now, farming is treated as a gather job on farmland tiles
        // Future: planting seasons, growth cycles, harvesting
        return TryGatherJob(ResourceType.WildGame, SkillType.Farming, 4f);
    }

    // ── Wander ───────────────────────────────────────────────────────────────

    void Wander()
    {
        var map = WorldMap.Instance;
        if (map == null) return;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            var offset = new Vector2Int(Random.Range(-5, 6), Random.Range(-5, 6));
            var target = _ctrl.CurrentTilePos + offset;
            var tile = map.GetTile(target);
            if (tile != null && tile.IsPassable)
            {
                _ctrl.MoveTo(target, () => _needsDecision = true);
                return;
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void SetState(BrainState state)
    {
        if (CurrentState != state)
        {
            CurrentState = state;
            // Future: trigger animations, UI badges
        }
    }

    // ── DORMANT HOOKS (wire up later) ─────────────────────────────────────────
    //
    // These methods exist so the architecture is ready.
    // They do nothing yet — implement in phases.
    //
    // public void OnSocialTick()   { /* find nearby character, chat, share memory */ }
    // public void OnThreatDetected(Vector2Int threatPos) { SetState(BrainState.Fleeing); }
    // public void OnTaskAssigned(JobType job) { _sheet.currentJob = job; _needsDecision = true; }
    // public void ConsiderRomance() { /* CHA check, affection threshold, propose */ }
    // public void ShareMemory(BrainController other) { /* copy relevant MemoryEvents */ }
}
