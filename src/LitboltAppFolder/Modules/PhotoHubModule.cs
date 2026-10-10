using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Litbolt.Shell;
using LitboltAppFolder.Services;

namespace LitboltAppFolder.Modules;

public sealed class PhotoHubModule : IAppModule
{
    private readonly Func<string?> _resolveExe;

    public PhotoHubModule(Func<string?> resolveExe) => _resolveExe = resolveExe;

    public string Id => KnownModuleIds.Photo;
    public string DisplayName => "Photo Hub";
    public string Description => "Sort iPhone backup photos by face, manage people folders, and update the Rainmeter photo widget.";
    public string? StandaloneExePath => _resolveExe();

    public FrameworkElement CreateView() =>
        new ExternalAppModule(
            Id,
            DisplayName,
            Description,
            AppExePaths.DefaultPhotoHubExe(),
            Color.FromRgb(0xE8, 0xA0, 0x4A),
            _resolveExe).CreateView();

    public void DisposeView(FrameworkElement view) { }
}
