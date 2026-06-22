using System;
using System.Collections.Generic;
using System.Linq;

namespace VenueMapper.Models;

public static class ServerData
{
    public static readonly Dictionary<string, List<string>> DatacenterServers = new()
    {
        ["Aether"]   = ["Adamantoise", "Cactuar", "Faerie", "Gilgamesh", "Jenova", "Midgardsormr", "Sargatanas", "Siren"],
        ["Primal"]   = ["Behemoth", "Excalibur", "Exodus", "Famfrit", "Hyperion", "Lamia", "Leviathan", "Ultros"],
        ["Crystal"]  = ["Balmung", "Brynhildr", "Coeurl", "Diabolos", "Goblin", "Malboro", "Mateus", "Zalera"],
        ["Dynamis"]  = ["Cuchulainn", "Golem", "Halicarnassus", "Kraken", "Maduin", "Marilith", "Rafflesia", "Seraph"],
        ["Light"]    = ["Alpha", "Lich", "Odin", "Phoenix", "Raiden", "Shiva", "Twintania", "Zodiark"],
        ["Chaos"]    = ["Cerberus", "Louisoix", "Moogle", "Omega", "Phantom", "Ragnarok", "Sagittarius", "Spriggan"],
        ["Materia"]  = ["Bismarck", "Ravana", "Sephirot", "Sophia", "Zurvan"],
        ["Mana"]     = ["Anima", "Asura", "Chocobo", "Hades", "Ixion", "Masamune", "Pandaemonium", "Titan"],
        ["Gaia"]     = ["Alexander", "Bahamut", "Durandal", "Fenrir", "Ifrit", "Ridill", "Tiamat", "Ultima"],
        ["Elemental"]= ["Aegis", "Atomos", "Carbuncle", "Garuda", "Gungnir", "Kujata", "Tonberry", "Typhon"],
        ["Meteor"]   = ["Belias", "Mandragora", "Ramuh", "Shinryu", "Unicorn", "Valefor", "Yojimbo", "Zeromus"],
    };

    public static string[] AllDatacenters => DatacenterServers.Keys.OrderBy(x => x).ToArray();

    public static string[] GetServers(string dc)
        => DatacenterServers.TryGetValue(dc, out var s) ? s.OrderBy(x => x).ToArray() : [];
}
