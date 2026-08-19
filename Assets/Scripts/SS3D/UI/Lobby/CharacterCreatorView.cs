using System;
using System.Collections.Generic;
using System.Linq;
using SS3D.UI.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.Lobby
{
    /// <summary>
    /// Full-screen Character Creator (Phase B: guided-steps visual + local mock state).
    /// Opened from the lobby preview; Return to Lobby restores the shell.
    /// </summary>
    public sealed class CharacterCreatorView : IUiSurface
    {
        private readonly LobbyAssetCatalog _catalog;

        private VisualElement _root;
        private VisualElement _bodyHost;
        private VisualElement _previewImage;
        private Label _angleLabel;
        private Label _loadoutNameLabel;
        private VisualElement _loadoutThumb;
        private VisualElement _loadoutMenu;
        private Texture _livePreviewTexture;

        private int _stepIndex;
        private string _editName = LobbyMockData.CharacterName;
        private string _species = CharacterCreatorMockData.SpeciesOptions[0];
        private int _uniformIndex;
        private int _hairColorIndex;
        private int _eyeColorIndex;
        private int _angleIndex;
        private string _styleTab = "hair";
        // Catalog-backed style indices (0 = none).
        private int _hairStyleIndex;
        private int _beardStyleIndex;
        private string _selectedBrows = "thin";
        private string _loadoutId = CharacterCreatorMockData.Loadouts[0].Id;
        private bool _loadoutOpen;

        private readonly Dictionary<string, float> _sliders = new();

        public event Action ReturnToLobbyRequested;

        public event Action<CharacterCreatorDraft> CharacterSaved;

        public event Action<int> PreviewAngleChanged;

        /// <summary>Fired when a body morph slider changes (live preview).</summary>
        public event Action<IReadOnlyDictionary<string, float>> BodyMorphsChanged;

        /// <summary>
        /// Fired when hair style, beard style, or hair colour selection changes.
        /// Args: hairStyleIndex, beardStyleIndex, hairColorIndex.
        /// </summary>
        public event Action<int, int, int> StyleChanged;

        public CharacterCreatorView(LobbyAssetCatalog catalog)
        {
            _catalog = catalog;
            foreach (CharacterCreatorMockData.SliderDef slider in CharacterCreatorMockData.BodySliders)
            {
                _sliders[slider.Key] = slider.DefaultValue;
            }
        }

        public void Attach(VisualElement layerRoot)
        {
            _root = new VisualElement { name = "character-creator-surface" };
            _root.AddToClassList("char-creator");
            _root.pickingMode = PickingMode.Position;

            if (_catalog != null && _catalog.LobbyStyle != null)
            {
                _root.styleSheets.Add(_catalog.LobbyStyle);
            }

            VisualElement window = new() { name = "character-creator-window" };
            window.AddToClassList("char-creator__window");
            window.pickingMode = PickingMode.Position;
            _root.Add(window);

            BuildChrome(window);
            layerRoot.Add(_root);
            Rebuild();
            SetVisible(false);
        }

        public void Detach()
        {
            ReturnToLobbyRequested = null;
            CharacterSaved = null;
            PreviewAngleChanged = null;
            BodyMorphsChanged = null;
            StyleChanged = null;
            if (_root != null && _root.parent != null)
            {
                _root.parent.Remove(_root);
            }

            _root = null;
            _bodyHost = null;
            _previewImage = null;
            _angleLabel = null;
            _loadoutNameLabel = null;
            _loadoutThumb = null;
            _loadoutMenu = null;
            _livePreviewTexture = null;
        }

        public void SetVisible(bool visible)
        {
            if (_root == null)
            {
                return;
            }

            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                _loadoutOpen = false;
                Rebuild();
            }
        }

        public void Open()
        {
            SetVisible(true);
            PreviewAngleChanged?.Invoke(_angleIndex);
            NotifyBodyMorphsChanged();
            NotifyStyleChanged();
        }

        public void ApplyDraft(CharacterCreatorDraft draft)
        {
            if (draft == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(draft.Name))
            {
                _editName = draft.Name;
            }

            foreach (KeyValuePair<string, float> pair in draft.BodyMorphs)
            {
                _sliders[pair.Key] = pair.Value;
            }

            _hairStyleIndex = draft.HairStyleIndex;
            _beardStyleIndex = draft.BeardStyleIndex;
            _hairColorIndex = draft.HairColorIndex;

            if (_root != null && _root.style.display == DisplayStyle.Flex)
            {
                Rebuild();
            }

            NotifyBodyMorphsChanged();
            NotifyStyleChanged();
        }

        /// <summary>Current body slider values (0–1) for live booth apply.</summary>
        public IReadOnlyDictionary<string, float> CurrentBodyMorphs => _sliders;

        public void NotifyBodyMorphsChanged()
        {
            BodyMorphsChanged?.Invoke(_sliders);
        }

        public void NotifyStyleChanged()
        {
            StyleChanged?.Invoke(_hairStyleIndex, _beardStyleIndex, _hairColorIndex);
        }

        public void SetPreviewTexture(Texture texture)
        {
            _livePreviewTexture = texture;
            RefreshPreview();
        }

        private void BuildChrome(VisualElement window)
        {
            VisualElement top = new();
            top.AddToClassList("char-creator__top");

            Label title = new("CHARACTER CREATOR");
            title.AddToClassList("char-creator__title");
            title.AddToClassList("font-titling");
            top.Add(title);

            VisualElement topRight = new();
            topRight.AddToClassList("char-creator__top-right");

            VisualElement loadoutWrap = new();
            loadoutWrap.AddToClassList("char-creator__loadout-wrap");

            VisualElement loadoutBtn = new();
            loadoutBtn.AddToClassList("char-creator__loadout-btn");
            loadoutBtn.pickingMode = PickingMode.Position;
            loadoutBtn.RegisterCallback<ClickEvent>(_ =>
            {
                _loadoutOpen = !_loadoutOpen;
                Rebuild();
            });

            _loadoutThumb = new VisualElement();
            _loadoutThumb.AddToClassList("char-creator__loadout-thumb");
            _loadoutThumb.pickingMode = PickingMode.Ignore;
            loadoutBtn.Add(_loadoutThumb);

            _loadoutNameLabel = new();
            _loadoutNameLabel.AddToClassList("char-creator__loadout-name");
            _loadoutNameLabel.AddToClassList("font-body");
            _loadoutNameLabel.pickingMode = PickingMode.Ignore;
            loadoutBtn.Add(_loadoutNameLabel);

            VisualElement chevron = new();
            chevron.AddToClassList("char-creator__loadout-chevron");
            chevron.pickingMode = PickingMode.Ignore;
            if (_catalog != null && _catalog.ChevronDown != null)
            {
                chevron.style.backgroundImage = new StyleBackground(_catalog.ChevronDown);
            }

            loadoutBtn.Add(chevron);
            loadoutWrap.Add(loadoutBtn);

            _loadoutMenu = new VisualElement();
            _loadoutMenu.AddToClassList("char-creator__loadout-menu");
            loadoutWrap.Add(_loadoutMenu);
            topRight.Add(loadoutWrap);

            Button returnBtn = new(() => ReturnToLobbyRequested?.Invoke())
            {
                text = "RETURN TO LOBBY",
            };
            returnBtn.AddToClassList("char-creator__return");
            returnBtn.AddToClassList("font-titling");
            topRight.Add(returnBtn);

            top.Add(topRight);
            window.Add(top);

            VisualElement main = new();
            main.AddToClassList("char-creator__main");
            window.Add(main);

            VisualElement rail = new();
            rail.AddToClassList("char-creator__rail");
            rail.name = "char-creator-rail";
            main.Add(rail);

            _bodyHost = new VisualElement();
            _bodyHost.AddToClassList("char-creator__body");
            main.Add(_bodyHost);

            VisualElement previewDock = new();
            previewDock.AddToClassList("char-creator__preview-dock");

            VisualElement previewFrame = new();
            previewFrame.AddToClassList("char-creator__preview-frame");
            _previewImage = new VisualElement();
            _previewImage.AddToClassList("char-creator__preview-image");
            previewFrame.Add(_previewImage);
            previewDock.Add(previewFrame);

            VisualElement angleRow = new();
            angleRow.AddToClassList("char-creator__angle-row");
            Button prevAngle = new(() =>
            {
                _angleIndex = (_angleIndex + CharacterCreatorMockData.AngleLabels.Length - 1)
                    % CharacterCreatorMockData.AngleLabels.Length;
                PreviewAngleChanged?.Invoke(_angleIndex);
                RefreshPreview();
            })
            {
                text = "‹",
            };
            prevAngle.AddToClassList("char-creator__angle-btn");
            prevAngle.AddToClassList("font-body");
            angleRow.Add(prevAngle);

            _angleLabel = new();
            _angleLabel.AddToClassList("char-creator__angle-label");
            _angleLabel.AddToClassList("font-terminal");
            angleRow.Add(_angleLabel);

            Button nextAngle = new(() =>
            {
                _angleIndex = (_angleIndex + 1) % CharacterCreatorMockData.AngleLabels.Length;
                PreviewAngleChanged?.Invoke(_angleIndex);
                RefreshPreview();
            })
            {
                text = "›",
            };
            nextAngle.AddToClassList("char-creator__angle-btn");
            nextAngle.AddToClassList("font-body");
            angleRow.Add(nextAngle);
            previewDock.Add(angleRow);

            main.Add(previewDock);
        }

        private void Rebuild()
        {
            if (_root == null || _bodyHost == null)
            {
                return;
            }

            RebuildRail();
            RebuildLoadoutChrome();
            _bodyHost.Clear();
            BuildStepContent(_bodyHost);
            BuildStepNav(_bodyHost);
            RefreshPreview();
        }

        private void RebuildRail()
        {
            VisualElement rail = _root.Q("char-creator-rail");
            if (rail == null)
            {
                return;
            }

            rail.Clear();
            for (int i = 0; i < CharacterCreatorMockData.Steps.Length; i++)
            {
                CharacterCreatorMockData.StepDef step = CharacterCreatorMockData.Steps[i];
                bool active = i == _stepIndex;
                int index = i;

                VisualElement row = new();
                row.AddToClassList("char-creator__rail-item");
                row.EnableInClassList("char-creator__rail-item--active", active);
                row.pickingMode = PickingMode.Position;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    _stepIndex = index;
                    Rebuild();
                });

                Label num = new((i + 1).ToString("00"));
                num.AddToClassList("char-creator__rail-num");
                num.AddToClassList("font-terminal");
                num.pickingMode = PickingMode.Ignore;
                row.Add(num);

                Label label = new(step.Label.ToUpperInvariant());
                label.AddToClassList("char-creator__rail-label");
                label.AddToClassList("font-titling");
                label.pickingMode = PickingMode.Ignore;
                row.Add(label);
                rail.Add(row);
            }
        }

        private void RebuildLoadoutChrome()
        {
            CharacterCreatorMockData.LoadoutDef selected = SelectedLoadout();
            if (_loadoutNameLabel != null)
            {
                _loadoutNameLabel.text = selected.Name;
            }

            ApplyThumb(_loadoutThumb, selected.ThumbId);

            if (_loadoutMenu == null)
            {
                return;
            }

            _loadoutMenu.Clear();
            _loadoutMenu.style.display = _loadoutOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_loadoutOpen)
            {
                return;
            }

            foreach (CharacterCreatorMockData.LoadoutDef loadout in CharacterCreatorMockData.Loadouts)
            {
                string id = loadout.Id;
                VisualElement row = new();
                row.AddToClassList("char-creator__loadout-row");
                row.EnableInClassList("char-creator__loadout-row--active", id == _loadoutId);
                row.pickingMode = PickingMode.Position;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    _loadoutId = id;
                    _editName = loadout.Name;
                    _loadoutOpen = false;
                    Rebuild();
                });

                VisualElement thumb = new();
                thumb.AddToClassList("char-creator__loadout-row-thumb");
                thumb.pickingMode = PickingMode.Ignore;
                ApplyThumb(thumb, loadout.ThumbId);
                row.Add(thumb);

                Label name = new(loadout.Name);
                name.AddToClassList("char-creator__loadout-row-name");
                name.AddToClassList("font-body");
                name.pickingMode = PickingMode.Ignore;
                row.Add(name);
                _loadoutMenu.Add(row);
            }

            VisualElement newRow = new();
            newRow.AddToClassList("char-creator__loadout-new");
            Label newLabel = new("+ NEW CHARACTER");
            newLabel.AddToClassList("char-creator__loadout-new-label");
            newLabel.AddToClassList("font-body");
            newRow.Add(newLabel);
            newRow.pickingMode = PickingMode.Position;
            newRow.RegisterCallback<ClickEvent>(_ =>
            {
                _editName = "New Character";
                _loadoutOpen = false;
                _stepIndex = 0;
                Rebuild();
            });
            _loadoutMenu.Add(newRow);
        }

        private void BuildStepContent(VisualElement host)
        {
            string key = CharacterCreatorMockData.Steps[_stepIndex].Key;
            switch (key)
            {
                case "identity":
                {
                    BuildIdentity(host);
                    break;
                }

                case "body":
                {
                    BuildBody(host);
                    break;
                }

                case "style":
                {
                    BuildStyle(host);
                    break;
                }

                default:
                {
                    BuildReview(host);
                    break;
                }
            }
        }

        private void BuildIdentity(VisualElement host)
        {
            VisualElement panel = new();
            panel.AddToClassList("char-creator__form");

            panel.Add(FieldLabel("Character Name"));
            TextField nameField = new() { value = _editName };
            nameField.AddToClassList("char-creator__input");
            nameField.AddToClassList("font-body");
            nameField.label = string.Empty;
            nameField.RegisterValueChangedCallback(evt => _editName = evt.newValue ?? string.Empty);
            StyleDarkTextField(nameField);
            panel.Add(nameField);

            panel.Add(FieldLabel("Species"));
            VisualElement speciesRow = new();
            speciesRow.AddToClassList("char-creator__species-row");
            foreach (string species in CharacterCreatorMockData.SpeciesOptions)
            {
                string value = species;
                Button chip = new(() =>
                {
                    _species = value;
                    Rebuild();
                })
                {
                    text = species.ToUpperInvariant(),
                };
                chip.AddToClassList("char-creator__species-chip");
                chip.AddToClassList("font-titling");
                chip.EnableInClassList("char-creator__species-chip--active", _species == value);
                speciesRow.Add(chip);
            }

            panel.Add(speciesRow);

            panel.Add(FieldLabel("Uniform Color"));
            VisualElement swatches = new();
            swatches.AddToClassList("char-creator__swatch-row");
            for (int i = 0; i < CharacterCreatorMockData.UniformSwatches.Length; i++)
            {
                int index = i;
                VisualElement swatch = new();
                swatch.AddToClassList("char-creator__swatch");
                swatch.EnableInClassList("char-creator__swatch--selected", _uniformIndex == index);
                swatch.style.backgroundColor = CharacterCreatorMockData.UniformSwatches[i];
                swatch.pickingMode = PickingMode.Position;
                swatch.RegisterCallback<ClickEvent>(_ =>
                {
                    _uniformIndex = index;
                    Rebuild();
                });
                swatches.Add(swatch);
            }

            panel.Add(swatches);
            host.Add(panel);
        }

        private void BuildBody(VisualElement host)
        {
            VisualElement panel = new();
            panel.AddToClassList("char-creator__form");
            panel.AddToClassList("char-creator__form--wide");

            Label section = new("BODY & SKIN");
            section.AddToClassList("char-creator__section");
            section.AddToClassList("font-titling");
            panel.Add(section);

            foreach (CharacterCreatorMockData.SliderDef def in CharacterCreatorMockData.BodySliders)
            {
                panel.Add(BuildSliderRow(def));
            }

            panel.Add(FieldLabel("Hair Color"));
            Color[] hairColors = _catalog != null && _catalog.HairColors != null && _catalog.HairColors.Count > 0
                ? _catalog.HairColors.ToArray()
                : CharacterCreatorMockData.HairColors;
            panel.Add(BuildColorRow(
                hairColors,
                _hairColorIndex,
                index =>
                {
                    _hairColorIndex = index;
                    NotifyStyleChanged();
                    Rebuild();
                }));

            panel.Add(FieldLabel("Eye Color"));
            panel.Add(BuildColorRow(
                CharacterCreatorMockData.EyeColors,
                _eyeColorIndex,
                index =>
                {
                    _eyeColorIndex = index;
                    Rebuild();
                }));

            host.Add(panel);
        }

        private VisualElement BuildSliderRow(CharacterCreatorMockData.SliderDef def)
        {
            VisualElement row = new();
            row.AddToClassList("char-creator__slider-row");

            Label label = new(def.Label);
            label.AddToClassList("char-creator__slider-label");
            label.AddToClassList("font-body");
            row.Add(label);

            float value = _sliders.TryGetValue(def.Key, out float stored) ? stored : def.DefaultValue;
            Label valueLabel = new(Mathf.RoundToInt(value * 100f).ToString());
            valueLabel.AddToClassList("char-creator__slider-value");
            valueLabel.AddToClassList("font-terminal");
            row.Add(valueLabel);

            Slider slider = new(0f, 1f, SliderDirection.Horizontal, 0f)
            {
                value = value,
                showInputField = false,
            };
            slider.AddToClassList("char-creator__slider");
            string key = def.Key;
            slider.RegisterValueChangedCallback(evt =>
            {
                _sliders[key] = evt.newValue;
                valueLabel.text = Mathf.RoundToInt(evt.newValue * 100f).ToString();
                NotifyBodyMorphsChanged();
            });
            row.Add(slider);
            return row;
        }

        private void BuildStyle(VisualElement host)
        {
            VisualElement panel = new();
            panel.AddToClassList("char-creator__form");
            panel.AddToClassList("char-creator__form--wide");

            VisualElement tabs = new();
            tabs.AddToClassList("char-creator__style-tabs");
            foreach (CharacterCreatorMockData.StyleTabDef tab in CharacterCreatorMockData.StyleTabs)
            {
                string key = tab.Key;
                Button btn = new(() =>
                {
                    _styleTab = key;
                    Rebuild();
                })
                {
                    text = tab.Label.ToUpperInvariant(),
                };
                btn.AddToClassList("char-creator__style-tab");
                btn.AddToClassList("font-titling");
                btn.EnableInClassList("char-creator__style-tab--active", _styleTab == key);
                tabs.Add(btn);
            }

            panel.Add(tabs);

            ScrollView gridScroll = new();
            gridScroll.AddToClassList("char-creator__style-scroll");
            VisualElement grid = new();
            grid.AddToClassList("char-creator__style-grid");

            switch (_styleTab)
            {
                case "facialHair":
                {
                    BuildBeardTiles(grid);
                    break;
                }

                case "eyebrows":
                {
                    BuildMockStyleTiles(grid, CharacterCreatorMockData.StyleOptionsFor("eyebrows"), _selectedBrows,
                        key => { _selectedBrows = key; });
                    break;
                }

                default:
                {
                    BuildHairTiles(grid);
                    break;
                }
            }

            gridScroll.Add(grid);
            panel.Add(gridScroll);
            host.Add(panel);
        }

        private void BuildHairTiles(VisualElement grid)
        {
            bool hasCatalog = _catalog != null && _catalog.HairStyles != null && _catalog.HairStyles.Count > 0;
            if (!hasCatalog)
            {
                BuildMockStyleTiles(grid, CharacterCreatorMockData.StyleOptionsFor("hair"), string.Empty, _ => { });
                return;
            }

            for (int i = 0; i < _catalog.HairStyles.Count; i++)
            {
                int index = i;
                LobbyNamedPrefab entry = _catalog.HairStyles[i];
                bool isNone = i == 0;
                string label = isNone ? "None" : entry.Id;
                VisualElement tile = BuildStyleTile(label, isNone, _hairStyleIndex == index, () =>
                {
                    _hairStyleIndex = index;
                    NotifyStyleChanged();
                    Rebuild();
                });
                grid.Add(tile);
            }
        }

        private void BuildBeardTiles(VisualElement grid)
        {
            bool hasCatalog = _catalog != null && _catalog.BeardStyles != null && _catalog.BeardStyles.Count > 0;
            if (!hasCatalog)
            {
                BuildMockStyleTiles(grid, CharacterCreatorMockData.StyleOptionsFor("facialHair"), string.Empty, _ => { });
                return;
            }

            for (int i = 0; i < _catalog.BeardStyles.Count; i++)
            {
                int index = i;
                LobbyNamedPrefab entry = _catalog.BeardStyles[i];
                bool isNone = i == 0;
                string label = isNone ? "None" : entry.Id;
                VisualElement tile = BuildStyleTile(label, isNone, _beardStyleIndex == index, () =>
                {
                    _beardStyleIndex = index;
                    NotifyStyleChanged();
                    Rebuild();
                });
                grid.Add(tile);
            }
        }

        private static void BuildMockStyleTiles(
            VisualElement grid,
            System.Collections.Generic.IReadOnlyList<CharacterCreatorMockData.StyleOptionDef> options,
            string selectedKey,
            Action<string> onSelect)
        {
            foreach (CharacterCreatorMockData.StyleOptionDef opt in options)
            {
                string key = opt.Key;
                VisualElement tile = BuildStyleTile(opt.Label, opt.IsNone, key == selectedKey, () => onSelect(key));
                grid.Add(tile);
            }
        }

        private static VisualElement BuildStyleTile(string label, bool isNone, bool isSelected, Action onClick)
        {
            VisualElement tile = new();
            tile.AddToClassList("char-creator__style-tile");
            tile.EnableInClassList("char-creator__style-tile--selected", isSelected);
            tile.pickingMode = PickingMode.Position;
            tile.RegisterCallback<ClickEvent>(_ => onClick());

            if (isNone)
            {
                Label noneMark = new("✕");
                noneMark.AddToClassList("char-creator__style-none");
                noneMark.AddToClassList("font-body");
                noneMark.pickingMode = PickingMode.Ignore;
                tile.Add(noneMark);
            }

            Label tileLabel = new(label.ToUpperInvariant());
            tileLabel.AddToClassList("char-creator__style-tile-label");
            tileLabel.AddToClassList("font-body");
            tileLabel.pickingMode = PickingMode.Ignore;
            tile.Add(tileLabel);
            return tile;
        }

        private void BuildReview(VisualElement host)
        {
            VisualElement panel = new();
            panel.AddToClassList("char-creator__form");

            Label heading = new("READY TO DEPLOY");
            heading.AddToClassList("char-creator__review-title");
            heading.AddToClassList("font-titling");
            panel.Add(heading);

            VisualElement summary = new();
            summary.AddToClassList("char-creator__review-card");
            summary.Add(ReviewLine("Name", _editName));
            summary.Add(ReviewLine("Species", _species));
            string hairLabel;
            if (_catalog != null
                && _catalog.HairStyles != null
                && _hairStyleIndex > 0
                && _hairStyleIndex < _catalog.HairStyles.Count)
            {
                hairLabel = _catalog.HairStyles[_hairStyleIndex].Id;
            }
            else
            {
                hairLabel = _hairStyleIndex == 0 ? "None" : "?";
            }
            summary.Add(ReviewLine("Hair", hairLabel));
            panel.Add(summary);

            Button save = new(SaveCharacter)
            {
                text = "SAVE CHARACTER",
            };
            save.AddToClassList("char-creator__save");
            save.AddToClassList("font-titling");
            panel.Add(save);
            host.Add(panel);
        }

        private static VisualElement ReviewLine(string label, string value)
        {
            VisualElement row = new();
            row.AddToClassList("char-creator__review-line");
            Label text = new($"{label}: {value}");
            text.AddToClassList("char-creator__review-text");
            text.AddToClassList("font-body");
            row.Add(text);
            return row;
        }

        private void BuildStepNav(VisualElement host)
        {
            VisualElement nav = new();
            nav.AddToClassList("char-creator__nav");

            bool first = _stepIndex <= 0;
            bool last = _stepIndex >= CharacterCreatorMockData.Steps.Length - 1;

            Button back = new(() =>
            {
                if (_stepIndex > 0)
                {
                    _stepIndex--;
                    Rebuild();
                }
            })
            {
                text = "BACK",
            };
            back.AddToClassList("char-creator__nav-btn");
            back.AddToClassList("font-titling");
            back.SetEnabled(!first);
            nav.Add(back);

            Button next = new(() =>
            {
                if (_stepIndex < CharacterCreatorMockData.Steps.Length - 1)
                {
                    _stepIndex++;
                    Rebuild();
                }
            })
            {
                text = "NEXT",
            };
            next.AddToClassList("char-creator__nav-btn");
            next.AddToClassList("char-creator__nav-btn--primary");
            next.AddToClassList("font-titling");
            next.SetEnabled(!last);
            nav.Add(next);

            host.Add(nav);
        }

        private VisualElement BuildColorRow(Color[] colors, int selectedIndex, Action<int> onSelect)
        {
            VisualElement row = new();
            row.AddToClassList("char-creator__swatch-row");
            for (int i = 0; i < colors.Length; i++)
            {
                int index = i;
                VisualElement swatch = new();
                swatch.AddToClassList("char-creator__swatch");
                swatch.AddToClassList("char-creator__swatch--small");
                swatch.EnableInClassList("char-creator__swatch--selected", selectedIndex == index);
                swatch.style.backgroundColor = colors[i];
                swatch.pickingMode = PickingMode.Position;
                swatch.RegisterCallback<ClickEvent>(_ => onSelect(index));
                row.Add(swatch);
            }

            return row;
        }

        private static Label FieldLabel(string text)
        {
            Label label = new(text.ToUpperInvariant());
            label.AddToClassList("char-creator__field-label");
            label.AddToClassList("font-body");
            return label;
        }

        private void RefreshPreview()
        {
            if (_angleLabel != null)
            {
                _angleLabel.text = CharacterCreatorMockData.AngleLabels[_angleIndex];
            }

            if (_previewImage == null)
            {
                return;
            }

            _previewImage.style.scale = new Scale(Vector3.one);

            if (_livePreviewTexture is RenderTexture renderTexture)
            {
                _previewImage.style.backgroundImage = Background.FromRenderTexture(renderTexture);
                return;
            }

            if (_livePreviewTexture is Texture2D texture2D)
            {
                _previewImage.style.backgroundImage = new StyleBackground(texture2D);
                return;
            }

            CharacterCreatorMockData.LoadoutDef loadout = SelectedLoadout();
            ApplyThumb(_previewImage, loadout.ThumbId);
        }

        private void SaveCharacter()
        {
            CharacterCreatorDraft draft = new()
            {
                Name = string.IsNullOrWhiteSpace(_editName) ? LobbyMockData.CharacterName : _editName.Trim(),
                HairStyleIndex = _hairStyleIndex,
                BeardStyleIndex = _beardStyleIndex,
                HairColorIndex = _hairColorIndex,
            };
            draft.CopyMorphsFrom(_sliders);
            _editName = draft.Name;
            CharacterSaved?.Invoke(draft);
        }

        private CharacterCreatorMockData.LoadoutDef SelectedLoadout()
        {
            foreach (CharacterCreatorMockData.LoadoutDef loadout in CharacterCreatorMockData.Loadouts)
            {
                if (loadout.Id == _loadoutId)
                {
                    return loadout;
                }
            }

            return CharacterCreatorMockData.Loadouts[0];
        }


        private void ApplyThumb(VisualElement target, string thumbId)
        {
            if (target == null)
            {
                return;
            }

            Sprite sprite = null;
            if (_catalog != null)
            {
                if (!_catalog.TryGetLoadoutThumb(thumbId, out sprite))
                {
                    sprite = _catalog.PreviewPlaceholder;
                }
            }

            target.style.backgroundImage = sprite != null
                ? new StyleBackground(sprite)
                : StyleKeyword.None;
        }

        private static void StyleDarkTextField(TextField field)
        {
            if (field == null)
            {
                return;
            }

            Color fill = new(0.1647059f, 0.1647059f, 0.1803922f, 1f);
            field.style.backgroundColor = fill;

            void Apply()
            {
                VisualElement input = field.Q(className: "unity-base-text-field__input")
                    ?? field.Q(className: "unity-text-field__input")
                    ?? field.Q(className: "unity-base-field__input");
                if (input != null)
                {
                    input.style.backgroundColor = fill;
                    input.style.borderTopWidth = 0;
                    input.style.borderRightWidth = 0;
                    input.style.borderBottomWidth = 0;
                    input.style.borderLeftWidth = 0;
                }
            }

            Apply();
            field.RegisterCallback<AttachToPanelEvent>(_ => Apply());
        }
    }
}
