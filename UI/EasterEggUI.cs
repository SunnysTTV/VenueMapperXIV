using VenueMapper.Services;

namespace VenueMapper.UI;

public static class EasterEggUI
{
    public static string GetName(string id) => id switch
    {
        EasterEggIds.RgbOverload    => Lang.EggRgbName,
        EasterEggIds.HackerMode     => Lang.EggHackerName,
        EasterEggIds.RandomTitle    => Lang.EggTitleName,
        _ => id,
    };

    public static string GetHint(string id) => id switch
    {
        EasterEggIds.RgbOverload    => Lang.EggRgbHint,
        EasterEggIds.HackerMode     => Lang.EggHackerHint,
        EasterEggIds.RandomTitle    => Lang.EggTitleHint,
        _ => "???",
    };
}
