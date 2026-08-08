using System;
using System.Collections.Generic;

namespace VenueMapper.Services;

public enum ToastKind
{
    Info,
    Success,
    Warning,
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
        public string? Tag;

        // Separate from StartedAt on purpose - StartedAt gets nudged forward every frame while
        // hovered (to freeze the countdown), so anything that needs a stable, one-time-only value
        // (like the Easter Egg sparkle's random seed) must use this instead, or it re-randomizes
        // every single frame while hovered instead of staying put.
        public readonly DateTime CreatedAt = DateTime.Now;
    }

    public sealed record HistoryEntry(string Text, ToastKind Kind, DateTime Timestamp);

    private const double TrimFadeOutGrace = 0.35;
    private const int MaxHistory = 200;
    private const double RecentTrimWindow = 4.0;

    private readonly object sync = new();
    private readonly List<ToastEntry> entries = new();
    private readonly List<ToastEntry> retiring = new();
    private readonly List<HistoryEntry> history = new();
    private readonly List<DateTime> recentTrims = new();

    public int MaxVisible = 4;
    public float DurationMultiplier = 1.0f;
    public bool Paused { get; private set; }

    private DateTime? pauseStartedAt;

    public void SetPaused(bool paused)
    {
        lock (sync)
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
    }

    // Toasts.Show() can be called from a background thread (e.g. a continuation after an
    // await'd HTTP call that didn't resume on the main/render thread), while ToastOverlay.Draw()
    // enumerates Active on the render thread every frame - lock + always-copy in Active prevents
    // "Collection was modified" crashes from that race.
    //
    // tag: when set, any existing visible toast with the same tag is removed before adding the
    // new one - for status-style toasts (e.g. "notifications now appear here") where only the
    // latest should ever be on screen, instead of stacking one per change.
    public void Show(string text, ToastKind kind = ToastKind.Info, double duration = 3.0, string? tag = null)
    {
        lock (sync)
        {
            history.Add(new HistoryEntry(text, kind, DateTime.Now));
            if (history.Count > MaxHistory)
                history.RemoveAt(0);

            if (tag != null)
            {
                entries.RemoveAll(e => e.Tag == tag);
                retiring.RemoveAll(e => e.Tag == tag);
            }

            entries.Add(new ToastEntry
            {
                Text = text,
                Kind = kind,
                StartedAt = DateTime.Now,
                Duration = duration * DurationMultiplier,
                Tag = tag,
            });

            while (MaxVisible > 0 && entries.Count > MaxVisible)
            {
                var oldest = entries[0];
                entries.RemoveAt(0);

                var elapsed = (DateTime.Now - oldest.StartedAt).TotalSeconds;
                oldest.Duration = Math.Min(oldest.Duration, elapsed + TrimFadeOutGrace);
                retiring.Add(oldest);
                recentTrims.Add(DateTime.Now);
            }
        }
    }

    public IReadOnlyList<ToastEntry> Active
    {
        get
        {
            lock (sync)
            {
                entries.RemoveAll(e => (DateTime.Now - e.StartedAt).TotalSeconds > e.Duration);
                retiring.RemoveAll(e => (DateTime.Now - e.StartedAt).TotalSeconds > e.Duration);

                var combined = new List<ToastEntry>(retiring.Count + entries.Count);
                combined.AddRange(retiring);
                combined.AddRange(entries);
                return combined;
            }
        }
    }

    public IReadOnlyList<HistoryEntry> History
    {
        get { lock (sync) return history.ToArray(); }
    }

    /// <summary>Number of toasts force-trimmed by MaxVisible overflow in the last few seconds - used
    /// for a brief "+N more" indicator, since the trimmed toasts themselves only flash for a moment.</summary>
    public int RecentTrimCount
    {
        get
        {
            lock (sync)
            {
                recentTrims.RemoveAll(t => (DateTime.Now - t).TotalSeconds > RecentTrimWindow);
                return recentTrims.Count;
            }
        }
    }

    public void ClearHistory()
    {
        lock (sync) history.Clear();
    }
}
