using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Full AD&D-inspired character data model.
/// Pure C# — no MonoBehaviour. Held by CharacterController.
/// </summary>
[System.Serializable]
public class CharacterSheet
{
    // ── Identity ─────────────────────────────────────────────────────────────

    public int id;                  // Unique simulation ID
    public string firstName;
    public string lastName;
    public string FullName => $"{firstName} {lastName}";
    public Gender gender;
    public float age;               // game-years (decimal)
    public bool isAlive = true;

    // Age brackets
    public AgeGroup AgeGroup
    {
        get
        {
            if (age < 13)  return AgeGroup.Child;
            if (age < 18)  return AgeGroup.Adolescent;
            if (age < 36)  return AgeGroup.YoungAdult;
            if (age < 56)  return AgeGroup.MiddleAge;
            return AgeGroup.Elder;
        }
    }
    public bool IsAdult => age >= 18f;

    // ── Primary Stats (3-18 AD&D range, rolled or inherited) ─────────────────

    public int Strength;        // melee, carrying capacity, building
    public int Dexterity;       // ranged, dodge, fine crafting
    public int Constitution;    // HP, fatigue, disease resistance
    public int Intelligence;    // learning rate, research, planning quality
    public int Wisdom;          // decision quality, morale, spiritual
    public int Charisma;        // leadership, persuasion, romance
    public int Comeliness;      // attractiveness; affects romance/first impressions

    // Standard AD&D ability score modifier: (score - 10) / 2
    public int StrMod  => Modifier(Strength);
    public int DexMod  => Modifier(Dexterity);
    public int ConMod  => Modifier(Constitution);
    public int IntMod  => Modifier(Intelligence);
    public int WisMod  => Modifier(Wisdom);
    public int ChaMod  => Modifier(Charisma);
    public int ComMod  => Modifier(Comeliness);

    static int Modifier(int score) => Mathf.FloorToInt((score - 10) / 2f);

    // ── Derived Stats ─────────────────────────────────────────────────────────

    public int MaxHP
    {
        get
        {
            // Base 8 + Con modifier per "level" (represented by age bracket)
            int baseHP = 8 + ConMod * 3;
            switch (AgeGroup)
            {
                case AgeGroup.Child:      baseHP -= 4; break;
                case AgeGroup.Adolescent: baseHP -= 1; break;
                case AgeGroup.MiddleAge:  baseHP -= 1; break;
                case AgeGroup.Elder:      baseHP -= 3; break;
            }
            return Mathf.Max(1, baseHP);
        }
    }

    public int CurrentHP;

    public float MovementSpeed => Mathf.Max(1f, 3f + DexMod * 0.3f + AgeMoveModifier);
    float AgeMoveModifier
    {
        get
        {
            switch (AgeGroup)
            {
                case AgeGroup.Child: return -0.5f;
                case AgeGroup.Elder: return -1.0f;
                default:             return 0f;
            }
        }
    }

    public int CarryCapacity => Mathf.Max(5, 20 + StrMod * 5);  // kg equivalent

    // ── Skill System ──────────────────────────────────────────────────────────
    // Skills stored as 0-100 proficiency. Improve through use. Wisdom/Int affect gain rate.

    public Dictionary<SkillType, float> Skills = new Dictionary<SkillType, float>();

    public float GetSkill(SkillType skill)
    {
        Skills.TryGetValue(skill, out float val);
        return val;
    }

    /// Attempt a skill check. Returns true on success.
    /// Difficulty: 0=trivial, 50=moderate, 80=hard, 100=legendary
    public bool SkillCheck(SkillType skill, float difficulty = 50f)
    {
        float effective = GetSkill(skill) + GetStatBonusForSkill(skill);
        float roll = Random.Range(0f, 100f);
        return roll < (effective - difficulty + 50f);
    }

