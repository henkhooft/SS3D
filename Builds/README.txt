SS3D — quick start (this fork)

Not an official RE:SS3D build. Default port is 1151 everywhere.

WINDOWS (SS3D-Windows-<version>.zip)

  Start_SS3D_Host.bat     host a game on this PC (server + your own client)
  Start_SS3D_Client.bat   join a game; asks for your name and the server IP
  Game\SS3D.exe           run on its own to get the launcher

  Both bats also take arguments:
    Start_SS3D_Client.bat <name> <server ip> [port]
    Start_SS3D_Host.bat [name] [port]

LINUX CLIENT (SS3D-Linux-Client-<version>.zip)

  ./start_ss3d_host.sh                         host a game on this machine
  ./start_ss3d_client.sh <server ip> [port] [name]
  Game/SS3D-Client                             run on its own to get the launcher

LINUX DEDICATED SERVER (SS3D-Linux-Server-<version>.zip)

  ./start_ss3d_server.sh [port]                headless; logs to GameServer/server.log

Players on other machines join with the host's LAN or public IP; forward UDP 1151 for
internet play.

The host's default name, hostingUser, is ServerOwner (admin) via Game/Config/permissions.txt.
Add a line "<name> ServerOwner" there to make another name an admin.

If Config/ or Data/Tilemaps are missing next to the executable, maps and admin permissions
will not load (host log: "No station templates found to load"). Re-download the release.

Local Unity developers: build into Builds/Game (see Game/BUILD_THE_GAME_HERE.txt) and use
these same scripts from the Builds/ folder.
