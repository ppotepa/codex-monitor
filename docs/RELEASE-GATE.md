# Release gate — baseline source

This repository can contain source version 0.5.1 while the newest GitHub
release asset is still v0.1.2. Do not equate source version with a published
installer or claim release support without a corresponding uploaded artifact.

Mandatory before the next release:

1. Run `dotnet build CodexMonitor.slnx -c Release` successfully on Windows,
   Linux and macOS (CI or local SDK). A failure before runner allocation
   does not validate the code.
2. Manually check Windows tray startup, menu actions, Limits/Tokens, repaired
   CLI detection, settings and alert behavior after plugin changes.
3. Validate Wayland/X11 tray fallback on Linux and menu bar/LaunchAgent
   behavior on macOS on real desktops.
4. Produce and inspect the Windows Inno Setup executable, Linux Debian
   package and macOS app bundle using the existing packaging scripts.
5. Verify native notification permissions, macOS signing and notarization
   before distributing a public macOS app.
6. Publish tagged assets only after those checks pass.

No automated behavioral tests are added for this phase of the project.