    float GetStatBonusForSkill(SkillType skill)
    {
        switch (skill)
        {
            case SkillType.Farming:       return ConMod * 2f;
            case SkillType.Hunting:       return DexMod * 3f + WisMod;
            case SkillType.Fishing:       return WisMod * 2f;
            case SkillType.Mining:        return StrMod * 3f;
            case SkillType.Woodcutting:   return StrMod * 3f;
            case SkillType.Carpentry:     return DexMod * 2f + IntMod;
            case SkillType.Masonry:       return StrMod * 2f + IntMod;
            case SkillType.Smithing:      return StrMod * 2f + DexMod;
            case SkillType.Cooking:       return WisMod * 2f + IntMod;
            case SkillType.Medicine:      return IntMod * 3f + WisMod;
            case SkillType.Swordsmanship: return StrMod * 2f + DexMod * 2f;
            case SkillType.Archery:       return DexMod * 4f;
            case SkillType.Trading:       return ChaMod * 4f;
            case SkillType.Leadership:    return ChaMod * 3f + WisMod * 2f;
            case SkillType.Research:      return IntMod * 4f;
            case SkillType.Teaching:      return IntMod * 2f + WisMod * 2f;
            default:                      return 0f;
        }
    }

    /// Improve a skill from use. Returns amount gained.
    public float ImproveSkill(SkillType skill, float baseGain = 0.5f)
    {
        float learningRate = 1f + (IntMod + WisMod) * 0.1f;
        // Skill gain slows as proficiency grows (diminishing returns)
        float current = GetSkill(skill);
        float diminish = Mathf.Max(0.05f, 1f - current / 120f);
        float gain = baseGain * learningRate * diminish;

        if (!Skills.ContainsKey(skill)) Skills[skill] = 0f;
        Skills[skill] = Mathf.Min(100f, Skills[skill] + gain);
        return gain;
    }

    // ── Personality (drives AI priorities, 0-1 range) ─────────────────────────

    public float Aggression;    // prone to conflict
    public float Ambition;      // desire for status/resources
    public float Loyalty;       // tribe over self
    public float Curiosity;     // exploration and learning
    public float Empathy;       // helping others
    public float Caution;       // risk aversion

    // ── Needs (Maslow hierarchy, 0=critical, 100=satisfied) ──────────────────

    public float Hunger     = 80f;
    public float Rest       = 80f;
    public float Safety     = 70f;
    public float Social     = 50f;
    public float Fulfillment = 50f;

    // Critical thresholds
    public bool IsStarving    => Hunger < 20f;
    public bool IsExhausted   => Rest < 15f;
    public bool IsInDanger    => Safety < 25f;
    public bool IsLonely      => Social < 25f;

    // ── Health & Status ───────────────────────────────────────────────────────

    public float Stamina     = 100f;  // depletes with physical work, recovers with rest
    public float MaxStamina  => 100f + ConMod * 10f;

    public List<StatusEffect> StatusEffects = new List<StatusEffect>();

    public bool HasStatus(StatusEffect effect) => StatusEffects.Contains(effect);
    public void AddStatus(StatusEffect effect) { if (!HasStatus(effect)) StatusEffects.Add(effect); }
    public void RemoveStatus(StatusEffect effect) => StatusEffects.Remove(effect);

    // ── Job / Role ────────────────────────────────────────────────────────────

    public JobType currentJob = JobType.Idle;
    public int jobPriority = 0;   // higher = preferred

    // ── Family ───────────────────────────────────────────────────────────────

    public int spouseId      = -1;
    public int motherId      = -1;
    public int fatherId      = -1;
    public List<int> childIds = new List<int>();

    public bool isPregnant            = false;
    public float pregnancyProgress    = 0f;   // 0-1 (1 = birth)
    public int pregnancyFatherId      = -1;

    // ── Memory / Relations ────────────────────────────────────────────────────
    // Kept minimal here; MemorySystem holds the richer graph

    public Dictionary<int, float> Affections = new Dictionary<int, float>();  // characterId → affection score

    // ── Stat Generation ───────────────────────────────────────────────────────

    /// Roll stats for a new character (4d6 drop lowest, AD&D standard)
    public void RollStats(System.Random rng = null)
    {
        rng ??= new System.Random();
        Strength     = Roll4d6DropLowest(rng);
        Dexterity    = Roll4d6DropLowest(rng);
        Constitution = Roll4d6DropLowest(rng);
        Intelligence = Roll4d6DropLowest(rng);
        Wisdom       = Roll4d6DropLowest(rng);
        Charisma     = Roll4d6DropLowest(rng);
        Comeliness   = Roll4d6DropLowest(rng);
        CurrentHP    = MaxHP;
    }

