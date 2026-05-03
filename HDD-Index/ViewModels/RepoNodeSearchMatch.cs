using Avalonia.Controls;

namespace HDD_Index.ViewModels;

public sealed record RepoNodeSearchMatch(
    RepoNodeVM Node,
    IndexPath IndexPath);
