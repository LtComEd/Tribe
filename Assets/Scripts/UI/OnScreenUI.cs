using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Immediate-mode HUD. No Canvas setup needed.
/// Left panel: tribe list with Jump buttons.
/// Right panel: selected character sheet.
/// Bottom: event log.
/// Top bar: date + speed controls.
/// </summary>
public class OnScreenUI : MonoBehaviour
{
    [Header("Settings")]
    public int  maxLogLines   = 14;

    private SimulationManager _sim;
    private TribeManager      _tribeManager;
    private WorldRenderer     _worldRenderer;
    private CharacterController2D _selectedChar;

    // Styles
    private GUIStyle _panelStyle, _labelStyle, _titleStyle, _logStyle, _buttonStyle, _jumpStyle;
    private bool     _stylesBuilt;

    private readonly List<string> _logLines = new List<string>();

    void Start()
    {
        _sim           = SimulationManager.Instance  ?? FindFirstObjectByType<SimulationManager>();
        _tribeManager  = TribeManager.Instance       ?? FindFirstObjectByType<TribeManager>();
        _worldRenderer = FindFirstObjectByType<WorldRenderer>();

        EventLog.OnNewEntry += OnNewLogEntry;
    }

    void OnDestroy() { EventLog.OnNewEntry -= OnNewLogEntry; }

    void OnNewLogEntry(string entry)
    {
        _logLines.Add(entry);
        if (_logLines.Count > 200) _logLines.RemoveAt(0);
    }

    public void SelectCharacter(CharacterController2D ctrl)
    {
        _selectedChar = (_selectedChar == ctrl) ? null : ctrl;
        // No camera movement on select — use >> Go to jump, or Follow button in sheet
        if (_selectedChar == null && _worldRenderer != null)
            _worldRenderer.StopFollowing();
    }

    /// Jump camera instantly to character and start following
    public void JumpAndFollow(CharacterController2D ctrl)
    {
        _selectedChar = ctrl;
        if (_worldRenderer != null && ctrl != null)
            _worldRenderer.FollowTransform(ctrl.transform, snapNow: true);
    }

    // ── OnGUI ─────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        BuildStyles();
        float sw = Screen.width, sh = Screen.height;

        DrawTopBar(sw);
        DrawTribePanel(sh);
        DrawEventLog(sw, sh);

