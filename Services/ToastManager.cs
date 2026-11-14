using System;
using System.Collections.Generic;
using System.Numerics;

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
        public string? Detail;
        public Vector4? Color;
        public Vector4? Color2;
        public bool Banner;

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
    private readonly List<ToastEntry> banners = new();

    public bool BannersEnabled = true;
    public bool BannersMuted;

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
                foreach (var e in banners) e.StartedAt += pausedFor;
                pauseStartedAt = null;
            }
        }
    }

    public void Announce(string title, string? detail, ToastKind kind, Vector4? color = null, Vector4? color2 = null, double duration = 4.5)
    {
        if (BannersMuted) return;
        if (!BannersEnabled)
        {
            Show(title, kind, duration, null, detail, color, color2);
            return;
        }

        lock (sync)
        {
            AddHistory(detail != null ? $"{title} · {detail}" : title, kind);
            var start = DateTime.Now;
            if (banners.Count > 0)
            {
                var last = banners[^1];
                var lastEnd = last.StartedAt + TimeSpan.FromSeconds(last.Duration);
                if (lastEnd > start) start = lastEnd;
            }
            banners.Add(new ToastEntry
            {
                Text = title,
                Detail = detail,
                Kind = kind,
                Color = color,
                Color2 = color2,
                Banner = true,
                StartedAt = start,
                Duration = duration * DurationMultiplier,
            });
        }
    }

    public ToastEntry? ActiveBanner
    {
        get
        {
            lock (sync)
            {
                banners.RemoveAll(e => (DateTime.Now - e.StartedAt).TotalSeconds > e.Duration);
                return banners.Count > 0 && banners[0].StartedAt <= DateTime.Now ? banners[0] : null;
            }
        }
    }

    private void AddHistory(string text, ToastKind kind)
    {
        history.Add(new HistoryEntry(text, kind, DateTime.Now));
        if (history.Count > MaxHistory)
            history.RemoveAt(0);
    }

    public void Show(string text, ToastKind kind = ToastKind.Info, double duration = 3.0, string? tag = null,
        string? detail = null, Vector4? color = null, Vector4? color2 = null)
    {
        lock (sync)
        {
            history.Add(new HistoryEntry(detail != null ? $"{text} · {detail}" : text, kind, DateTime.Now));
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
                Detail = detail,
                Color = color,
                Color2 = color2,
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

    public void DismissAll(bool includeBanners)
    {
        lock (sync)
        {
            entries.Clear();
            retiring.Clear();
            recentTrims.Clear();
            if (includeBanners) banners.Clear();
        }
    }
}
