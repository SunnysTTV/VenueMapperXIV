using System;
using System.Collections.Generic;

namespace VenueMapper.Services;

public enum ToastKind
{
    Info,
    Success,
    Egg,
}

public enum ToastCorner
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft,
}

public sealed class ToastManager
{
    public sealed class ToastEntry
    {
        public required string Text;
        public ToastKind Kind;
        public DateTime StartedAt;
        public double Duration;
    }

    private const double TrimFadeOutGrace = 0.35;

    private readonly List<ToastEntry> entries = new();
    private readonly List<ToastEntry> retiring = new();

    public int MaxVisible = 4;
    public float DurationMultiplier = 1.0f;
    public bool Paused { get; private set; }

    private DateTime? pauseStartedAt;

    public void SetPaused(bool paused)
    {
        if (paused == Paused) return;
        Paused = paused;

        if (paused)
        {
            pauseStartedAt = DateTime.Now;
        }
        else if (pauseStartedAt.HasValue)
        {
            var pausedFor = DateTime.Now - pauseStartedAt.Value;
            foreach (var e in entries) e.StartedAt += pausedFor;
            foreach (var e in retiring) e.StartedAt += pausedFor;
            pauseStartedAt = null;
        }
    }

    public void Show(string text, ToastKind kind = ToastKind.Info, double duration = 3.0)
    {
        entries.Add(new ToastEntry
        {
            Text = text,
            Kind = kind,
            StartedAt = DateTime.Now,
            Duration = duration * DurationMultiplier,
        });

        while (MaxVisible > 0 && entries.Count > MaxVisible)
        {
            var oldest = entries[0];
            entries.RemoveAt(0);

            var elapsed = (DateTime.Now - oldest.StartedAt).TotalSeconds;
            oldest.Duration = Math.Min(oldest.Duration, elapsed + TrimFadeOutGrace);
            retiring.Add(oldest);
        }
    }

    public IReadOnlyList<ToastEntry> Active
    {
        get
        {
            entries.RemoveAll(e => (DateTime.Now - e.StartedAt).TotalSeconds > e.Duration);
            retiring.RemoveAll(e => (DateTime.Now - e.StartedAt).TotalSeconds > e.Duration);

            if (retiring.Count == 0) return entries;

            var combined = new List<ToastEntry>(retiring.Count + entries.Count);
            combined.AddRange(retiring);
            combined.AddRange(entries);
            return combined;
        }
    }
}
