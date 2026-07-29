using System.Windows;
using System.Windows.Controls;

namespace Litbolt.Shell;

/// <summary>Contract for an app that can live inside Litbolt's App Folder and/or run alone.</summary>
public interface IAppModule
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    string? StandaloneExePath { get; }

    /// <summary>True when the module hosts real UI that can float in its own window.</summary>
    bool SupportsPopOut => true;

    /// <summary>Builds the UI hosted inside a Folder tab (or a popped-out window).</summary>
    FrameworkElement CreateView();

    void DisposeView(FrameworkElement view);
}

public sealed class ModuleTabState
{
    public required string ModuleId { get; init; }
    public required string Title { get; set; }
    public required FrameworkElement View { get; set; }
    public bool IsDetached { get; set; }
}

public static class KnownModuleIds
{
    public const string Home = "home";
    public const string Music = "music";
    public const string Video = "video";
    public const string Downloader = "downloader";
}
