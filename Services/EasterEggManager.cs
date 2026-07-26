using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;

namespace VenueMapper.Services;

[Serializable]
public class EasterEggState
{
    public bool Discovered { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime? DiscoveredAt { get; set; }
}

public static class EasterEggIds
{
    public const string RgbOverload    = "rgb_overload";
    public const string HackerMode     = "hacker_mode";
    public const string SunnyDetection = "sunny_detection";
    public const string WindowWobble   = "window_wobble";
    public const string RandomTitle    = "random_title";

    public static readonly string[] All =
    [
        RgbOverload, HackerMode, SunnyDetection, WindowWobble, RandomTitle,
    ];
}

public class EasterEggManager
{
    private readonly Configuration config;
    private readonly IPluginLog log;

    public event Action<string>? OnUnlocked;
    public event Action? OnAllUnlocked;

    private DateTime hackerModeBootUntil = DateTime.MinValue;

    public EasterEggManager(Configuration config, IPluginLog log)
    {
        this.config = config;
        this.log = log;

        EnsureDefaultDiscovered(EasterEggIds.WindowWobble);
    }

    public bool IsRgbOverloadActive => IsEnabled(EasterEggIds.RgbOverload);

    public bool IsHackerModeActive => IsEnabled(EasterEggIds.HackerMode);
    public bool IsHackerModeBooting => DateTime.Now < hackerModeBootUntil;
    public void StartHackerModeBoot(double seconds = 7.5) => hackerModeBootUntil = DateTime.Now.AddSeconds(seconds);

    private EasterEggState GetOrCreate(string id)
    {
        if (!config.EasterEggs.TryGetValue(id, out var state))
        {
            state = new EasterEggState();
            config.EasterEggs[id] = state;
        }
        return state;
    }

    public bool IsDiscovered(string id)
        => config.EasterEggs.TryGetValue(id, out var s) && s.Discovered;

    public bool IsEnabled(string id)
        => config.EasterEggs.TryGetValue(id, out var s) && s.Discovered && s.Enabled;

    public DateTime? GetDiscoveredAt(string id)
        => config.EasterEggs.TryGetValue(id, out var s) ? s.DiscoveredAt : null;

    public bool Unlock(string id, bool autoEnable = true)
    {
        var state = GetOrCreate(id);
        if (state.Discovered) return false;

        state.Discovered = true;
        state.Enabled = autoEnable;
        state.DiscoveredAt = DateTime.Now;
        config.Save();

        log.Information($"[VenueMapper/EasterEgg] Unlocked: {id}");
        OnUnlocked?.Invoke(id);

        if (EasterEggIds.All.All(IsDiscovered))
            OnAllUnlocked?.Invoke();

        return true;
    }

    private void EnsureDefaultDiscovered(string id)
    {
        var state = GetOrCreate(id);
        if (state.Discovered) return;
        state.Discovered = true;
        state.Enabled = true;
        config.Save();
    }

    public void RegisterFirstSeen(string id)
    {
        var state = GetOrCreate(id);
        if (!state.Discovered || state.DiscoveredAt.HasValue) return;
        state.DiscoveredAt = DateTime.Now;
        config.Save();
    }

    public void SetEnabled(string id, bool enabled)
    {
        var state = GetOrCreate(id);
        if (!state.Discovered) return;
        state.Enabled = enabled;

        if (enabled)
        {
            var other = id switch
            {
                EasterEggIds.RgbOverload => EasterEggIds.HackerMode,
                EasterEggIds.HackerMode  => EasterEggIds.RgbOverload,
                _ => null,
            };
            if (other != null && IsDiscovered(other))
                GetOrCreate(other).Enabled = false;
        }

        config.Save();
    }
}
