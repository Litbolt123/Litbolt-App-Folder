# Litbolt's App Folder

Parent shell for Litbolt apps — home dashboard, tabbed modules, detachable windows.
Each app still runs standalone; the Folder can host them and let you pop a tab out.

## Apps (v1)

| Module | Standalone | In Folder |
|--------|------------|-----------|
| Local Music Hub | yes | Open / Pop out |
| YouTube Downloader | yes | Open / Pop out |
| Local Video Hub | yes (`LocalVideoHub.exe`) | Embedded + Pop out |

## Updates

WAID-style GitHub Releases check — see [docs/updating-app.md](docs/updating-app.md).

## Dev

```powershell
dotnet build .\LitboltAppFolder.sln -c Release
```

Release builds sync to `%LocalAppData%\Programs\LitboltAppFolder\` and `%LocalAppData%\Programs\LocalVideoHub\`.

## Version

See `Directory.Build.props`.
