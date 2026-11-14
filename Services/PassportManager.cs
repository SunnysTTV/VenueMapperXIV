using System;
using System.Collections.Generic;
using System.Linq;
using VenueMapper.Models;

namespace VenueMapper.Services;

[Serializable]
public class PassportStamp
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Color2 { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Datacenter { get; set; } = string.Empty;
    public DateTime FirstVisit { get; set; }
    public DateTime LastVisit { get; set; }
    public int Visits { get; set; }
    public bool Night { get; set; }
}

[Serializable]
public class PassportBook
{
    public string Character { get; set; } = string.Empty;
    public Dictionary<string, PassportStamp> Stamps { get; set; } = new();
    public HashSet<PassportBadge> Badges { get; set; } = new();
}

public enum PassportBadge { First, Five, Ten, Regular, Districts, Night, Traveler, Complete }

public readonly record struct PassportResult(PassportStamp Stamp, bool NewStamp, bool NewVisit, IReadOnlyList<PassportBadge> Earned);

public sealed class PassportManager
{
    public static readonly PassportBadge[] AllBadges = Enum.GetValues<PassportBadge>();
    private const int RegularVisits = 10;
    private const int DistrictCount = 5;

    private readonly Configuration config;
    private readonly Action save;

    public PassportManager(Configuration config, Action? save = null)
    {
        this.config = config;
        this.save = save ?? config.Save;
    }

    public static string BookKey(ulong contentId) => contentId == 0 ? string.Empty : OwnerIdHelper.ComputeHash(contentId);

    public PassportBook? Find(ulong contentId)
    {
        var key = BookKey(contentId);
        return key.Length > 0 && config.Passports.TryGetValue(key, out var book) ? book : null;
    }

    public PassportBook? Book(ulong contentId)
    {
        var key = BookKey(contentId);
        if (key.Length == 0) return null;
        if (!config.Passports.TryGetValue(key, out var book))
        {
            book = new PassportBook();
            config.Passports[key] = book;
        }
        return book;
    }

    private static DateTime NightOf(DateTime local) => local.AddHours(-6).Date;

    public static string District(Venue v)
    {
        var parts = v.Address?.Split(" - ") ?? Array.Empty<string>();
        return parts.Length >= 3 ? parts[2].Trim() : string.Empty;
    }

    public PassportResult? Record(ulong contentId, string character, Venue venue, IReadOnlyList<Venue> all)
    {
        if (!config.PassportEnabled) return null;
        var book = Book(contentId);
        if (book == null) return null;

        var now = DateTime.Now;
        var isNew = !book.Stamps.TryGetValue(venue.VenueId, out var stamp);
        var newVisit = isNew;
        if (stamp == null)
        {
            stamp = new PassportStamp { FirstVisit = now, LastVisit = now, Visits = 1 };
            book.Stamps[venue.VenueId] = stamp;
        }
        else if (NightOf(now) != NightOf(stamp.LastVisit))
        {
            stamp.Visits++;
            newVisit = true;
        }
        stamp.LastVisit = now;
        if (newVisit && now.Hour < 4) stamp.Night = true;

        stamp.Name = venue.Name;
        stamp.Color = venue.Colors?.Primary ?? string.Empty;
        stamp.Color2 = venue.Colors?.Secondary ?? string.Empty;
        stamp.District = District(venue);
        stamp.Datacenter = venue.Datacenter;
        if (character.Length > 0) book.Character = character;

        var earned = new List<PassportBadge>();
        foreach (var badge in AllBadges)
        {
            if (book.Badges.Contains(badge) || !Qualifies(book, badge, all)) continue;
            book.Badges.Add(badge);
            earned.Add(badge);
        }

        save();
        return new PassportResult(stamp, isNew, newVisit, earned);
    }

    public bool ResetStamp(ulong contentId, string venueId)
    {
        if (Find(contentId) is not { } book || !book.Stamps.TryGetValue(venueId, out var stamp)) return false;
        stamp.Visits = 1;
        stamp.LastVisit = stamp.FirstVisit;
        save();
        return true;
    }

    public static int Visited(PassportBook book, IReadOnlyList<Venue> all)
        => all.Count(v => !v.Closed && book.Stamps.ContainsKey(v.VenueId));

    public static int Total(IReadOnlyList<Venue> all) => all.Count(v => !v.Closed);

    public static HashSet<PassportBadge> EarnedBadges(PassportBook book, IReadOnlyList<Venue> all)
        => AllBadges.Where(b => Qualifies(book, b, all)).ToHashSet();

    private static bool Qualifies(PassportBook book, PassportBadge badge, IReadOnlyList<Venue> all)
    {
        var stamps = book.Stamps.Values;
        return badge switch
        {
            PassportBadge.First => stamps.Count >= 1,
            PassportBadge.Five => stamps.Count >= 5,
            PassportBadge.Ten => stamps.Count >= 10,
            PassportBadge.Regular => stamps.Any(s => s.Visits >= RegularVisits),
            PassportBadge.Districts => stamps.Select(s => s.District.ToLowerInvariant()).Where(d => d.Length > 0).Distinct().Count() >= DistrictCount,
            PassportBadge.Night => stamps.Any(s => s.Night),
            PassportBadge.Traveler => stamps.Select(s => s.Datacenter.ToLowerInvariant()).Where(d => d.Length > 0).Distinct().Count() >= 2,
            PassportBadge.Complete => Total(all) > 0 && all.Where(v => !v.Closed).All(v => book.Stamps.ContainsKey(v.VenueId)),
            _ => false,
        };
    }
}
