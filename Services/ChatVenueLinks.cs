using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using VenueMapper.Models;

namespace VenueMapper.Services;

[Flags]
public enum ChatLinkChannels
{
    None = 0,
    Say = 1,
    ShoutYell = 2,
    Tell = 4,
    Party = 8,
    FreeCompany = 16,
    Linkshell = 32,
    CrossLinkshell = 64,
    Novice = 128,
    All = 255,
}

public readonly record struct ChatVenueHit(int Start, int Length, Venue Venue);

public sealed class ChatVenueLinks : IDisposable
{
    private const ushort LinkColor = 576;
    private const int MaxLinksPerMessage = 3;
    private const int MinNameLength = 4;

    private static readonly string[][] DistrictAliases =
    [
        ["Mist", "Dorf des Nebels"],
        ["The Lavender Beds", "Lavender Beds", "Lavendelbeete", "Lavender", "LB"],
        ["The Goblet", "Goblet", "Kelchkuppe"],
        ["Shirogane"],
        ["Empyreum", "Empyrium", "Empy"],
    ];

    private static readonly Regex ContextWords = new(
        @"(?<![\p{L}\p{N}])(?:venue|club|lounge|bar|party|tonight|open|opens|opening|doors|event|dj|ward|plot|w\s?\d{1,2}|heute|abend|geöffnet|offen|eröffnung|bezirk)(?![\p{L}\p{N}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex[] AddressPatterns = BuildAddressPatterns();

    private static readonly Regex SiteLink = new(
        $@"(?<![\p{{L}}\p{{N}}])(?:https?://)?{Regex.Escape(UI.SiteLinks.Host)}{Regex.Escape(UI.SiteLinks.VenuePath)}(?<id>[A-Za-z0-9_\-]{{1,64}})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly VenueMapperPlugin plugin;
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;
    private readonly HashSet<string> worlds;
    private readonly Dictionary<string, DalamudLinkPayload> linkPayloads = new();
    private readonly Dictionary<uint, string> commandVenues = new();
    private uint nextCommandId = 1;


    public ChatVenueLinks(VenueMapperPlugin plugin, IChatGui chatGui, IDataManager data, IPluginLog log)
    {
        this.plugin = plugin;
        this.chatGui = chatGui;
        this.log = log;
        worlds = data.GetExcelSheet<World>()
            .Where(w => w.IsPublic)
            .Select(w => w.Name.ExtractText())
            .Where(n => n.Length > 2)
            .ToHashSet(StringComparer.Ordinal);
        chatGui.ChatMessage += OnChatMessage;
    }

    private static Regex[] BuildAddressPatterns()
    {
        var districts = string.Join("|", DistrictAliases.SelectMany(a => a).OrderByDescending(a => a.Length).Select(Regex.Escape));
        const string ward = @"(?:w|ward|wd|bezirk)\.?\s?(?<w>\d{1,2})";
        const string plot = @"(?:p|plot|pl|grundstück|parzelle|nr)\.?\s?(?<p>\d{1,2})";
        const string sep = @"[\s,.:;\-/|]{0,3}";
        const string open = @"(?<![\p{L}\p{N}])";
        const string close = @"(?![\p{L}\p{N}])";
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        return
        [
            new Regex($"{open}(?<d>{districts}){sep}{ward}{sep}{plot}{close}", options),
            new Regex($@"{open}{ward}{sep}{plot}[\s,]{{0,3}}(?:(?:in|im|at|@)\s+)?(?<d>{districts}){close}", options),
        ];
    }

    public static bool ChannelAllowed(ChatLinkChannels allowed, XivChatType type)
    {
        var channel = type switch
        {
            XivChatType.Echo => ChatLinkChannels.All,
            XivChatType.Say => ChatLinkChannels.Say,
            XivChatType.Shout or XivChatType.Yell => ChatLinkChannels.ShoutYell,
            XivChatType.TellIncoming or XivChatType.TellOutgoing => ChatLinkChannels.Tell,
            XivChatType.Party or XivChatType.CrossParty or XivChatType.Alliance or XivChatType.PvPTeam => ChatLinkChannels.Party,
            XivChatType.FreeCompany => ChatLinkChannels.FreeCompany,
            XivChatType.Ls1 or XivChatType.Ls2 or XivChatType.Ls3 or XivChatType.Ls4
                or XivChatType.Ls5 or XivChatType.Ls6 or XivChatType.Ls7 or XivChatType.Ls8 => ChatLinkChannels.Linkshell,
            XivChatType.CrossLinkShell1 or XivChatType.CrossLinkShell2 or XivChatType.CrossLinkShell3 or XivChatType.CrossLinkShell4
                or XivChatType.CrossLinkShell5 or XivChatType.CrossLinkShell6 or XivChatType.CrossLinkShell7 or XivChatType.CrossLinkShell8 => ChatLinkChannels.CrossLinkshell,
            XivChatType.NoviceNetwork => ChatLinkChannels.Novice,
            _ => ChatLinkChannels.None,
        };
        if (channel == ChatLinkChannels.All) return allowed != ChatLinkChannels.None;
        return channel != ChatLinkChannels.None && (allowed & channel) != 0;
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        try
        {
            var cfg = plugin.Configuration;
            if (!cfg.ChatLinks || !ChannelAllowed(cfg.ChatLinkChannels, message.LogKind)) return;
            var venues = plugin.ConfigManager.Config?.Venues;
            if (venues == null || venues.Count == 0) return;

            var seString = message.Message;
            var fullText = seString.TextValue;
            if (fullText.Length < MinNameLength) return;

            var rewritten = Rewrite(seString.Payloads, fullText, venues);
            if (rewritten == null) return;
            seString.Payloads.Clear();
            seString.Payloads.AddRange(rewritten);
        }
        catch (Exception ex)
        {
            log.Debug($"[VenueMapper] Chat links: {ex.Message}");
        }
    }

    public void PrintDemo(string text)
    {
        var venues = plugin.ConfigManager.Config?.Venues;
        if (venues == null) return;
        var payloads = Rewrite([new TextPayload(text)], text, venues) ?? [new TextPayload(text)];
        chatGui.Print(new XivChatEntry { Type = XivChatType.Echo, Message = new SeString(payloads) });
    }

    private static bool StartsLink(Payload payload) => payload.Type is PayloadType.Player or PayloadType.Item or PayloadType.Status
        or PayloadType.MapLink or PayloadType.Quest or PayloadType.DalamudLink or PayloadType.PartyFinder;

    private List<Payload>? Rewrite(List<Payload> payloads, string fullText, IReadOnlyList<Venue> venues)
    {
        var mentionedWorlds = MentionedWorlds(fullText, worlds);
        var currentServer = plugin.PositionTracker.CurrentServerName;
        var output = new List<Payload>(payloads.Count + 6);
        var used = new HashSet<string>();
        var inLink = false;
        var changed = false;

        foreach (var payload in payloads)
        {
            if (payload is RawPayload raw && raw.Equals(RawPayload.LinkTerminator))
            {
                inLink = false;
                output.Add(payload);
                continue;
            }
            if (StartsLink(payload))
            {
                inLink = true;
                output.Add(payload);
                continue;
            }
            if (inLink || used.Count >= MaxLinksPerMessage || payload is not TextPayload { Text: { Length: >= MinNameLength } text })
            {
                output.Add(payload);
                continue;
            }

            var hits = FindHits(text, fullText, venues, mentionedWorlds, used, currentServer);
            if (hits.Count == 0)
            {
                output.Add(payload);
                continue;
            }

            var pos = 0;
            foreach (var hit in hits)
            {
                if (hit.Start > pos) output.Add(new TextPayload(text[pos..hit.Start]));
                output.Add(LinkFor(hit.Venue));
                output.Add(new UIForegroundPayload(LinkColor));
                output.Add(new TextPayload(SeIconChar.LinkMarker.ToIconString() + text.Substring(hit.Start, hit.Length)));
                output.Add(UIForegroundPayload.UIForegroundOff);
                output.Add(RawPayload.LinkTerminator);
                pos = hit.Start + hit.Length;
                used.Add(hit.Venue.VenueId);
            }
            if (pos < text.Length) output.Add(new TextPayload(text[pos..]));
            changed = true;
        }

        return changed ? output : null;
    }

    private static HashSet<string> MentionedWorlds(string text, IEnumerable<string> worlds)
        => worlds.Where(w => ContainsWord(text, w, StringComparison.Ordinal)).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public List<ChatVenueHit> FindIn(string text, string? server)
    {
        var venues = plugin.ConfigManager.Config?.Venues;
        if (venues == null || venues.Count == 0 || text.Length < MinNameLength) return [];
        return FindHits(text, text, venues, MentionedWorlds(text, worlds), new HashSet<string>(), server);
    }

    public static List<ChatVenueHit> Find(string text, IReadOnlyList<Venue> venues, IEnumerable<string> worlds, string? currentServer)
        => FindHits(text, text, venues, MentionedWorlds(text, worlds), new HashSet<string>(), currentServer);

    private static List<ChatVenueHit> FindHits(string text, string fullText, IReadOnlyList<Venue> venues, HashSet<string> mentionedWorlds, HashSet<string> used, string? currentServer)
    {
        var found = new List<ChatVenueHit>();

        foreach (Match m in SiteLink.Matches(text))
        {
            var id = m.Groups["id"].Value;
            var venue = venues.FirstOrDefault(v => string.Equals(v.VenueId, id, StringComparison.OrdinalIgnoreCase));
            if (venue != null && !used.Contains(venue.VenueId))
                found.Add(new ChatVenueHit(m.Index, m.Length, venue));
        }

        foreach (var pattern in AddressPatterns)
        {
            foreach (Match m in pattern.Matches(text))
            {
                if (ResolveAddress(m, venues, mentionedWorlds, currentServer) is { } venue && !used.Contains(venue.VenueId))
                    found.Add(new ChatVenueHit(m.Index, m.Length, venue));
            }
        }

        foreach (var venue in venues)
        {
            if (used.Contains(venue.VenueId)) continue;
            if (mentionedWorlds.Count > 0 && !mentionedWorlds.Contains(venue.Server)) continue;
            foreach (var name in NameVariants(venue))
            {
                var index = FindWord(text, name, StringComparison.Ordinal);
                if (index < 0) continue;
                if (IsWeakName(name) && (!HasContext(fullText, venue) || PrecededByThe(text, index, name))) continue;
                found.Add(new ChatVenueHit(index, name.Length, venue));
                break;
            }
        }

        var result = new List<ChatVenueHit>();
        var end = 0;
        var seen = new HashSet<string>();
        foreach (var hit in found.OrderBy(h => h.Start).ThenByDescending(h => h.Length))
        {
            if (hit.Start < end || !seen.Add(hit.Venue.VenueId)) continue;
            result.Add(hit);
            end = hit.Start + hit.Length;
            if (used.Count + result.Count >= MaxLinksPerMessage) break;
        }
        return result;
    }

    private static IEnumerable<string> NameVariants(Venue venue)
    {
        var name = venue.Name.Trim();
        if (name.Length >= MinNameLength) yield return name;
        var clean = Regex.Replace(new string(name.Where(c => !char.IsSurrogate(c) && c != '\u200D' && c != '\uFE0F').ToArray()), @"\s{2,}", " ").Trim();
        if (clean != name && clean.Length >= MinNameLength) yield return clean;
    }

    private static bool IsWeakName(string name)
    {
        if (name.Contains(' ') || !name.All(char.IsLetter)) return false;
        return !name.Skip(1).Any(char.IsUpper);
    }

    private static bool PrecededByThe(string text, int index, string name)
    {
        if (name.StartsWith("The ", StringComparison.OrdinalIgnoreCase)) return false;
        var before = text[..index].TrimEnd();
        return before.EndsWith("the", StringComparison.OrdinalIgnoreCase)
               && (before.Length == 3 || !char.IsLetterOrDigit(before[^4]));
    }

    private static bool HasContext(string fullText, Venue venue)
    {
        if (ContextWords.IsMatch(fullText)) return true;
        if (ContainsWord(fullText, venue.Server, StringComparison.OrdinalIgnoreCase)) return true;
        if (ContainsWord(fullText, venue.Datacenter, StringComparison.OrdinalIgnoreCase)) return true;
        var district = DistrictIndex(PassportManager.District(venue));
        return district >= 0 && DistrictAliases[district].Any(a => ContainsWord(fullText, a, StringComparison.OrdinalIgnoreCase));
    }

    private static int DistrictIndex(string name)
    {
        for (var i = 0; i < DistrictAliases.Length; i++)
        {
            if (DistrictAliases[i].Any(a => string.Equals(a, name.Trim(), StringComparison.OrdinalIgnoreCase)))
                return i;
        }
        return -1;
    }

    private static Venue? ResolveAddress(Match m, IReadOnlyList<Venue> venues, HashSet<string> mentionedWorlds, string? currentServer)
    {
        if (!int.TryParse(m.Groups["w"].Value, out var ward) || !int.TryParse(m.Groups["p"].Value, out var plot)) return null;
        if (ward is < 1 or > 30 || plot is < 1 or > 60) return null;
        var district = DistrictIndex(m.Groups["d"].Value);
        if (district < 0) return null;

        var candidates = venues.Where(v => v.Ward == ward && v.Plot == plot && DistrictIndex(PassportManager.District(v)) == district).ToList();
        if (mentionedWorlds.Count > 0)
            candidates = candidates.Where(v => mentionedWorlds.Contains(v.Server)).ToList();
        if (candidates.Count == 1) return candidates[0];
        if (candidates.Count == 0 || mentionedWorlds.Count > 0) return null;

        var local = candidates.Where(v => string.Equals(v.Server, currentServer, StringComparison.OrdinalIgnoreCase)).ToList();
        return local.Count == 1 ? local[0] : null;
    }

    private static int FindWord(string text, string word, StringComparison comparison)
    {
        if (string.IsNullOrEmpty(word)) return -1;
        var start = 0;
        while (start <= text.Length - word.Length)
        {
            var index = text.IndexOf(word, start, comparison);
            if (index < 0) return -1;
            var beforeOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var after = index + word.Length;
            var afterOk = after >= text.Length || !char.IsLetterOrDigit(text[after]);
            if (beforeOk && afterOk) return index;
            start = index + 1;
        }
        return -1;
    }

    private static bool ContainsWord(string text, string word, StringComparison comparison) => FindWord(text, word, comparison) >= 0;

    private DalamudLinkPayload LinkFor(Venue venue)
    {
        if (linkPayloads.TryGetValue(venue.VenueId, out var payload)) return payload;
        var id = nextCommandId++;
        payload = chatGui.AddChatLinkHandler(id, OnLinkClicked);
        linkPayloads[venue.VenueId] = payload;
        commandVenues[id] = venue.VenueId;
        return payload;
    }

    private void OnLinkClicked(uint commandId, SeString _)
    {
        if (!commandVenues.TryGetValue(commandId, out var venueId)) return;
        var venue = plugin.ConfigManager.Config?.Venues.FirstOrDefault(v => v.VenueId == venueId);
        if (venue != null)
            plugin.VenueDetailWindow.Open(venue);
    }

    public void Dispose()
    {
        chatGui.ChatMessage -= OnChatMessage;
        chatGui.RemoveChatLinkHandler();
        linkPayloads.Clear();
        commandVenues.Clear();
    }
}
