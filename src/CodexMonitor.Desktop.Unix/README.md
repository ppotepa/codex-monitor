# Unix desktop host

A code-only Avalonia 11 application shared by Linux and macOS. It owns
`UnixDashboardWindow`, `UnixTrayShell`, settings UI and desktop timers.
All OS- and Codex-specific services come from the composition root.

The host implements the shared `IDesktopHost` and `IDashboardView`
contracts. No WinForms reference is required.

See `docs/UNIX.md` for preview behavior, publishing and remaining validation.
