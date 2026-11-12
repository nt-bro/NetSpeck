# Contributing

Bug reports and focused pull requests are welcome. The project is a Windows 11 preview and uses an unofficial taskbar integration, so include the exact Windows build for taskbar-related changes.

## Local development

1. Use Windows x64 with .NET Framework 4.8.
2. Run `powershell -NoProfile -File .\build.ps1` to compile and run the offline checks.
3. Use `powershell -NoProfile -File .\scripts\package.ps1` to create the portable ZIP and SHA-256 checksum.

The compiler is the Framework compiler installed with Windows; keep source compatible with C# 5. There are no external NuGet dependencies. Built-in icon resources are in `icons`; imported user fonts/icons must not be committed.

Interactive checks (`build.ps1 -Integration -Menus`) require a compatible Windows 11 desktop and an active routed adapter. Close the regular app first. They temporarily change and restore local preferences, create a taskbar child HWND, and test settings/menu behavior. They do not trigger sleep or restart Explorer. Do not run a real sleep/reboot test without the computer user's explicit approval.

GitHub-hosted CI runs compilation and offline checks only. A Windows Server build runner is not a supported runtime target for the app.

## Reporting problems

Use the issue template and include the Windows build, scale, taskbar layout, selected adapter mode, expected behavior and steps to reproduce. Do not publish tokens, user folders, adapter identifiers, or unreviewed diagnostic screenshots.

## Changes

Keep changes focused and preserve independent numeric averaging, graph smoothing, user preferences and the taskbar child-window behavior. Check both interface languages when changing UI text or layout. Update `VERSION`, assembly metadata, release notes and download links when preparing a new version.

Contributions are provided under the repository's MIT license.