    /// Inherit stats from parents with genetic variance (±2 per stat)
    public void InheritStats(CharacterSheet mother, CharacterSheet father, System.Random rng)
    {
        Strength     = InheritStat(mother.Strength,     father.Strength,     rng);
        Dexterity    = InheritStat(mother.Dexterity,    father.Dexterity,    rng);
        Constitution = InheritStat(mother.Constitution, father.Constitution, rng);
        Intelligence = InheritStat(mother.Intelligence, father.Intelligence, rng);
        Wisdom       = InheritStat(mother.Wisdom,       father.Wisdom,       rng);
        Charisma     = InheritStat(mother.Charisma,     father.Charisma,     rng);
        Comeliness   = InheritStat(mother.Comeliness,   father.Comeliness,   rng);

        // Inherit personality as blend with variance
        Aggression = InheritTrait(mother.Aggression, father.Aggression, rng);
        Ambition   = InheritTrait(mother.Ambition,   father.Ambition,   rng);
        Loyalty    = InheritTrait(mother.Loyalty,    father.Loyalty,    rng);
        Curiosity  = InheritTrait(mother.Curiosity,  father.Curiosity,  rng);
        Empathy    = InheritTrait(mother.Empathy,    father.Empathy,    rng);
        Caution    = InheritTrait(mother.Caution,    father.Caution,    rng);

        CurrentHP = MaxHP;
    }

    int InheritStat(int a, int b, System.Random rng)
    {
        int avg = (a + b) / 2;
        int variance = rng.Next(-2, 3);
        return Mathf.Clamp(avg + variance, 3, 20);
    }

    float InheritTrait(float a, float b, System.Random rng)
    {
        float avg = (a + b) / 2f;
        float variance = (float)(rng.NextDouble() - 0.5) * 0.2f;
        return Mathf.Clamp01(avg + variance);
    }

    static int Roll4d6DropLowest(System.Random rng)
    {
        int[] rolls = new int[4];
        for (int i = 0; i < 4; i++) rolls[i] = rng.Next(1, 7);
        System.Array.Sort(rolls);
        return rolls[1] + rolls[2] + rolls[3]; // drop lowest
    }

    // ── Age Effects (applied on birthday tick) ────────────────────────────────

    public void ApplyAgingEffects()
    {
        // Stat changes per age bracket transition
        switch (AgeGroup)
        {
            case AgeGroup.MiddleAge:
                Strength     = Mathf.Max(3, Strength     - 1);
                Dexterity    = Mathf.Max(3, Dexterity    - 1);
                Constitution = Mathf.Max(3, Constitution - 1);
                Wisdom       = Mathf.Min(20, Wisdom      + 1);
                break;
            case AgeGroup.Elder:
                if ((int)age % 5 == 0) // every 5 elder years
                {
                    Strength     = Mathf.Max(3, Strength     - 1);
                    Dexterity    = Mathf.Max(3, Dexterity    - 1);
                    Constitution = Mathf.Max(3, Constitution - 1);
                    Wisdom       = Mathf.Min(20, Wisdom      + 1);
                }
                break;
        }
        CurrentHP = Mathf.Min(CurrentHP, MaxHP);
    }

    public override string ToString() =>
        $"{FullName} | Age:{age:F0} {gender} [{AgeGroup}] | " +
        $"STR:{Strength} DEX:{Dexterity} CON:{Constitution} " +
        $"INT:{Intelligence} WIS:{Wisdom} CHA:{Charisma} | " +
        $"HP:{CurrentHP}/{MaxHP} | Job:{currentJob}";
}

// ── Supporting Enums ──────────────────────────────────────────────────────────

public enum Gender { Male, Female }

public enum AgeGroup { Child, Adolescent, YoungAdult, MiddleAge, Elder }

public enum SkillType
{
    Farming, Hunting, Fishing, Mining, Woodcutting,
    Carpentry, Masonry, Smithing, Cooking, Medicine,
    Swordsmanship, Archery, Tactics,
    Trading, Diplomacy, Leadership,
    Research, Teaching, Storytelling, Foraging
}

public enum JobType
{
    Idle, Farmer, Hunter, Fisher, Gatherer, Forager,
    Woodcutter, Miner, Carpenter, Mason, Thatcher,
    Blacksmith, Weaver, Cook, Healer, Scholar,
    Guard, Scout, Soldier, Elder, Chieftain
}

public enum StatusEffect
{
    Hungry, Starving, Tired, Exhausted,
    Wounded, SeriouslyWounded, Sick, Diseased,
    Pregnant, Grieving, Happy, Inspired
}
