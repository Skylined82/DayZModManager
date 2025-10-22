**DayZ Mod Manager**

A fast, no-nonsense Windows app to build DayZ mods, manage load orders, and spin up a local server + client in one click. Purpose-built for creators who iterate a lot and hate busywork.

**✨ Highlights**

> One-click build
Pack sources from WorkingMods\<YourMod> into BuiltMods\@<YourMod>\addons using AddonBuilder.

> Smart mod selection
Side-by-side picker for Workshop Mods and Your Built Mods with a dedicated Load Order list (double-click to add/remove; Up/Down to reorder).

> Launch server + client
Starts DayZ Server (or DayZDiag if you prefer), then auto-launches the client (BE or x64, with optional Diag client). All flags wired for local testing.

> Readable console log
Dark console with timestamped lines, soft-wrapping, and color-accented tokens:
Workshop Mods and Your Built Mods are bold + color coded.

> Mission selector
Polished “Select Mission” dialog; updates serverDZ.cfg → template = "<mission>".

> Purge logs
One click to delete *.log, *.RPT, *.mdmp, *.adm under Servers\profiles.

> Good UX by default
Remembers window size/position, smooth repaints while resizing, consistent buttons, tidy spacing.

> Zero magic folders
Everything lives under a workspace you choose:

<Workspace>\
  Missions\
  WorkingMods\
  BuiltMods\
  Servers\

**🖼️ Screens (quick look)**

Home: Actions on the left, color-coded console on the right
Build Mods: two columns (“Your source mods” → “Mods to build”)
Select Mods: Workshop | Built | Load Order (triple column)
Select Mission: compact list + consistent Save/Cancel
Settings: clear path rows + “Auto-detect common Steam paths”

**🚀 Quick Start**

Download & Install
Grab the latest MSI from Releases and run it.
(If building yourself, see Developer Notes below.)

Set Workspace
📁 Set workspace folder…
The app will ensure Missions/, WorkingMods/, BuiltMods/, Servers/ exist.

Configure Settings
⚙️ Settings… → set:

DayZ Game folder (and Server folder if separate)
AddonBuilder.exe
Workshop folder (e.g., DayZ\!Workshop or !dzsal)
Profiles folder (e.g., Servers\profiles)
serverDZ.cfg file path
(Use “Auto-detect common Steam paths” to quickly prefill.)
Pick a Mission
Drop missions into Missions\, then 🎯 Select mission… (writes serverDZ.cfg → template).

Build & Run

🧱 Build mod(s) → pick your source mods from WorkingMods\
🧩 Select mods… → add Workshop/Built mods into Load Order
▶️ Start server + client → watch the log for Workshop Mods / Your Built Mods and launch status

Tidy Up
🧹 Purge logs to clear noisy files under Servers\profiles.

**🧩 How it works (under the hood)**

Build: invokes AddonBuilder.exe "<source>" "<@outRoot>" -clear -packonly; verifies PBOS in @Mod\addons.
Load Order: persisted as ExtraMods in settings.json. When launching, each mod is resolved to either the Workshop path or BuiltMods path (falling back to the mod token if not found).
Launch Server: DayZServer_x64.exe or DayZDiag_x64.exe with -config, -profiles, -mission, and -mod (if any), plus logs flags.
Client: DayZ_BE.exe (preferred) or DayZ_x64.exe / DayZDiag_x64.exe with -profiles, -mod, -nolauncher (when applicable), and -connect=127.0.0.1 -port=2302.
Stop: kills DayZServer_x64, DayZ_x64, DayZDiag_x64, DayZ_BE (prompts elevation if needed).

**✅ Requirements**

Windows 10/11

DayZ (Game + optionally Server install)
DayZ Tools AddonBuilder.exe
.NET Desktop runtime matching the project target (e.g., .NET 8)

🛠️ Install / Build (developers)

Open the solution in Visual Studio (or dotnet build if targeting modern .NET).
Set the app project as Startup; run.
If you include a Setup/Installer project:
In the setup project properties set ProductName, Title, and Output file name to control the MSI name (e.g., DayZModManager Setup).
Clean + Rebuild the installer project.

**🧪 Typical Workflow**

Edit your mod in WorkingMods\<YourMod>\…
Build it from the app → PBOS land in BuiltMods\@<YourMod>\addons
Open Select mods… and include @<YourMod> in Load Order
Start server + client, test, repeat.

**🧯 Troubleshooting**

> Server/Client won’t launch
Double-check paths in Settings. Ensure you can run those EXEs manually.

> Mods not loading
Confirm the mod exists in !Workshop\@Mod or BuiltMods\@Mod and appears in Load Order.

> BattlEye blocked
The app prefers DayZ_BE.exe for the client if present; otherwise falls back to DayZ_x64.exe. Try the other path if you hit issues.

> Old logs are huge
Use Purge logs to clear *.log, *.RPT, *.mdmp, *.adm under Profiles.

🤝 Contributing

Issues and PRs welcome! If you’re adding UI:
Keep the clean, compact aesthetic
Use consistent button sizing and spacing
Prefer smooth resizing & wrapping (no clipped text)

📝 License

You are free to use, adjust to your needs or whatever you want to do with the program/code. No limits.

🙌 Credits

Built with WinForms and a custom polished UI theme
Thanks to DayZ'N'Chill for initial template - it inspired me to make significant improvements and additional functionality.
