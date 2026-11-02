using System;
using System.Collections.Generic;

namespace SS3D.UI.Shell.MainMenu
{
    public enum MainMenuScreen { Menu = 0, Servers = 1, Settings = 2, Connecting = 3, Hosting = 4, Updating = 5 }
    public enum MainMenuPublicListState { Populated = 0, Empty = 1, FetchFailed = 2 }
    public enum MainMenuServerState { Compatible = 0, NeedsUpdate = 1, Full = 2, Offline = 3, OlderVersion = 4 }

    /// <summary>Presentation data only. Does not query servers or persist credentials.</summary>
    public sealed class MainMenuPresentation
    {
        public MainMenuScreen Screen;
        public string PlayerName = "";
        public string Version = "";
        public string LatestVersion = "";
        public string UpdateMessage = "";
        public bool UpdateAvailable;
        public bool FirstLaunch;
        public bool Advanced;
        public string Port = "7777";
        public string Address = "";
        public string AddressError = "";
        public string CheckedText = "Checked just now";
        public int Wallpaper;
        public MainMenuPublicListState PublicList;
        public IReadOnlyList<MainMenuServerPresentation> Recent = Array.Empty<MainMenuServerPresentation>();
        public IReadOnlyList<MainMenuServerPresentation> Public = Array.Empty<MainMenuServerPresentation>();
        public string OperationName = "";
        public string OperationAddress = "";
        public int OperationStep;
    }

    public sealed class MainMenuServerPresentation
    {
        public string Id = "";
        public string Name = "";
        public string Address = "";
        public MainMenuServerState State;
        public string RequiredVersion = "";
        public string Players = "—";
        public string Ping = "—";
        public string Refusal = "";
        public string RefusalTime = "";
        public bool Expanded;
        public string AdminToken = "";
    }
}
