using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central heartbeat of the simulation. Decouples game logic from Unity's Update loop.
/// All systems register tick callbacks here. Rendering remains frame-rate independent.
/// </summary>
[DefaultExecutionOrder(-100)]
public class SimulationManager : MonoBehaviour
{
    public static SimulationManager Instance { get; private set; }

    [Header("Time Settings")]
    [Tooltip("How many real seconds = 1 game hour")]
    public float secondsPerGameHour = 1f;
    [Range(0.1f, 20f)]
    public float timeScale = 1f;
    public bool isPaused = false;

    // Game time tracking
    public int Hour       { get; private set; } = 6; // start at dawn
    public int Day        { get; private set; } = 1;
    public int Month      { get; private set; } = 4; // Spring
    public int Year       { get; private set; } = 900;
    public Season CurrentSeason => (Season)((Month - 1) / 3);

    // Total elapsed game hours — useful for scheduling
    public long TotalHours { get; private set; } = 0;

    // Tick event tiers — systems subscribe to the frequency they need
    public event Action OnHourTick;    // AI decisions, needs decay
    public event Action OnDayTick;     // Growth, aging, job reassignment
    public event Action OnMonthTick;   // Births, deaths, harvests
    public event Action OnYearTick;    // Major events, tech progress
    public event Action OnSeasonChange;

    private Season _lastSeason;
    private Coroutine _tickCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        _lastSeason = CurrentSeason;
        _tickCoroutine = StartCoroutine(TickLoop());
    }

    IEnumerator TickLoop()
    {
        while (true)
        {
            if (!isPaused)
            {
                AdvanceHour();
            }
            yield return new WaitForSeconds(secondsPerGameHour / timeScale);
        }
    }

    void AdvanceHour()
    {
        TotalHours++;
        Hour++;
        OnHourTick?.Invoke();

        if (Hour >= 24)
        {
            Hour = 0;
            Day++;
            OnDayTick?.Invoke();

            if (Day > 30)
            {
                Day = 1;
                Month++;
                OnMonthTick?.Invoke();

                Season newSeason = CurrentSeason;
                if (newSeason != _lastSeason)
                {
                    _lastSeason = newSeason;
                    OnSeasonChange?.Invoke();
                }

                if (Month > 12)
                {
                    Month = 1;
                    Year++;
                    OnYearTick?.Invoke();
                }
            }
        }
    }

    public void SetPaused(bool paused) => isPaused = paused;
    public void SetTimeScale(float scale) => timeScale = Mathf.Clamp(scale, 0.1f, 20f);

    public string GetDateString() =>
        $"Day {Day}, {(MonthName)Month} {Year} AD  |  {Hour:D2}:00  [{CurrentSeason}]";

    public enum MonthName
    {
        January=1, February, March, April, May, June,
        July, August, September, October, November, December
    }
}

public enum Season { Spring, Summer, Autumn, Winter }