        if (_selectedChar != null && _selectedChar.Sheet != null)
            DrawCharacterSheet(sw, sh);
    }

    // ── Top Bar ───────────────────────────────────────────────────────────────

    void DrawTopBar(float sw)
    {
        GUI.Box(new Rect(0, 0, sw, 28), GUIContent.none, _panelStyle);
        string date = _sim != null ? _sim.GetDateString() : "Initialising...";
        GUI.Label(new Rect(8, 4, 500, 22), date, _titleStyle);

        if (_sim != null)
        {
            float bx = sw - 290f;
            string pauseLabel = _sim.isPaused ? "PAUSED" : "Running";
            GUI.Label(new Rect(bx, 5, 60, 18), pauseLabel, _labelStyle);
            bx += 62f;
            if (GUI.Button(new Rect(bx,      5, 26, 18), "||",  _buttonStyle)) _sim.SetPaused(!_sim.isPaused);
            if (GUI.Button(new Rect(bx + 30, 5, 28, 18), "x1",  _buttonStyle)) { _sim.SetPaused(false); _sim.SetTimeScale(1f);  }
            if (GUI.Button(new Rect(bx + 62, 5, 28, 18), "x3",  _buttonStyle)) { _sim.SetPaused(false); _sim.SetTimeScale(3f);  }
            if (GUI.Button(new Rect(bx + 94, 5, 32, 18), "x10", _buttonStyle)) { _sim.SetPaused(false); _sim.SetTimeScale(10f); }
            if (GUI.Button(new Rect(bx +130, 5, 32, 18), "x20", _buttonStyle)) { _sim.SetPaused(false); _sim.SetTimeScale(20f); }
        }
    }

    // ── Tribe Panel ───────────────────────────────────────────────────────────

    void DrawTribePanel(float sh)
    {
        float px = 6, py = 32, pw = 200, ph = sh - 175;
        GUI.Box(new Rect(px, py, pw, ph), GUIContent.none, _panelStyle);

        float y = py + 6;
        GUI.Label(new Rect(px+6, y, pw-12, 18), "TRIBE  [pop: " + (_tribeManager?.Population ?? 0) + "]", _titleStyle);
        y += 20;

        // Resource summary
        if (_tribeManager != null)
        {
            GUI.Label(new Rect(px+6, y, pw-12, 14),
                "Wood:" + Fmt(_tribeManager.Inventory.GetAmount(ResourceType.Wood)) +
                " Stone:" + Fmt(_tribeManager.Inventory.GetAmount(ResourceType.Stone)), _logStyle);
            y += 14;
            GUI.Label(new Rect(px+6, y, pw-12, 14),
                "Berries:" + Fmt(_tribeManager.Inventory.GetAmount(ResourceType.Berries)) +
                " Meat:" + Fmt(_tribeManager.Inventory.GetAmount(ResourceType.RawMeat)), _logStyle);
            y += 16;

            // Character rows
            foreach (var c in _tribeManager.Characters)
            {
                if (c == null || c.Sheet == null || y > py + ph - 28) continue;
                var s   = c.Sheet;
                bool sel = _selectedChar == c;

                // Background highlight for selected
                if (sel)
                {
                    GUI.color = new Color(1f, 0.85f, 0.3f, 0.25f);
                    GUI.Box(new Rect(px+2, y-1, pw-4, 18), GUIContent.none);
                    GUI.color = Color.white;
                }

                // Colour: red=starving, blue=exhausted, yellow=selected, white=normal
                Color nameColor = sel             ? new Color(1f, 0.9f, 0.3f)
                                : s.IsStarving    ? new Color(1f, 0.3f, 0.3f)
                                : s.IsExhausted   ? new Color(0.5f, 0.6f, 1f)
                                : Color.white;

                string jobTag  = s.currentJob != JobType.Idle ? " [" + s.currentJob + "]" : "";
                string label   = s.firstName + " " + s.lastName[0] + ". " + (int)s.age + jobTag;

                // Name button — click to select
                var oldColor = GUI.contentColor;
                GUI.contentColor = nameColor;
                if (GUI.Button(new Rect(px+4, y, pw-50, 16), label, _logStyle))
                    SelectCharacter(c);
                GUI.contentColor = oldColor;

                // Jump button
                if (GUI.Button(new Rect(px + pw - 44, y, 38, 16), ">> Go", _jumpStyle))
                {
                    JumpAndFollow(c);
                }

                y += 17;
            }
        }

        // Follow status indicator at bottom of panel
        if (_worldRenderer != null && _worldRenderer.followTarget)
        {
            GUI.color = new Color(1f, 0.9f, 0.3f, 0.9f);
            GUI.Label(new Rect(px+6, py+ph-20, pw-12, 16), "Following — click map to release", _logStyle);
            GUI.color = Color.white;
        }
    }

    static string Fmt(float v) { return ((int)v).ToString(); }

    // ── Event Log ─────────────────────────────────────────────────────────────

    void DrawEventLog(float sw, float sh)
    {
        float lx = 212, ly = sh - 148, lw = sw - 220, lh = 142;

        // Shrink if character sheet is open
        if (_selectedChar != null) lw = sw - 440;

        GUI.Box(new Rect(lx-2, ly-2, lw+4, lh+4), GUIContent.none, _panelStyle);
        GUI.Label(new Rect(lx, ly, 80, 16), "EVENT LOG", _titleStyle);

        int start = Mathf.Max(0, _logLines.Count - maxLogLines);
        float ty  = ly + 18;
        for (int i = start; i < _logLines.Count && ty < ly + lh - 4; i++)
        {
            GUI.Label(new Rect(lx, ty, lw - 8, 14), _logLines[i], _logStyle);
            ty += 14;
        }
    }

    // ── Character Sheet ────────────────────────────────────────────────────────

    void DrawCharacterSheet(float sw, float sh)
    {
        var s  = _selectedChar.Sheet;
        float cx = sw - 218, cy = 32, cw = 212, ch = sh - 175;
        GUI.Box(new Rect(cx, cy, cw, ch), GUIContent.none, _panelStyle);

        float y = cy + 6;

        // Header
        GUI.Label(new Rect(cx+6, y, cw-30, 18), s.FullName, _titleStyle); y += 18;
        GUI.Label(new Rect(cx+6, y, cw-12, 14),
            "Age " + (int)s.age + "  " + s.gender + "  [" + s.AgeGroup + "]", _labelStyle); y += 15;
        GUI.Label(new Rect(cx+6, y, cw-12, 14),
            "HP: " + s.CurrentHP + "/" + s.MaxHP + "   Job: " + s.currentJob, _labelStyle); y += 15;

        // AI state
        var brain = _selectedChar.GetComponent<BrainController>();
        if (brain != null)
        {
            GUI.Label(new Rect(cx+6, y, cw-12, 14), "State: " + brain.CurrentState, _logStyle); y += 14;
        }

        // Status effects
        if (s.StatusEffects.Count > 0)
        {
            string effects = "";
            foreach (var e in s.StatusEffects) effects += e + " ";
            GUI.Label(new Rect(cx+6, y, cw-12, 14), effects.Trim(), _logStyle); y += 14;
        }

        y += 4;

        // Stats
        GUI.Label(new Rect(cx+6, y, cw-12, 14), "─── STATS ───", _logStyle); y += 14;
        StatRow(cx, ref y, cw, "STR", s.Strength,     s.StrMod);
        StatRow(cx, ref y, cw, "DEX", s.Dexterity,    s.DexMod);
        StatRow(cx, ref y, cw, "CON", s.Constitution, s.ConMod);
        StatRow(cx, ref y, cw, "INT", s.Intelligence, s.IntMod);
        StatRow(cx, ref y, cw, "WIS", s.Wisdom,       s.WisMod);
        StatRow(cx, ref y, cw, "CHA", s.Charisma,     s.ChaMod);

        y += 4;

        // Needs
        GUI.Label(new Rect(cx+6, y, cw-12, 14), "─── NEEDS ───", _logStyle); y += 14;
        NeedBar(cx, ref y, cw, "Hunger",  s.Hunger,  new Color(0.9f, 0.5f, 0.15f));
        NeedBar(cx, ref y, cw, "Rest",    s.Rest,    new Color(0.3f, 0.5f, 0.9f));
        NeedBar(cx, ref y, cw, "Safety",  s.Safety,  new Color(0.8f, 0.25f, 0.25f));
        NeedBar(cx, ref y, cw, "Social",  s.Social,  new Color(0.4f, 0.85f, 0.4f));

        y += 4;

        // Skills (non-zero only)
        bool anySkill = false;
        foreach (var kv in s.Skills)
            if (kv.Value >= 1f) { anySkill = true; break; }

        if (anySkill)
        {
            GUI.Label(new Rect(cx+6, y, cw-12, 14), "─── SKILLS ───", _logStyle); y += 14;
            foreach (var kv in s.Skills)
            {
                if (kv.Value < 1f || y > cy+ch-24) continue;
                float barW = (cw - 80f) * Mathf.Clamp01(kv.Value / 100f);
                GUI.Label(new Rect(cx+6, y, 65, 13), kv.Key.ToString(), _logStyle);
                var oc = GUI.color;
                GUI.color = new Color(0.3f, 0.7f, 0.4f, 0.8f);
                GUI.Box(new Rect(cx+72, y+2, barW, 9), GUIContent.none);
                GUI.color = oc;
                GUI.Label(new Rect(cx+72+(int)barW+2, y, 30, 13), ((int)kv.Value).ToString(), _logStyle);
                y += 14;
            }
        }

        // Close button
        if (GUI.Button(new Rect(cx+cw-22, cy+4, 18, 16), "x", _buttonStyle))
        {
            _selectedChar = null;
            _worldRenderer?.StopFollowing();
        }

        // Jump / Follow toggle at bottom
        if (y < cy+ch-22)
        {
            bool following = _worldRenderer != null && _worldRenderer.followTarget;
            string followLabel = following ? "Unfollow" : "Follow";
            if (GUI.Button(new Rect(cx+6, cy+ch-22, 68, 18), followLabel, _buttonStyle))
            {
                if (following) _worldRenderer?.StopFollowing();
                else           _worldRenderer?.FollowTransform(_selectedChar.transform, snapNow: false);
            }
            if (GUI.Button(new Rect(cx+78, cy+ch-22, 58, 18), "Jump To", _buttonStyle))
            {
                _worldRenderer?.FollowTransform(_selectedChar.transform, snapNow: true);
            }
            if (GUI.Button(new Rect(cx+140, cy+ch-22, 54, 18), "Detach", _buttonStyle))
            {
                _worldRenderer?.StopFollowing();
            }
        }
    }

    void StatRow(float cx, ref float y, float cw, string stat, int val, int mod)
    {
        string sign = mod >= 0 ? "+" : "";
        GUI.Label(new Rect(cx+6,  y, 30, 13), stat,                    _labelStyle);
        GUI.Label(new Rect(cx+38, y, 22, 13), val.ToString(),          _labelStyle);
        GUI.Label(new Rect(cx+62, y, 35, 13), "(" + sign + mod + ")",  _logStyle);

        // Mini stat bar
        float bw = cw - 105f;
        float fill = (val / 20f) * bw;
        var oc = GUI.color;
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        GUI.Box(new Rect(cx+100, y+2, bw, 8), GUIContent.none);
        GUI.color = new Color(0.4f, 0.7f, 0.9f, 0.8f);
        GUI.Box(new Rect(cx+100, y+2, fill, 8), GUIContent.none);
        GUI.color = oc;
        y += 14;
    }

    void NeedBar(float cx, ref float y, float cw, string label, float val, Color col)
    {
        float bw   = cw - 72f;
        float fill = Mathf.Clamp01(val / 100f) * bw;
        GUI.Label(new Rect(cx+6, y, 60, 13), label, _logStyle);
        var oc = GUI.color;
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.7f);
        GUI.Box(new Rect(cx+66, y+2, bw, 9), GUIContent.none);
        GUI.color = col;
        GUI.Box(new Rect(cx+66, y+2, fill, 9), GUIContent.none);
        GUI.color = oc;
        GUI.Label(new Rect(cx+66+bw+2, y, 28, 13), ((int)val).ToString(), _logStyle);
        y += 14;
    }

    // ── Style Builder ─────────────────────────────────────────────────────────

    void BuildStyles()
    {
        if (_stylesBuilt) return;
        _stylesBuilt = true;

        _panelStyle = new GUIStyle(GUI.skin.box);
        _panelStyle.normal.background = MakeTex(2, 2, new Color(0.04f, 0.05f, 0.06f, 0.90f));

        _labelStyle = new GUIStyle(GUI.skin.label);
        _labelStyle.fontSize = 11;
        _labelStyle.normal.textColor = new Color(0.88f, 0.88f, 0.82f);

        _titleStyle = new GUIStyle(GUI.skin.label);
        _titleStyle.fontSize  = 12;
        _titleStyle.fontStyle = FontStyle.Bold;
        _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.45f);

        _logStyle = new GUIStyle(GUI.skin.label);
        _logStyle.fontSize = 10;
        _logStyle.normal.textColor = new Color(0.75f, 0.78f, 0.72f);

        _buttonStyle = new GUIStyle(GUI.skin.button);
        _buttonStyle.fontSize = 10;
        _buttonStyle.padding  = new RectOffset(3, 3, 2, 2);

        _jumpStyle = new GUIStyle(GUI.skin.button);
        _jumpStyle.fontSize = 9;
        _jumpStyle.padding  = new RectOffset(2, 2, 2, 2);
        _jumpStyle.normal.textColor = new Color(0.6f, 1f, 0.6f);
    }

    static Texture2D MakeTex(int w, int h, Color col)
    {
        var tex = new Texture2D(w, h);
        var px  = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = col;
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
