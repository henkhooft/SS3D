using System;
using System.Collections.Generic;
using SS3D.UI.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.Lobby
{
    /// <summary>
    /// Full-screen pre-round lobby shell (Phase A: mock data + local interaction only).
    /// </summary>
    public sealed class LobbyShellView : IUiSurface
    {
        private const string TabServerInfo = "server-info";
        private const string TabJobs = "jobs";
        private const string TabChat = "chat";
        private const string TabPlayers = "players";
        private const string TabSettings = "settings";
        private const string TabServerSettings = "server-settings";

        private readonly LobbyAssetCatalog _catalog;

        private VisualElement _root;
        private VisualElement _tabBar;
        private VisualElement _tabContent;
        private VisualElement _roundDot;
        private Label _countdownLabel;
        private Label _countdownSub;
        private Button _readyButton;
        private Button _adminButton;
        private VisualElement _previewImage;

        private string _activeTab = TabServerInfo;
        private bool _ready = true;
        private bool _adminActive;
        private bool _countingDown = true;
        private int _secondsLeft = 4 * 60 + 12;
        private string _playerSearch = string.Empty;
        private string _settingsSubTab = "display";
        private string _chatChannel = "ooc";
        private string _openPlayerId;

        private readonly Dictionary<string, JobPriority> _priorities = LobbyMockData.DefaultPriorities();
        private readonly Dictionary<string, bool> _antagSelected = LobbyMockData.DefaultAntagSelections();
        private readonly HashSet<string> _collapsedDepts = new();

        public event Action CharacterCreatorRequested;

        public LobbyShellView(LobbyAssetCatalog catalog)
        {
            _catalog = catalog;
        }

        public bool IsVisible => _root != null && _root.resolvedStyle.display != DisplayStyle.None;

        public void Attach(VisualElement layerRoot)
        {
            _root = new VisualElement { name = "lobby-shell-surface" };
            _root.AddToClassList("lobby-shell");
            _root.pickingMode = PickingMode.Position;

            if (_catalog != null && _catalog.LobbyStyle != null)
            {
                _root.styleSheets.Add(_catalog.LobbyStyle);
            }

            VisualElement window = new() { name = "lobby-shell-window" };
            window.AddToClassList("lobby-shell__window");
            window.pickingMode = PickingMode.Position;
            _root.Add(window);

            BuildChrome(window);
            layerRoot.Add(_root);
            RefreshTabs();
            ShowTab(_activeTab);
            RefreshSidebar();
            SetVisible(true);
        }

        public void Detach()
        {
            CharacterCreatorRequested = null;
            _tabBar = null;
            _tabContent = null;
            _roundDot = null;
            _countdownLabel = null;
            _countdownSub = null;
            _readyButton = null;
            _adminButton = null;
            _previewImage = null;
            _root?.RemoveFromHierarchy();
            _root = null;
        }

        public void SetVisible(bool visible)
        {
            if (_root == null)
            {
                return;
            }

            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _root.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
        }

        private void BuildChrome(VisualElement window)
        {
            window.Clear();

            VisualElement header = new();
            header.AddToClassList("lobby-shell__header");
            header.pickingMode = PickingMode.Ignore;

            VisualElement headerLeft = new();
            headerLeft.AddToClassList("lobby-shell__header-left");
            headerLeft.pickingMode = PickingMode.Ignore;
            headerLeft.Add(MakeLabel(LobbyMockData.ServerName, "font-body", "text-primary"));
            headerLeft.Add(MakeSep());
            headerLeft.Add(MakeLabel(LobbyMockData.MapName, "font-body", "text-secondary"));
            headerLeft.Add(MakeSep());
            headerLeft.Add(MakeLabel(LobbyMockData.ModeLabel, "font-body", "text-secondary"));
            header.Add(headerLeft);

            VisualElement headerRight = new();
            headerRight.AddToClassList("lobby-shell__header-right");
            headerRight.pickingMode = PickingMode.Ignore;
            int readyCount = 0;
            foreach (LobbyMockData.PlayerRow p in LobbyMockData.Players)
            {
                if (p.Ready)
                {
                    readyCount++;
                }
            }

            // Header is session strip only (design): players + round — not build/uptime.
            headerRight.Add(MakeMeta($"{readyCount}/{LobbyMockData.Players.Length} players"));
            headerRight.Add(MakeSep());
            headerRight.Add(MakeMeta($"Round {LobbyMockData.RoundNumber}"));
            header.Add(headerRight);
            window.Add(header);

            VisualElement body = new();
            body.AddToClassList("lobby-shell__body");

            VisualElement main = new();
            main.AddToClassList("lobby-shell__main");

            _tabBar = new VisualElement();
            _tabBar.AddToClassList("lobby-shell__tabs");
            main.Add(_tabBar);

            _tabContent = new VisualElement();
            _tabContent.AddToClassList("lobby-shell__tab-content");
            main.Add(_tabContent);

            body.Add(main);
            body.Add(BuildSidebar());
            window.Add(body);
        }

        private VisualElement BuildSidebar()
        {
            VisualElement sidebar = new();
            sidebar.AddToClassList("lobby-shell__sidebar");

            VisualElement roundPanel = new();
            roundPanel.AddToClassList("lobby-shell__panel");

            VisualElement roundHeader = new();
            roundHeader.AddToClassList("lobby-shell__panel-header");
            _roundDot = new VisualElement();
            _roundDot.AddToClassList("lobby-shell__panel-dot");
            roundHeader.Add(_roundDot);
            Label roundTitle = MakeLabel("Round State", "font-titling");
            roundTitle.AddToClassList("lobby-shell__panel-title");
            roundHeader.Add(roundTitle);
            roundPanel.Add(roundHeader);

            VisualElement accent = new();
            accent.AddToClassList("lobby-shell__panel-accent");
            accent.pickingMode = PickingMode.Ignore;
            roundPanel.Add(accent);

            VisualElement roundBody = new();
            roundBody.AddToClassList("lobby-shell__panel-body");
            _countdownLabel = new Label();
            _countdownLabel.AddToClassList("lobby-shell__countdown");
            _countdownLabel.AddToClassList("font-titling");
            roundBody.Add(_countdownLabel);
            _countdownSub = new Label();
            _countdownSub.AddToClassList("lobby-shell__countdown-sub");
            _countdownSub.AddToClassList("font-body");
            roundBody.Add(_countdownSub);
            roundPanel.Add(roundBody);
            sidebar.Add(roundPanel);

            VisualElement preview = new();
            preview.AddToClassList("lobby-shell__preview");
            preview.pickingMode = PickingMode.Position;
            preview.RegisterCallback<ClickEvent>(_ => CharacterCreatorRequested?.Invoke());

            _previewImage = new VisualElement();
            _previewImage.AddToClassList("lobby-shell__preview-image");
            _previewImage.pickingMode = PickingMode.Ignore;
            if (_catalog != null && _catalog.PreviewPlaceholder != null)
            {
                _previewImage.style.backgroundImage = new StyleBackground(_catalog.PreviewPlaceholder);
            }

            preview.Add(_previewImage);

            Label previewHint = new("Click to open character creator");
            previewHint.AddToClassList("lobby-shell__preview-hint");
            previewHint.AddToClassList("font-body");
            previewHint.pickingMode = PickingMode.Ignore;
            preview.Add(previewHint);
            sidebar.Add(preview);

            VisualElement actions = new();
            actions.AddToClassList("lobby-shell__actions");

            _readyButton = new Button(ToggleReady) { text = "READY" };
            _readyButton.AddToClassList("lobby-shell__btn");
            _readyButton.AddToClassList("lobby-shell__btn--ready");
            _readyButton.AddToClassList("font-arcade");
            actions.Add(_readyButton);

            VisualElement row = new();
            row.AddToClassList("lobby-shell__actions-row");

            Button observe = new(() => Debug.Log("[Lobby] Observe clicked (stub until observer wiring)."))
            {
                text = "OBSERVE",
            };
            observe.AddToClassList("lobby-shell__btn");
            observe.AddToClassList("lobby-shell__btn--observe");
            observe.AddToClassList("font-arcade");
            row.Add(observe);

            _adminButton = new Button(ToggleAdmin) { text = "ADMIN" };
            _adminButton.AddToClassList("lobby-shell__btn");
            _adminButton.AddToClassList("lobby-shell__btn--admin");
            _adminButton.AddToClassList("font-arcade");
            row.Add(_adminButton);

            actions.Add(row);
            sidebar.Add(actions);
            return sidebar;
        }

        private void RefreshSidebar()
        {
            if (_countdownLabel == null)
            {
                return;
            }

            if (_countingDown)
            {
                int mm = _secondsLeft / 60;
                int ss = _secondsLeft % 60;
                _countdownLabel.text = $"{mm:00}:{ss:00}";
                _countdownSub.text = "Starting In";
                _roundDot?.EnableInClassList("lobby-shell__panel-dot--live", true);
            }
            else
            {
                _countdownLabel.text = "—";
                _countdownSub.text = "Waiting For Players";
                _roundDot?.EnableInClassList("lobby-shell__panel-dot--live", false);
            }

            if (_readyButton != null)
            {
                _readyButton.text = _ready ? "✓  READY" : "NOT READY";
                _readyButton.EnableInClassList("lobby-shell__btn--ready-on", _ready);
            }

            if (_adminButton != null)
            {
                _adminButton.text = _adminActive ? "ADMIN ✓" : "ADMIN";
                _adminButton.EnableInClassList("lobby-shell__btn--admin-on", _adminActive);
            }
        }

        private void RefreshTabs()
        {
            if (_tabBar == null)
            {
                return;
            }

            _tabBar.Clear();
            AddTab(TabServerInfo, "Server Info", false);
            AddTab(TabJobs, "Jobs", false);
            AddTab(TabChat, "Chat", false);
            AddTab(TabPlayers, "Players", false);
            AddTab(TabSettings, "Settings", false);
            if (_adminActive)
            {
                AddTab(TabServerSettings, "Server Settings", true);
            }
        }

        private void AddTab(string key, string label, bool admin)
        {
            Button tab = new(() => SelectTab(key)) { text = label.ToUpperInvariant() };
            tab.AddToClassList("lobby-shell__tab");
            tab.AddToClassList("font-titling");
            tab.style.flexGrow = 1;
            tab.style.flexBasis = 0;
            if (admin)
            {
                tab.AddToClassList("lobby-shell__tab--admin");
            }

            tab.EnableInClassList("lobby-shell__tab--active", key == _activeTab);
            _tabBar.Add(tab);
        }

        private void SelectTab(string key)
        {
            _activeTab = key;
            RefreshTabs();
            ShowTab(key);
        }

        private void ShowTab(string key)
        {
            if (_tabContent == null)
            {
                return;
            }

            _tabContent.Clear();
            switch (key)
            {
                case TabServerInfo:
                {
                    BuildServerInfoTab(_tabContent);
                    break;
                }

                case TabJobs:
                {
                    BuildJobsTab(_tabContent);
                    break;
                }

                case TabChat:
                {
                    BuildChatTab(_tabContent);
                    break;
                }

                case TabPlayers:
                {
                    BuildPlayersTab(_tabContent);
                    break;
                }

                case TabSettings:
                {
                    BuildSettingsTab(_tabContent);
                    break;
                }

                case TabServerSettings:
                {
                    BuildServerSettingsTab(_tabContent);
                    break;
                }

                default:
                {
                    _tabContent.Add(MakePlaceholder($"Unknown tab: {key}"));
                    break;
                }
            }
        }

        private void BuildServerInfoTab(VisualElement parent)
        {
            VisualElement hero = new();
            hero.AddToClassList("lobby-shell__hero");
            hero.pickingMode = PickingMode.Ignore;
            Label mark = new();
            mark.AddToClassList("lobby-shell__hero-mark");
            mark.AddToClassList("font-titling");
            mark.text = "SS";
            Label markAccent = new("3D");
            markAccent.AddToClassList("lobby-shell__hero-mark");
            markAccent.AddToClassList("lobby-shell__hero-mark-accent");
            markAccent.AddToClassList("font-titling");
            VisualElement markRow = new() { style = { flexDirection = FlexDirection.Row } };
            markRow.Add(mark);
            markRow.Add(markAccent);
            hero.Add(markRow);
            parent.Add(hero);

            VisualElement titleRow = new();
            titleRow.AddToClassList("lobby-shell__server-title-row");
            titleRow.pickingMode = PickingMode.Ignore;

            Label title = new(LobbyMockData.ServerName.ToUpperInvariant());
            title.AddToClassList("lobby-shell__server-title");
            title.AddToClassList("font-titling");
            titleRow.Add(title);

            Label meta = new($"build {LobbyMockData.BuildVersion} · up {LobbyMockData.UptimeLabel}");
            meta.AddToClassList("lobby-shell__server-meta");
            meta.AddToClassList("font-terminal");
            titleRow.Add(meta);
            parent.Add(titleRow);

            VisualElement motdBlock = new();
            motdBlock.AddToClassList("lobby-shell__motd");
            motdBlock.Add(SectionTitle("Message of the Day"));
            Label motd = new(LobbyMockData.Motd);
            motd.AddToClassList("lobby-shell__inset");
            motd.AddToClassList("font-body");
            motdBlock.Add(motd);
            parent.Add(motdBlock);

            VisualElement bottom = new();
            bottom.AddToClassList("lobby-shell__bottom-row");

            VisualElement mapModeBlock = new();
            mapModeBlock.AddToClassList("lobby-shell__map-mode-block");
            mapModeBlock.Add(SectionTitle("Map & Mode"));

            VisualElement mapModeRow = new();
            mapModeRow.AddToClassList("lobby-shell__map-mode-row");
            mapModeRow.Add(BuildSelectCard(
                LobbyMockData.MapName,
                "Map",
                LobbyMockData.MapName.Length > 0 ? LobbyMockData.MapName[0].ToString().ToUpperInvariant() : "?",
                mapSwatch: true));
            mapModeRow.Add(BuildSelectCard(
                LobbyMockData.ModeLabel,
                "Mode",
                LobbyMockData.ModeLabel.Length > 0 ? LobbyMockData.ModeLabel[0].ToString().ToUpperInvariant() : "?",
                mapSwatch: false));
            mapModeBlock.Add(mapModeRow);
            bottom.Add(mapModeBlock);

            VisualElement logCol = new();
            logCol.AddToClassList("lobby-shell__changelog-col");
            logCol.Add(SectionTitle("Change Log"));
            VisualElement logBox = new();
            logBox.AddToClassList("lobby-shell__inset");
            foreach (LobbyMockData.ChangelogEntry entry in LobbyMockData.Changelog)
            {
                VisualElement row = new();
                row.AddToClassList("lobby-shell__changelog-row");
                Label ver = new(entry.Version);
                ver.AddToClassList("lobby-shell__changelog-ver");
                ver.AddToClassList("font-terminal");
                Label text = new($"— {entry.Text}");
                text.AddToClassList("lobby-shell__changelog-text");
                text.AddToClassList("font-body");
                row.Add(ver);
                row.Add(text);
                logBox.Add(row);
            }

            logCol.Add(logBox);
            bottom.Add(logCol);
            parent.Add(bottom);
        }

        private static VisualElement BuildSelectCard(string name, string kind, string initial, bool mapSwatch)
        {
            VisualElement card = new();
            card.AddToClassList("lobby-shell__select-card");

            VisualElement swatch = new();
            swatch.AddToClassList("lobby-shell__select-swatch");
            if (mapSwatch)
            {
                swatch.AddToClassList("lobby-shell__select-swatch--map");
            }

            Label swatchLabel = new(initial);
            swatchLabel.AddToClassList("lobby-shell__select-swatch-label");
            swatchLabel.AddToClassList("font-titling");
            swatch.Add(swatchLabel);
            card.Add(swatch);

            VisualElement textCol = new();
            textCol.AddToClassList("lobby-shell__select-text");
            Label nameLabel = new(name);
            nameLabel.AddToClassList("lobby-shell__select-name");
            nameLabel.AddToClassList("font-titling");
            textCol.Add(nameLabel);
            Label kindLabel = new(kind);
            kindLabel.AddToClassList("lobby-shell__select-kind");
            kindLabel.AddToClassList("font-body");
            textCol.Add(kindLabel);
            card.Add(textCol);
            return card;
        }

        private void BuildJobsTab(VisualElement parent)
        {
            VisualElement split = new();
            split.AddToClassList("lobby-shell__jobs-split");
            split.style.flexGrow = 1;

            ScrollView list = new();
            list.AddToClassList("lobby-shell__jobs-list");
            list.style.flexGrow = 1;

            foreach (LobbyMockData.Department dept in LobbyMockData.Departments)
            {
                bool expanded = !_collapsedDepts.Contains(dept.Key);
                int openSum = 0;
                int slotSum = 0;
                foreach (LobbyMockData.JobRow job in dept.Jobs)
                {
                    openSum += job.Open;
                    slotSum += job.Slots;
                }

                VisualElement header = new();
                header.AddToClassList("lobby-shell__dept-header");
                header.pickingMode = PickingMode.Position;
                string deptKey = dept.Key;
                header.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!_collapsedDepts.Add(deptKey))
                    {
                        _collapsedDepts.Remove(deptKey);
                    }

                    ShowTab(TabJobs);
                });

                Label name = new(dept.Name);
                name.AddToClassList("lobby-shell__dept-name");
                name.AddToClassList("font-titling");
                name.pickingMode = PickingMode.Ignore;
                header.Add(name);

                Label slots = new($"{openSum}/{slotSum}");
                slots.AddToClassList("lobby-shell__dept-slots");
                slots.AddToClassList("font-terminal");
                slots.pickingMode = PickingMode.Ignore;
                header.Add(slots);

                Label chevron = new(expanded ? "▾" : "▸");
                chevron.AddToClassList("lobby-shell__dept-chevron");
                chevron.pickingMode = PickingMode.Ignore;
                header.Add(chevron);
                list.Add(header);

                if (!expanded)
                {
                    continue;
                }

                foreach (LobbyMockData.JobRow job in dept.Jobs)
                {
                    list.Add(BuildJobRow(job));
                }
            }

            split.Add(list);
            split.Add(BuildAntagPanel());
            parent.Add(split);
        }

        private VisualElement BuildJobRow(LobbyMockData.JobRow job)
        {
            VisualElement row = new();
            row.AddToClassList("lobby-shell__job-row");
            if (job.Locked)
            {
                row.AddToClassList("lobby-shell__job-row--locked");
            }

            VisualElement icon = new();
            icon.AddToClassList("lobby-shell__job-icon");
            if (_catalog != null && _catalog.TryGetJobIcon(job.IconId, out Sprite sprite))
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
            }

            row.Add(icon);

            VisualElement info = new();
            info.AddToClassList("lobby-shell__job-info");
            Label name = new(job.Name);
            name.AddToClassList("lobby-shell__job-name");
            name.AddToClassList("font-body");
            info.Add(name);

            string meta = job.Locked && !string.IsNullOrEmpty(job.LockReason)
                ? job.LockReason
                : $"{job.Open}/{job.Slots} open · {job.Interested} interested";
            Label metaLabel = new(meta);
            metaLabel.AddToClassList("lobby-shell__job-meta");
            metaLabel.AddToClassList("font-body");
            info.Add(metaLabel);
            row.Add(info);

            if (!job.Locked)
            {
                row.Add(BuildPriorityChips(job.Id));
            }

            return row;
        }

        private VisualElement BuildPriorityChips(string jobId)
        {
            VisualElement chips = new();
            chips.AddToClassList("lobby-shell__priority");
            JobPriority current = _priorities.TryGetValue(jobId, out JobPriority p) ? p : JobPriority.Never;

            foreach (JobPriority level in JobPriorityUtil.CycleOrder)
            {
                JobPriority captured = level;
                Button chip = new(() =>
                {
                    _priorities[jobId] = captured;
                    ShowTab(TabJobs);
                })
                {
                    text = JobPriorityUtil.ShortLabel(level),
                    tooltip = JobPriorityUtil.FullLabel(level),
                };
                chip.AddToClassList("lobby-shell__priority-chip");
                chip.AddToClassList("font-arcade");
                if (level == current)
                {
                    chip.AddToClassList(PriorityClass(level));
                }
                else
                {
                    chip.AddToClassList("lobby-shell__priority-chip--idle");
                }

                chips.Add(chip);
            }

            return chips;
        }

        private static string PriorityClass(JobPriority priority) => priority switch
        {
            JobPriority.Never => "lobby-shell__priority-chip--never",
            JobPriority.Low => "lobby-shell__priority-chip--low",
            JobPriority.Medium => "lobby-shell__priority-chip--medium",
            JobPriority.High => "lobby-shell__priority-chip--high",
            _ => "lobby-shell__priority-chip--idle",
        };

        private VisualElement BuildAntagPanel()
        {
            VisualElement panel = new();
            panel.AddToClassList("lobby-shell__antag");

            VisualElement titleRow = new()
            {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 4 },
            };
            Label title = SectionTitle("Antagonist");
            title.style.marginBottom = 0;
            titleRow.Add(title);

            bool allOn = true;
            foreach (LobbyMockData.AntagRole role in LobbyMockData.AntagRoles)
            {
                if (!_antagSelected.TryGetValue(role.Key, out bool on) || !on)
                {
                    allOn = false;
                    break;
                }
            }

            Label toggleAll = new(allOn ? "Clear All" : "Select All");
            toggleAll.AddToClassList("lobby-shell__link");
            toggleAll.AddToClassList("font-body");
            toggleAll.pickingMode = PickingMode.Position;
            toggleAll.RegisterCallback<ClickEvent>(_ =>
            {
                bool next = !allOn;
                foreach (LobbyMockData.AntagRole role in LobbyMockData.AntagRoles)
                {
                    _antagSelected[role.Key] = next;
                }

                ShowTab(TabJobs);
            });
            titleRow.Add(toggleAll);
            panel.Add(titleRow);

            Label hint = new("Affects eligibility only — checking a role does not guarantee it.");
            hint.AddToClassList("lobby-shell__antag-hint");
            hint.AddToClassList("font-body");
            panel.Add(hint);

            foreach (LobbyMockData.AntagRole role in LobbyMockData.AntagRoles)
            {
                bool on = _antagSelected.TryGetValue(role.Key, out bool selected) && selected;
                string key = role.Key;

                VisualElement row = new();
                row.AddToClassList("lobby-shell__antag-row");
                row.pickingMode = PickingMode.Position;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    _antagSelected[key] = !on;
                    ShowTab(TabJobs);
                });

                VisualElement box = new();
                box.AddToClassList("lobby-shell__check");
                box.EnableInClassList("lobby-shell__check--on", on);
                box.pickingMode = PickingMode.Ignore;
                if (on)
                {
                    Label mark = new("✓");
                    mark.AddToClassList("lobby-shell__check-mark");
                    mark.pickingMode = PickingMode.Ignore;
                    box.Add(mark);
                }

                row.Add(box);

                Label name = new(role.Name);
                name.AddToClassList("lobby-shell__antag-name");
                name.AddToClassList("font-body");
                name.pickingMode = PickingMode.Ignore;
                row.Add(name);
                panel.Add(row);
            }

            return panel;
        }

        private void BuildChatTab(VisualElement parent)
        {
            VisualElement channels = new();
            channels.AddToClassList("lobby-shell__channel-row");
            AddChannel(channels, "ooc", "OOC");
            AddChannel(channels, "looc", "LOOC");
            AddChannel(channels, "admin", "Admin");
            parent.Add(channels);

            ScrollView log = new();
            log.AddToClassList("lobby-shell__chat-log");
            log.Add(ChatLine("[OOC] Kowalski: Anyone else seeing atmos weirdness on arrivals?"));
            log.Add(ChatLine("[OOC] Reyes: That's just Cerestation being Cerestation."));
            log.Add(ChatLine("[LOOC] Voss: Ready when you are."));
            parent.Add(log);

            VisualElement composer = new();
            composer.AddToClassList("lobby-shell__chat-composer");
            TextField input = new() { value = string.Empty };
            input.AddToClassList("lobby-shell__chat-input");
            input.AddToClassList("font-body");
            composer.Add(input);
            Button send = new(() => Debug.Log($"[Lobby] Chat send ({_chatChannel}): {input.value}"))
            {
                text = "Send",
            };
            send.AddToClassList("lobby-shell__chat-send");
            send.AddToClassList("font-body");
            composer.Add(send);
            parent.Add(composer);
        }

        private void AddChannel(VisualElement parent, string key, string label)
        {
            Button btn = new(() =>
            {
                _chatChannel = key;
                ShowTab(TabChat);
            })
            {
                text = label.ToUpperInvariant(),
            };
            btn.AddToClassList("lobby-shell__channel");
            btn.AddToClassList("font-titling");
            btn.EnableInClassList("lobby-shell__channel--active", _chatChannel == key);
            parent.Add(btn);
        }

        private static Label ChatLine(string text)
        {
            Label line = new(text);
            line.AddToClassList("lobby-shell__chat-line");
            line.AddToClassList("font-terminal");
            return line;
        }

        private void BuildPlayersTab(VisualElement parent)
        {
            TextField search = new() { value = _playerSearch };
            search.AddToClassList("lobby-shell__search");
            search.AddToClassList("font-body");
            search.RegisterValueChangedCallback(evt =>
            {
                _playerSearch = evt.newValue ?? string.Empty;
                ShowTab(TabPlayers);
            });
            parent.Add(search);

            ScrollView list = new();
            list.style.flexGrow = 1;
            string filter = _playerSearch.Trim().ToLowerInvariant();
            int shown = 0;
            foreach (LobbyMockData.PlayerRow player in LobbyMockData.Players)
            {
                if (!string.IsNullOrEmpty(filter) && !player.Name.ToLowerInvariant().Contains(filter))
                {
                    continue;
                }

                shown++;
                VisualElement row = new();
                row.AddToClassList("lobby-shell__player-row");
                row.pickingMode = PickingMode.Position;
                string id = player.Id;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    _openPlayerId = _openPlayerId == id ? null : id;
                    ShowTab(TabPlayers);
                });

                VisualElement dot = new();
                dot.AddToClassList("lobby-shell__player-dot");
                dot.EnableInClassList("lobby-shell__player-dot--ready", player.Ready);
                dot.pickingMode = PickingMode.Ignore;
                row.Add(dot);

                string nameText = string.IsNullOrEmpty(player.Rank) ? player.Name : $"{player.Name} ({player.Rank})";
                Label name = new(nameText);
                name.AddToClassList("lobby-shell__player-name");
                name.AddToClassList("font-body");
                name.pickingMode = PickingMode.Ignore;
                row.Add(name);

                Label status = new(player.Ready ? "Ready" : "Not Ready");
                status.AddToClassList("lobby-shell__player-status");
                status.AddToClassList("font-body");
                status.pickingMode = PickingMode.Ignore;
                row.Add(status);
                list.Add(row);

                if (_openPlayerId == player.Id)
                {
                    VisualElement detail = new();
                    detail.AddToClassList("lobby-shell__inset");
                    detail.style.marginBottom = 6;
                    detail.Add(MakeLabel($"Ping: {player.Ping}", "font-terminal", "text-secondary"));
                    detail.Add(MakeLabel($"Joined: {player.JoinTime}", "font-terminal", "text-secondary"));
                    detail.Add(MakeLabel($"Account age: {player.AccountAge}", "font-terminal", "text-secondary"));
                    if (_adminActive)
                    {
                        detail.Add(MakeLabel("Kick / Ban / Mute (stub)", "font-body", "text-tertiary"));
                    }

                    list.Add(detail);
                }
            }

            if (shown == 0)
            {
                list.Add(MakePlaceholder($"No players match \"{_playerSearch}\"."));
            }

            parent.Add(list);
        }

        private void BuildSettingsTab(VisualElement parent)
        {
            VisualElement subTabs = new();
            subTabs.AddToClassList("lobby-shell__settings-tabs");
            AddSettingsSubTab(subTabs, "display", "Display");
            AddSettingsSubTab(subTabs, "sound", "Sound");
            AddSettingsSubTab(subTabs, "keyboard", "Keyboard");
            parent.Add(subTabs);

            switch (_settingsSubTab)
            {
                case "sound":
                {
                    parent.Add(SettingRow("Master Volume", "80%"));
                    parent.Add(SettingRow("Music Volume", "60%"));
                    parent.Add(SettingRow("SFX Volume", "80%"));
                    parent.Add(SettingRow("Mute All Audio", "Off"));
                    break;
                }

                case "keyboard":
                {
                    parent.Add(SettingRow("Move", "WASD"));
                    parent.Add(SettingRow("Interact", "E"));
                    parent.Add(SettingRow("Ready Toggle", "F3"));
                    parent.Add(MakePlaceholder("Keybind rebinding wires in a later pass."));
                    break;
                }

                default:
                {
                    parent.Add(SettingRow("Resolution", "1920×1080"));
                    parent.Add(SettingRow("Fullscreen", "On"));
                    parent.Add(SettingRow("UI Scale", "100%"));
                    parent.Add(SettingRow("Brightness", "60%"));
                    break;
                }
            }
        }

        private void AddSettingsSubTab(VisualElement parent, string key, string label)
        {
            Button btn = new(() =>
            {
                _settingsSubTab = key;
                ShowTab(TabSettings);
            })
            {
                text = label.ToUpperInvariant(),
            };
            btn.AddToClassList("lobby-shell__tab");
            btn.AddToClassList("font-titling");
            btn.EnableInClassList("lobby-shell__tab--active", _settingsSubTab == key);
            parent.Add(btn);
        }

        private static VisualElement SettingRow(string label, string value)
        {
            VisualElement row = new();
            row.AddToClassList("lobby-shell__setting-row");
            Label left = new(label);
            left.AddToClassList("lobby-shell__setting-label");
            left.AddToClassList("font-body");
            Label right = new(value);
            right.AddToClassList("lobby-shell__setting-value");
            right.AddToClassList("font-terminal");
            row.Add(left);
            row.Add(right);
            return row;
        }

        private void BuildServerSettingsTab(VisualElement parent)
        {
            parent.Add(SectionTitle("Round Controls"));
            VisualElement controls = new();
            controls.AddToClassList("lobby-shell__admin-controls");
            controls.Add(AdminButton("Start Round", () =>
            {
                _countingDown = true;
                _secondsLeft = 5 * 60;
                RefreshSidebar();
            }));
            controls.Add(AdminButton("Force Start", () =>
            {
                _countingDown = false;
                _secondsLeft = 0;
                RefreshSidebar();
            }, "lobby-shell__admin-btn--danger"));
            controls.Add(AdminButton("Extend Timer", () =>
            {
                _secondsLeft += 60;
                RefreshSidebar();
            }, "lobby-shell__admin-btn--amber"));
            controls.Add(AdminButton("Cancel Countdown", () =>
            {
                _countingDown = false;
                RefreshSidebar();
            }));
            parent.Add(controls);

            parent.Add(SectionTitle("Map & Mode"));
            parent.Add(SettingRow("Map", LobbyMockData.MapName));
            parent.Add(SettingRow("Game Mode", LobbyMockData.ModeLabel));
            parent.Add(SettingRow("Player Cap", "32"));
            parent.Add(SettingRow("Round Length (min)", "90"));

            parent.Add(SectionTitle("Round Rules"));
            parent.Add(MakePlaceholder("Round-rule toggles are visual stubs until round-config wiring."));
        }

        private static Button AdminButton(string label, Action onClick, string extraClass = null)
        {
            Button btn = new(onClick) { text = label.ToUpperInvariant() };
            btn.AddToClassList("lobby-shell__admin-btn");
            btn.AddToClassList("font-titling");
            if (!string.IsNullOrEmpty(extraClass))
            {
                btn.AddToClassList(extraClass);
            }

            return btn;
        }

        private void ToggleReady()
        {
            _ready = !_ready;
            RefreshSidebar();
        }

        private void ToggleAdmin()
        {
            _adminActive = !_adminActive;
            if (!_adminActive && _activeTab == TabServerSettings)
            {
                _activeTab = TabServerInfo;
            }
            else if (_adminActive)
            {
                _activeTab = TabServerSettings;
            }

            RefreshTabs();
            ShowTab(_activeTab);
            RefreshSidebar();
        }

        private static Label SectionTitle(string text)
        {
            Label label = new(text);
            label.AddToClassList("lobby-shell__section-title");
            label.AddToClassList("font-titling");
            return label;
        }

        private static Label MakePlaceholder(string text)
        {
            Label label = new(text);
            label.AddToClassList("lobby-shell__placeholder");
            label.AddToClassList("font-body");
            return label;
        }

        private static Label MakeLabel(string text, string fontClass, string colorClass = null)
        {
            Label label = new(text);
            label.AddToClassList(fontClass);
            if (!string.IsNullOrEmpty(colorClass))
            {
                label.AddToClassList(colorClass);
            }

            return label;
        }

        private static Label MakeSep()
        {
            Label sep = new("•");
            sep.AddToClassList("lobby-shell__header-sep");
            sep.AddToClassList("font-body");
            return sep;
        }

        private static Label MakeMeta(string text)
        {
            Label label = new(text);
            label.AddToClassList("lobby-shell__header-meta");
            label.AddToClassList("font-body");
            return label;
        }
    }
}
