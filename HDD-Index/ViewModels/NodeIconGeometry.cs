namespace HDD_Index.ViewModels;

/// <summary>
/// Shared, resolution-independent outlines for the shell and tree templates.
/// All paths use a 24-unit view box and deliberately contain no bitmap assets.
/// </summary>
public static class NodeIconGeometry
{
    public const string Folder = "M3,6 C3,4.9 3.9,4 5,4 H9 L12,7 H19 C20.1,7 21,7.9 21,9 V19 C21,20.1 20.1,21 19,21 H5 C3.9,21 3,20.1 3,19 Z M3,9 H21";
    public const string File = "M6,3 H14 L19,8 V20 C19,20.6 18.6,21 18,21 H6 C5.4,21 5,20.6 5,20 V4 C5,3.4 5.4,3 6,3 Z M14,3 V8 H19";
    public const string Disk = "M6,3 H18 L21,18 V21 H3 V18 Z M3,17 H21 M16,19 H18";
    public const string Link = "M10,14 L14,10 M8,16 L6.5,17.5 C5.1,18.9 2.9,18.9 1.5,17.5 C0.1,16.1 0.1,13.9 1.5,12.5 L6.5,7.5 C7.9,6.1 10.1,6.1 11.5,7.5 M12.5,16.5 C13.9,17.9 16.1,17.9 17.5,16.5 L22.5,11.5 C23.9,10.1 23.9,7.9 22.5,6.5 C21.1,5.1 18.9,5.1 17.5,6.5 L16,8";
    public const string Search = "M16.5,10 A6.5,6.5 0 1 1 3.5,10 A6.5,6.5 0 1 1 16.5,10 M15,15 L21,21";
    public const string Save = "M4,3 H17 L21,7 V21 H3 V3 Z M7,3 V10 H17 V3 M7,21 V14 H17 V21";
    public const string Plus = "M12,4 V20 M4,12 H20";
    public const string ChevronLeft = "M15,5 L8,12 L15,19";
    public const string ChevronRight = "M9,5 L16,12 L9,19";
    public const string ChevronUp = "M5,15 L12,8 L19,15";
    public const string ChevronDown = "M5,9 L12,16 L19,9";

    public static string GetPath(string kind) => kind switch
    {
        "File" => File,
        "Disk" => Disk,
        "Link" => Link,
        "Search" => Search,
        "Save" => Save,
        "Plus" => Plus,
        "ChevronLeft" => ChevronLeft,
        "ChevronRight" => ChevronRight,
        "ChevronUp" => ChevronUp,
        "ChevronDown" => ChevronDown,
        _ => Folder,
    };
}
