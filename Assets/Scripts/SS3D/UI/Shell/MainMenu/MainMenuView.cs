using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.Shell.MainMenu
{
    /// <summary>
    /// Unwired presentation of the supplied main-menu reference. Attach explicitly to a shell layer;
    /// no bootstrap, networking, settings writes, URL launches or application lifecycle actions.
    /// </summary>
    public sealed class MainMenuView : IUiSurface
    {
        public VisualElement Root { get; }
        private readonly MainMenuAssetCatalog _catalog;
        private Texture2D _blurredWallpaper;
        private Texture2D _blurredSource;

        public MainMenuView(MainMenuAssetCatalog catalog)
        {
            _catalog = catalog;
            Root = catalog.Template.CloneTree();
            Root.style.flexGrow = 1;
            Root.styleSheets.Add(catalog.Style);
            Root.Q("logo").style.backgroundImage = catalog.Logo;
            Root.Query(className: "main-menu__empty").ForEach(DashedBorder);
            Root.Q("main-menu").RegisterCallback<GeometryChangedEvent>(e =>
            {
                Root.Q("main-menu").EnableInClassList("main-menu--compact", e.newRect.width < 1450);
                Root.Q<ScrollView>("sidebar").contentContainer.style.minHeight = e.newRect.height;
            });
        }

        public void Attach(VisualElement layerRoot) { Detach(); layerRoot.Add(Root); }
        public void Detach()
        {
            Root.RemoveFromHierarchy();
            if (_blurredWallpaper != null)
            {
                if (UnityEngine.Application.isPlaying) Object.Destroy(_blurredWallpaper);
                else Object.DestroyImmediate(_blurredWallpaper);
                _blurredWallpaper = null;
                _blurredSource = null;
            }
        }

        public void Present(MainMenuPresentation model)
        {
            Root.Q("wallpaper").style.backgroundImage = _catalog.Wallpaper(model.Wallpaper);
            Root.Q<TextField>("player-name").SetValueWithoutNotify(model.PlayerName);
            Root.Q<TextField>("port").SetValueWithoutNotify(model.Port);
            Root.Q<Label>("version").text = $"v{model.Version} pre-alpha";
            Show("update-banner", model.UpdateAvailable);
            Root.Q<Label>("update-copy").text = $"v{model.LatestVersion} is out; this is v{model.Version}. {model.UpdateMessage}";
            bool hasName = !string.IsNullOrWhiteSpace(model.PlayerName);
            Root.Q<Button>("join").SetEnabled(hasName);
            Root.Q<Button>("local").SetEnabled(hasName);
            string joinSubtitle = "Recent, public or direct connect";
            if (!hasName) joinSubtitle = "Needs a name";
            else if (model.FirstLaunch) joinSubtitle = "Paste an address from Discord";
            else if (model.Recent.Count > 0) joinSubtitle = $"Last played: {model.Recent[0].Name}";
            Root.Q<Label>("join-subtitle").text = joinSubtitle;
            Root.Q<Label>("local-subtitle").text = hasName ? $"Host on this computer, port {model.Port}" : "Needs a name";
            Show("name-hint", !hasName || model.FirstLaunch);
            Root.Q<Label>("name-hint").text = !hasName ? "Enter a name to join or play." : "Taken from this computer's user name. Change it any time.";
            Root.Q<Label>("name-hint").style.color = hasName ? new Color(0.667f, 0.667f, 0.667f) : new Color(0.941f, 0.769f, 0.49f);
            Show("port-panel", model.Advanced);
            Root.Q("advanced-caret").EnableInClassList("main-menu__caret--up", model.Advanced);
            bool servers = model.Screen == MainMenuScreen.Servers;
            bool settings = model.Screen == MainMenuScreen.Settings;
            Root.Q("settings").EnableInClassList("main-menu__nav--selected", settings);
            bool operation = model.Screen is MainMenuScreen.Connecting or MainMenuScreen.Hosting or MainMenuScreen.Updating;
            if (operation)
            {
                Texture2D source = _catalog.Wallpaper(model.Wallpaper);
                if (_blurredWallpaper == null || _blurredSource != source)
                {
                    if (_blurredWallpaper != null)
                    {
                        if (UnityEngine.Application.isPlaying) Object.Destroy(_blurredWallpaper);
                        else Object.DestroyImmediate(_blurredWallpaper);
                    }
                    _blurredSource = source;
                    _blurredWallpaper = MainMenuWallpaper.Blur(source);
                }
                Root.Q("wallpaper").style.backgroundImage = _blurredWallpaper;
            }
            Show("sidebar", !operation);
            Show("side-panel", servers || settings);
            Show("server-scroll", servers);
            Show("settings-placeholder", settings);
            Show("refresh", servers);
            Root.Q<Label>("panel-title").text = settings ? "SETTINGS" : "JOIN A SERVER";
            Root.Q<Label>("checked").text = settings ? "Esc" : model.CheckedText;
            VisualElement content = Root.Q("server-content");
            content.Clear();
            if (servers)
            {
                DirectConnect(content, model);
                Section(content, "RECENT", model.Recent, true, model);
                Section(content, "PUBLIC", model.Public, false, model);
            }
            Show("overlay", operation);
            if (operation) Operation(model);
        }

        private void DirectConnect(VisualElement parent, MainMenuPresentation model)
        {
            VisualElement box = Element(parent, "main-menu__section");
            if (model.FirstLaunch)
            {
                box.AddToClassList("main-menu__first");
                Text(box, "Paste a server address", "main-menu__bold main-menu__large");
                Text(box, "Server addresses are shared in the SS3D Discord. Copy one there and paste it here; the menu remembers it after the first join.", "main-menu__secondary main-menu__wrap");
            }
            else Text(box, "DIRECT CONNECT", "main-menu__eyebrow");
            VisualElement row = Element(box, "main-menu__direct-row");
            TextField address = Input(row, model.Address, "main-menu__address main-menu__mono");
            address.name = "direct-address";
            address.textEdition.placeholder = model.FirstLaunch ? "address or address:port" : "Paste address or address:port";
            Button connect = Action(row, "CONNECT", "main-menu__button--accent");
            connect.name = "connect";
            connect.SetEnabled(!string.IsNullOrWhiteSpace(model.Address) && !string.IsNullOrWhiteSpace(model.PlayerName));
            if (!string.IsNullOrEmpty(model.AddressError))
                Text(box, model.AddressError, "main-menu__wrap main-menu__hint").style.color = ColorCode("#d17a7a");
            if (model.FirstLaunch) Action(box, "Open the SS3D Discord", "main-menu__link");
        }

        private void Section(VisualElement parent, string title, System.Collections.Generic.IReadOnlyList<MainMenuServerPresentation> servers,
            bool recent, MainMenuPresentation model)
        {
            VisualElement section = Element(parent, "main-menu__section");
            VisualElement heading = Element(section, "main-menu__row main-menu__spread");
            Text(heading, title, "main-menu__eyebrow");
            string count = servers.Count.ToString();
            if (recent) count += " / 5";
            else if (model.PublicList == MainMenuPublicListState.FetchFailed) count = "unavailable";
            Text(heading, count, "main-menu__mono main-menu__tiny main-menu__secondary");
            if (servers.Count == 0 || (!recent && model.PublicList != MainMenuPublicListState.Populated))
            {
                VisualElement empty = Element(section, "main-menu__empty");
                bool failed = !recent && model.PublicList == MainMenuPublicListState.FetchFailed;
                string emptyTitle = "No public servers yet";
                string emptyCopy = "The project lists servers here once they go public. Until then, addresses are shared on Discord.";
                if (recent) { emptyTitle = "No recent servers"; emptyCopy = "Servers you join show up here, newest first. The last five are kept."; }
                else if (failed) { emptyTitle = "Couldn't fetch the public list"; emptyCopy = "Recent servers and direct connect still work. Refresh to try again."; }
                Text(empty, emptyTitle, "");
                Text(empty, emptyCopy, "main-menu__secondary main-menu__wrap");
                return;
            }
            VisualElement columns = Element(section, "main-menu__server-heading");
            VisualElement gutter = Element(columns, ""); gutter.style.width = 18;
            Text(columns, "Server", "main-menu__server-identity");
            Text(columns, "Status", "main-menu__status");
            Text(columns, "Players", "main-menu__players");
            Text(columns, "Ping", "main-menu__ping");
            Element(columns, "main-menu__actions");
            bool primary = false;
            foreach (MainMenuServerPresentation server in servers)
            {
                bool accent = recent && !primary && server.State == MainMenuServerState.Compatible;
                Server(section, server, recent, accent, !string.IsNullOrWhiteSpace(model.PlayerName));
                if (accent) primary = true;
            }
        }

        private void Server(VisualElement parent, MainMenuServerPresentation server, bool recent, bool primary, bool hasName)
        {
            VisualElement card = Element(parent, "main-menu__server"); card.name = $"server-{server.Id}";
            VisualElement row = Element(card, "main-menu__server-row");
            string color = server.State switch { MainMenuServerState.Compatible => "#88b36a", MainMenuServerState.NeedsUpdate => "#e6ad53", MainMenuServerState.Full => "#e6ad53", MainMenuServerState.Offline => "#b13d3d", _ => "#717c7f" };
            Element(row, "main-menu__dot").style.backgroundColor = ColorCode(color);
            VisualElement identity = Element(row, "main-menu__server-identity");
            Text(identity, server.Name, "main-menu__server-name");
            Text(identity, server.Address, "main-menu__server-address main-menu__mono");
            string status = server.State switch { MainMenuServerState.Compatible => "Online", MainMenuServerState.NeedsUpdate => $"Needs v{server.RequiredVersion} — update", MainMenuServerState.Full => "Full", MainMenuServerState.OlderVersion => $"Runs v{server.RequiredVersion}, older than yours", _ => "Offline" };
            string statusColor = "#aaaaaa";
            if (server.State == MainMenuServerState.Compatible) statusColor = "#a8cf8c";
            else if (server.State is MainMenuServerState.NeedsUpdate or MainMenuServerState.Full) statusColor = "#f0c47d";
            Text(row, status, "main-menu__status main-menu__wrap").style.color = ColorCode(statusColor);
            Label players = Text(row, server.Players, "main-menu__players main-menu__mono");
            if (server.State == MainMenuServerState.Full) players.style.color = ColorCode("#f0c47d");
            Text(row, server.Ping, "main-menu__ping main-menu__mono");
            VisualElement actions = Element(row, "main-menu__actions");
            if (recent) Action(actions, server.Expanded ? "Hide" : "Details", "main-menu__details-button");
            bool update = server.State == MainMenuServerState.NeedsUpdate;
            string actionClass = "";
            if (update) actionClass = "main-menu__button--blue";
            else if (primary) actionClass = "main-menu__button--accent";
            Button join = Action(actions, update ? "UPDATE" : "JOIN", actionClass);
            join.SetEnabled(hasName && (update || server.State == MainMenuServerState.Compatible));
            if (!string.IsNullOrEmpty(server.Refusal))
            {
                VisualElement refusal = Element(card, "main-menu__refusal");
                Text(refusal, server.Refusal, "main-menu__wrap");
                Text(refusal, server.RefusalTime, "main-menu__mono main-menu__tiny main-menu__secondary");
            }
            if (recent && server.Expanded) Details(card, server);
        }

        private static void Details(VisualElement parent, MainMenuServerPresentation server)
        {
            VisualElement details = Element(parent, "main-menu__details");
            VisualElement name = DetailRow(details, "Name");
            Input(name, server.Name, "main-menu__detail-value").maxLength = 40;
            Text(DetailRow(details, "Address"), server.Address, "main-menu__mono");
            VisualElement tokenRow = DetailRow(details, "Admin token");
            TextField token = Input(tokenRow, server.AdminToken, "main-menu__detail-value main-menu__mono");
            token.isPasswordField = true; token.textEdition.placeholder = "Optional";
            Action(tokenRow, "Show", "main-menu__details-button");
            VisualElement footer = DetailRow(details, "");
            Label hint = Text(footer, "Saved automatically. The token is sent when joining; admins join like everyone else.", "main-menu__secondary main-menu__tiny main-menu__wrap");
            hint.style.maxWidth = 420; hint.style.flexShrink = 1; hint.style.marginRight = 12;
            Action(footer, "REMOVE", "main-menu__button--danger");
        }

        private void Operation(MainMenuPresentation model)
        {
            bool updating = model.Screen == MainMenuScreen.Updating;
            bool hosting = model.Screen == MainMenuScreen.Hosting;
            string title = "CONNECTING";
            if (updating) title = "UPDATE AND RESTART";
            else if (hosting) title = "STARTING LOCAL SESSION";
            Root.Q<Label>("overlay-title").text = title;
            Root.Q<Label>("overlay-name").text = model.OperationName;
            Root.Q<Label>("overlay-address").text = model.OperationAddress;
            Show("cancel", !updating);
            VisualElement steps = Root.Q("progress-steps"); steps.Clear();
            string[] labels = { "Contacting server", "Checking version", "Loading world" };
            if (updating) labels = new string[] { "Downloading update", "Keeping maps and settings", "Restarting" };
            else if (hosting) labels = new string[] { "Starting host", "Loading world", "Opening lobby" };
            for (int i = 0; i < labels.Length; i++)
            {
                VisualElement row = Element(steps, "main-menu__step");
                Text(row, labels[i], i > model.OperationStep ? "main-menu__secondary" : "");
                string progress = "";
                if (i < model.OperationStep) progress = "OK";
                else if (i == model.OperationStep) progress = "…";
                Label mark = Text(row, progress, "main-menu__mono");
                if (i < model.OperationStep) mark.style.color = ColorCode("#a8cf8c");
            }
        }

        private void Show(string name, bool visible) => Root.Q(name).EnableInClassList("main-menu__hidden", !visible);
        private static Color ColorCode(string value) { ColorUtility.TryParseHtmlString(value, out Color color); return color; }
        private static void Classes(VisualElement element, string classes)
        { foreach (string value in classes.Split(' ')) if (value.Length > 0) element.AddToClassList(value); }
        private static VisualElement Element(VisualElement parent, string classes)
        {
            VisualElement element = new(); Classes(element, classes); parent.Add(element);
            if (element.ClassListContains("main-menu__empty")) DashedBorder(element);
            return element;
        }

        private static void DashedBorder(VisualElement element)
        {
            element.generateVisualContent += context =>
            {
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1;
                painter.strokeColor = new Color(0.847f, 0.847f, 0.847f, 0.18f);
                float width = element.layout.width - 1;
                float height = element.layout.height - 1;
                Vector2[] corners = { new(0.5f, 0.5f), new(width, 0.5f), new(width, height), new(0.5f, height) };
                painter.BeginPath();
                for (int edge = 0; edge < 4; edge++)
                {
                    Vector2 start = corners[edge];
                    Vector2 delta = corners[(edge + 1) % 4] - start;
                    float length = delta.magnitude;
                    for (float distance = 0; distance < length; distance += 10)
                    {
                        painter.MoveTo(start + delta.normalized * distance);
                        painter.LineTo(start + delta.normalized * Mathf.Min(distance + 6, length));
                    }
                }
                painter.Stroke();
            };
        }
        private static Label Text(VisualElement parent, string text, string classes)
        { Label label = new(text); label.AddToClassList("main-menu__text"); Classes(label, classes); parent.Add(label); return label; }
        private static Button Action(VisualElement parent, string text, string classes)
        { Button button = new() { text = text }; button.AddToClassList("main-menu__button"); Classes(button, classes); parent.Add(button); return button; }
        private static TextField Input(VisualElement parent, string value, string classes)
        { TextField field = new() { value = value }; field.AddToClassList("main-menu__input"); Classes(field, classes); parent.Add(field); return field; }
        private static VisualElement DetailRow(VisualElement parent, string key)
        { VisualElement row = Element(parent, "main-menu__detail-row"); Text(row, key, "main-menu__detail-key"); return row; }
    }
}
