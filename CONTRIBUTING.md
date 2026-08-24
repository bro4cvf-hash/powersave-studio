# Contributing to PowerSave Studio

Thanks for helping polish PowerSave!

## Setup
- Windows 10/11
- .NET 8 SDK (`dotnet --list-sdks`)
- `dotnet build -c Release` should pass with 0 warnings (except intentional dotnet format whitespace if you ignore).

## Guidelines
- **Stay featherweight**: <1 MB framework-dependent single exe, 0% idle CPU, ~10-25 MB RAM. No new polling loops.
- **Stay WinForms**: No WPF/MAUI migration.
- **UI**: Use `UI/Theme.cs:1` tokens (colors, radii, `Theme.S()` for DPI). Cards use `Squircle`, not `RoundRect` directly.
- **Tweaks**: Only visual/powercfg. Don't add registry bloat removals (widgets/background apps) — those were intentionally removed.
- **Single-instance**: Test `PowerSave.exe --apply=powersave` from second process while main runs.
- **Logging**: Use `Infra/Logger.cs:1` (`Logger.Info/Warn/Error`) — logs to `%LOCALAPPDATA%\PowerSave\powersave.log`.

## PR Checklist
- [ ] `.\build.ps1` succeeds
- [ ] No new `bin/`/`obj/`/`publish/` committed (see `.gitignore`)
- [ ] Tested on at least one laptop + one desktop (battery vs no-battery)
- [ ] Updated README if you add a CLI flag or toggle

## Code style
- `nullable: enable`, `ImplicitUsings: enable`
- File-scoped namespaces, `required` init where appropriate
- Use `Theme.S()` for every pixel that should scale with DPI

## Reporting bugs
Open an issue with:
- Windows build (`winver`)
- `%LOCALAPPDATA%\PowerSave\powersave.log` tail
- `%LOCALAPPDATA%\PowerSave\settings.json` (redact if needed)
