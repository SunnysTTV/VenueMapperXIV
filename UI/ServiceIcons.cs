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
        "vip" => FontAwesomeIcon.Crown,
        "bath" or "spa" => FontAwesomeIcon.Bath,
        "event" => FontAwesomeIcon.Star,
        "stage" => FontAwesomeIcon.MicrophoneAlt,
        _ => FontAwesomeIcon.MapMarkerAlt,
    };
}
