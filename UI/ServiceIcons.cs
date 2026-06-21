using Dalamud.Interface;

namespace VenueMapper.UI;

public static class ServiceIcons
{
    public static FontAwesomeIcon GetIcon(string serviceType) => serviceType switch
    {
        "entrance" => FontAwesomeIcon.DoorOpen,
        "bar" => FontAwesomeIcon.Cocktail,
        "gambling" => FontAwesomeIcon.Dice,
        "dj_booth" => FontAwesomeIcon.Headphones,
        "upstairs" => FontAwesomeIcon.ArrowUp,
        "downstairs" => FontAwesomeIcon.ArrowDown,
        "store" => FontAwesomeIcon.StoreAlt,
        "crafting" => FontAwesomeIcon.Hammer,
        _ => FontAwesomeIcon.MapMarkerAlt,
    };
}
