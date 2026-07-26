using VenueMapper.Services;

namespace VenueMapper.UI;

public static class EasterEggUI
{
    public static string GetName(string id) => id switch
    {
        EasterEggIds.RgbOverload    => Lang.EggRgbName,
        EasterEggIds.HackerMode     => Lang.EggHackerName,
        EasterEggIds.SunnyDetection => Lang.EggSunnyName,
        EasterEggIds.WindowWobble   => Lang.EggWobbleName,
        EasterEggIds.RandomTitle    => Lang.EggTitleName,
        _ => id,
    };

    public static string GetHint(string id) => id switch
    {
        EasterEggIds.RgbOverload    => Lang.EggRgbHint,
        EasterEggIds.HackerMode     => Lang.EggHackerHint,
        EasterEggIds.SunnyDetection => Lang.EggSunnyHint,
        EasterEggIds.WindowWobble   => Lang.EggWobbleHint,
        EasterEggIds.RandomTitle    => Lang.EggTitleHint,
        _ => "???",
    };
}
