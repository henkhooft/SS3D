SS3D — quick start (this fork)

Not an official RE:SS3D build. Unzip so you have:

  Start_SS3D_Host.bat
  Start_SS3D_Client_*.bat
  Game\SS3D.exe
  Game\Config\permissions.txt
  Game\Data\Tilemaps\...
  ...

1. Double-click Start_SS3D_Host.bat (self-hosts; skips the launcher).
   Host needs loopback for its own client: `-host -ip=127.0.0.1 …`. If an older zip's Host.bat
   omits `-ip=`, edit it to put `-ip=127.0.0.1` *before* `-host` (or re-download a newer cut).
2. On the same PC (or another on the LAN), run a Client bat — default is 127.0.0.1:1151.
   Client bats only differ by ckey; language is an in-game setting.

If Config/ or Data/Tilemaps are missing next to the exe, maps and admin permissions will not
load (host log: "No station templates found to load"). Re-download a newer prerelease, or copy
those folders from a local Builds/Game tree.

Dedicated Linux server binaries are a separate download on the same GitHub prerelease
when cut via Actions → Develop Release. Day-to-day play does not need them: Host.bat is enough.

Local Unity developers: build into Builds/Game (see Game/BUILD_THE_GAME_HERE.txt) and use
these same bats from the Builds/ folder.
