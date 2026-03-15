using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class CharacterController2D : MonoBehaviour
{
    public CharacterSheet Sheet { get; private set; }

    [Header("Movement")]
    [Tooltip("Tiles per second. At 1 tile = 1 Unity unit, 2 = normal walking pace.")]
    public float tilesPerSecond = 2f;

    private List<Vector2Int> _path      = new List<Vector2Int>();
    private int              _pathIndex = 0;
    private bool             _isMoving  = false;
    private Vector3          _targetWorldPos;
    private System.Action    _onArrivalCallback;
    private bool             _isBusy = false;

    private SpriteRenderer _spriteRenderer;
    public  SpriteRenderer SpriteRenderer { get { return _spriteRenderer; } }

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Initialise(CharacterSheet sheet)
    {
        Sheet            = sheet;
        _spriteRenderer  = GetComponent<SpriteRenderer>();
        name             = sheet.FullName;

        // Ensure sprite is visible — set sorting layer and order
        if (_spriteRenderer != null)
        {
            _spriteRenderer.sortingLayerName = "Default";
            _spriteRenderer.sortingOrder     = 5;   // above tilemap (order 0-2)
            _spriteRenderer.color            = GenderColor(sheet.gender);

            // Scale: 1 tile = 1 Unity unit, character should fill ~0.7 of a tile
            transform.localScale = new Vector3(0.7f, 0.7f, 1f);
        }

        if (SimulationManager.Instance)
        {
            SimulationManager.Instance.OnHourTick += OnHourTick;
            SimulationManager.Instance.OnDayTick  += OnDayTick;
            SimulationManager.Instance.OnYearTick += OnYearTick;
        }
    }

    // Tint males blue-ish, females red-ish so they're distinguishable at a glance
    static Color GenderColor(Gender g)
    {
        return g == Gender.Male
            ? new Color(0.6f, 0.75f, 1.0f)
            : new Color(1.0f, 0.7f, 0.65f);
    }

    void OnDestroy()
    {
        if (SimulationManager.Instance)
        {
            SimulationManager.Instance.OnHourTick -= OnHourTick;
            SimulationManager.Instance.OnDayTick  -= OnDayTick;
            SimulationManager.Instance.OnYearTick -= OnYearTick;
        }
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    public bool MoveTo(Vector2Int destination, System.Action onArrival = null)
    {
        var path = Pathfinder.FindPath(CurrentTilePos, destination, allowDiagonal: false);
        if (path == null || path.Count == 0)
        {
            // Don't spam error — just fail silently and let brain retry next tick
            return false;
        }
        SetPath(path, onArrival);
        return true;
    }

    public void SetPath(List<Vector2Int> path, System.Action onArrival = null)
    {
        _path              = path;
        _pathIndex         = 0;
        _onArrivalCallback = onArrival;
        _isMoving          = path.Count > 0;
        _isBusy            = false;
        if (_isMoving) _targetWorldPos = TileToWorld(path[0]);
    }

    public void StopMoving()
    {
        _path.Clear();
        _isMoving          = false;
        _isBusy            = false;
        _onArrivalCallback = null;
    }

    // ── Position ──────────────────────────────────────────────────────────────

    public Vector2Int CurrentTilePos
    {
        get { return new Vector2Int(Mathf.FloorToInt(transform.position.x),
                                   Mathf.FloorToInt(transform.position.y)); }
    }

    public static Vector3 TileToWorld(Vector2Int tile)
    {
        return new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_isMoving || Sheet == null || !Sheet.isAlive) return;

        // Speed: tiles per second, scaled by character Dex
        float speed = tilesPerSecond * Sheet.MovementSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, _targetWorldPos, speed);

        // Flip sprite horizontally based on movement direction
        float dx = _targetWorldPos.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.01f)
            _spriteRenderer.flipX = dx < 0;

        if (Vector3.Distance(transform.position, _targetWorldPos) < 0.02f)
        {
            transform.position = _targetWorldPos;
            _pathIndex++;

            if (_pathIndex >= _path.Count)
            {
                _isMoving = false;
                _isBusy   = true;
                var cb    = _onArrivalCallback;
                _onArrivalCallback = null;
                cb?.Invoke();
                _isBusy = false;
            }
            else
            {
                _targetWorldPos = TileToWorld(_path[_pathIndex]);
            }
        }
    }

    // ── Simulation Ticks ──────────────────────────────────────────────────────

    void OnHourTick()
    {
        if (Sheet == null || !Sheet.isAlive) return;

        float hungerDecay = Sheet.AgeGroup == AgeGroup.Child ? 3f : 2.5f;
        float restDecay   = _isMoving ? 3f : 1f;

        Sheet.Hunger = Mathf.Max(0f, Sheet.Hunger - hungerDecay);
        Sheet.Rest   = Mathf.Max(0f, Sheet.Rest   - restDecay);
        Sheet.Social = Mathf.Max(0f, Sheet.Social - 0.5f);

        if (!_isMoving)
            Sheet.Stamina = Mathf.Min(Sheet.MaxStamina, Sheet.Stamina + 5f);
        else
            Sheet.Stamina = Mathf.Max(0f, Sheet.Stamina - 2f);

        if (Sheet.Hunger <= 0f) Sheet.CurrentHP -= 2;
        if (Sheet.Rest   <= 0f) Sheet.CurrentHP -= 1;

        Sheet.RemoveStatus(StatusEffect.Hungry);
        Sheet.RemoveStatus(StatusEffect.Starving);
        if (Sheet.Hunger < 30f) Sheet.AddStatus(StatusEffect.Hungry);
        if (Sheet.Hunger < 10f) Sheet.AddStatus(StatusEffect.Starving);

        // Tint red when starving, blue when exhausted
        if (_spriteRenderer != null)
        {
            if (Sheet.IsStarving)
                _spriteRenderer.color = Color.red;
            else if (Sheet.IsExhausted)
                _spriteRenderer.color = new Color(0.5f, 0.5f, 1f);
            else
                _spriteRenderer.color = GenderColor(Sheet.gender);
        }

        if (Sheet.CurrentHP <= 0) Die("starvation or exhaustion");
    }

    void OnDayTick()
    {
        if (Sheet == null || !Sheet.isAlive) return;

        if (Sheet.Hunger > 50f && Sheet.Rest > 50f)
            Sheet.CurrentHP = Mathf.Min(Sheet.MaxHP, Sheet.CurrentHP + 1);

        if (Sheet.isPregnant)
        {
            Sheet.pregnancyProgress += 1f / 270f;
            if (Sheet.pregnancyProgress >= 1f)
                TribeManager.Instance?.TriggerBirth(Sheet, this);
        }
    }

    void OnYearTick()
    {
        if (Sheet == null || !Sheet.isAlive) return;
        Sheet.age += 1f;
        Sheet.ApplyAgingEffects();
        if (Sheet.age >= 65f)
        {
            float chance = (Sheet.age - 65f) * 0.02f - Sheet.ConMod * 0.02f;
            if (Random.value < chance) Die("old age");
        }
    }

    // ── Work ──────────────────────────────────────────────────────────────────

    public float GatherResource(ResourceType type, float efficiency = 1f)
    {
        var tile = WorldMap.Instance?.GetTile(CurrentTilePos);
        if (tile == null) return 0f;

        foreach (var res in tile.resources)
        {
            if (res.resourceType == type && !res.isDepleted)
            {
                float harvested = res.Harvest(Mathf.Min(5f * efficiency, res.quantity));
                var skill = ResourceToSkill(type);
                if (skill.HasValue) Sheet.ImproveSkill(skill.Value, 0.3f);
                return harvested;
            }
        }
        return 0f;
    }

    static SkillType? ResourceToSkill(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.WildGame:  return SkillType.Hunting;
            case ResourceType.Fish:      return SkillType.Fishing;
            case ResourceType.Wood:      return SkillType.Woodcutting;
            case ResourceType.Stone:     return SkillType.Mining;
            case ResourceType.IronOre:   return SkillType.Mining;
            case ResourceType.Berries:   return SkillType.Foraging;
            case ResourceType.Herbs:     return SkillType.Foraging;
            case ResourceType.Mushrooms: return SkillType.Foraging;
            default:                     return null;
        }
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    public void Die(string cause)
    {
        if (!Sheet.isAlive) return;
        Sheet.isAlive = false;
        StopMoving();
        Debug.Log("[Sim] " + Sheet.FullName + " died: " + cause + " age " + (int)Sheet.age);
        EventLog.Log(Sheet.FullName + " has died (" + cause + ") at age " + (int)Sheet.age + ".");
        TribeManager.Instance?.OnCharacterDeath(Sheet, this);
        StartCoroutine(DeathFade());
    }

    IEnumerator DeathFade()
    {
        float t = 0f;
        Color c = _spriteRenderer.color;
        while (t < 1.5f)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, t / 1.5f);
            _spriteRenderer.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    void OnMouseDown()
    {
        UIManager.Instance?.ShowCharacterSheet(this);
    }

    public void OnPointerClick()
    {
        UIManager.Instance?.ShowCharacterSheet(this);
    }

    public bool IsBusy   { get { return _isBusy || _isMoving; } }
    public bool IsMoving { get { return _isMoving; } }
}
