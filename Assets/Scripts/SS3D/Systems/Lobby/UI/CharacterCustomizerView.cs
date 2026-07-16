using Coimbra;
using SS3D.Core;
using SS3D.Systems.Entities.Character;
using SS3D.Systems.Screens;
using SS3D.UI.Buttons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Actor = SS3D.Core.Behaviours.Actor;

namespace SS3D.Systems.Lobby.UI
{
    /// <summary>
    /// Wires the Character Customizer screen: name, style pickers, live preview, submit/back.
    /// </summary>
    public sealed class CharacterCustomizerView : Actor
    {
        private enum Category
        {
            Hair,
            Beard,
            Appearance,
        }

        [Header("Catalog / Preview")]
        [SerializeField] private AppearanceCatalog _catalog;
        [SerializeField] private GameObject _humanPrefab;
        [SerializeField] private CharacterPreviewBooth _previewBooth;
        [SerializeField] private RawImage _previewImage;

        [Header("UI")]
        [SerializeField] private TMP_InputField _nameField;
        [SerializeField] private Transform _optionsContent;
        [SerializeField] private LabelButton _backButton;
        [SerializeField] private LabelButton _hairTabButton;
        [SerializeField] private LabelButton _beardTabButton;
        [SerializeField] private LabelButton _appearanceTabButton;
        [SerializeField] private GameObject _optionButtonPrefab;

        private CharacterSheet _draft;
        private Category _category = Category.Hair;

        protected override void OnStart()
        {
            base.OnStart();

            ResolveCatalog();
            EnsureNameField();
            EnsurePreview();
            WireButtons();

            CharacterSubSystem characterSystem = SubSystems.Get<CharacterSubSystem>();
            _draft = characterSystem != null ? characterSystem.LocalDraft : CharacterSheet.CreateDefault(Core.Settings.LocalPlayer.Ckey);

            if (_nameField != null)
            {
                _nameField.SetTextWithoutNotify(_draft.Name);
                _nameField.onValueChanged.AddListener(HandleNameChanged);
            }

            RefreshOptions();
            ApplyPreview();
        }

        protected override void OnDestroyed()
        {
            base.OnDestroyed();

            UnwireButtons();

            if (_nameField != null)
            {
                _nameField.onValueChanged.RemoveListener(HandleNameChanged);
            }

            if (_previewBooth != null)
            {
                Destroy(_previewBooth.gameObject);
                _previewBooth = null;
            }
        }

        private void ResolveCatalog()
        {
            if (_catalog != null)
            {
                return;
            }

            CharacterSubSystem characterSystem = SubSystems.Get<CharacterSubSystem>();
            if (characterSystem != null)
            {
                _catalog = characterSystem.Catalog;
            }
        }

        private void EnsurePreview()
        {
            if (_previewBooth == null)
            {
                _previewBooth = GetComponentInChildren<CharacterPreviewBooth>(true);
            }

            if (_previewBooth == null)
            {
                GameObject boothObject = new("CharacterPreviewBooth");
                // World-space root; CharacterPreviewBooth.Initialize also detaches from the canvas.
                _previewBooth = boothObject.AddComponent<CharacterPreviewBooth>();
            }

            if (_previewImage == null)
            {
                _previewImage = ResolvePreviewImage();
            }

            if (_previewBooth != null)
            {
                _previewBooth.Initialize(_humanPrefab, _catalog, _previewImage);
            }
        }

        private RawImage ResolvePreviewImage()
        {
            Transform preview = FindChildRecursive(transform, "Character Preview");
            if (preview == null)
            {
                return null;
            }

            RawImage rawImage = preview.GetComponent<RawImage>();
            if (rawImage != null)
            {
                rawImage.enabled = true;
                return rawImage;
            }

            // Character Preview ships with a UI Image placeholder; RawImage cannot coexist with it.
            Image placeholder = preview.GetComponent<Image>();
            if (placeholder != null)
            {
                Destroy(placeholder);
            }

            rawImage = preview.gameObject.AddComponent<RawImage>();
            if (rawImage != null)
            {
                rawImage.color = Color.white;
                rawImage.enabled = true;
            }

            return rawImage;
        }

        private void EnsureNameField()
        {
            if (_nameField != null)
            {
                return;
            }

            Transform header = FindChildRecursive(transform, "Header");
            Transform parent = header != null ? header : transform;

            GameObject fieldObject = new("CharacterNameInput");
            fieldObject.transform.SetParent(parent, false);

            RectTransform rect = fieldObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -8f);
            rect.sizeDelta = new Vector2(280f, 36f);

            Image background = fieldObject.AddComponent<Image>();
            background.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);

            GameObject textArea = new("Text Area");
            textArea.transform.SetParent(fieldObject.transform, false);
            RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(8f, 4f);
            textAreaRect.offsetMax = new Vector2(-8f, -4f);

            GameObject textObject = new("Text");
            textObject.transform.SetParent(textArea.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject placeholderObject = new("Placeholder");
            placeholderObject.transform.SetParent(textArea.transform, false);
            RectTransform placeholderRect = placeholderObject.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            TextMeshProUGUI placeholder = placeholderObject.AddComponent<TextMeshProUGUI>();
            placeholder.fontSize = 18f;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            placeholder.text = "Character name";
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            _nameField = fieldObject.AddComponent<TMP_InputField>();
            _nameField.textViewport = textAreaRect;
            _nameField.textComponent = text;
            _nameField.placeholder = placeholder;
            _nameField.characterLimit = CharacterSheet.MaxNameLength;
        }

        private void WireButtons()
        {
            if (_backButton == null)
            {
                _backButton = FindLabelButton("Back");
            }

            EnsureDoneButton();

            if (_hairTabButton == null)
            {
                _hairTabButton = FindLabelButton("Hair");
            }

            if (_beardTabButton == null)
            {
                _beardTabButton = FindLabelButton("Beard");
            }

            if (_appearanceTabButton == null)
            {
                _appearanceTabButton = FindLabelButton("Appearance");
            }

            if (_optionsContent == null)
            {
                // Prefer the Options slot — never use root "Content" (that holds Header + panels).
                Transform options = FindChildRecursive(transform, "Options");
                _optionsContent = options != null ? options : null;
            }

            EnsureOptionsLayout();

            if (_backButton != null)
            {
                _backButton.OnPressedDown += HandleBackPressed;
            }

            if (_hairTabButton != null)
            {
                _hairTabButton.OnPressedDown += HandleHairTab;
            }

            if (_beardTabButton != null)
            {
                _beardTabButton.OnPressedDown += HandleBeardTab;
            }

            if (_appearanceTabButton != null)
            {
                _appearanceTabButton.OnPressedDown += HandleAppearanceTab;
            }
        }

        private void EnsureOptionsLayout()
        {
            if (_optionsContent == null)
            {
                return;
            }

            Image panelImage = _optionsContent.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.enabled = true;
                panelImage.color = new Color(0.12f, 0.12f, 0.14f, 0.92f);
                if (panelImage.sprite == null)
                {
                    panelImage.sprite = ResolveUiSprite();
                }
            }

            LayoutElement panelLayout = _optionsContent.GetComponent<LayoutElement>();
            if (panelLayout != null)
            {
                panelLayout.minWidth = 140f;
                panelLayout.preferredWidth = 160f;
                panelLayout.flexibleWidth = 0f;
                panelLayout.flexibleHeight = 1f;
            }

            EnsureSideMenuLayout();
            EnsurePreviewLayout();

            VerticalLayoutGroup layout = _optionsContent.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = _optionsContent.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(8, 8, 8, 8);

            ContentSizeFitter fitter = _optionsContent.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                Destroy(fitter);
            }
        }

        private void EnsureSideMenuLayout()
        {
            Transform sideMenu = FindChildRecursive(transform, "Side Menu");
            if (sideMenu == null)
            {
                return;
            }

            LayoutElement sideLayout = sideMenu.GetComponent<LayoutElement>();
            if (sideLayout == null)
            {
                sideLayout = sideMenu.gameObject.AddComponent<LayoutElement>();
            }

            sideLayout.minWidth = 120f;
            sideLayout.preferredWidth = 140f;
            sideLayout.flexibleWidth = 0f;
            sideLayout.flexibleHeight = 1f;

            VerticalLayoutGroup vertical = sideMenu.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
            {
                vertical.childControlWidth = true;
                vertical.childForceExpandWidth = true;
                vertical.childControlHeight = true;
                vertical.childForceExpandHeight = false;
                vertical.spacing = 8f;
            }

            TextMeshProUGUI[] labels = sideMenu.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].enableWordWrapping = false;
                labels[i].overflowMode = TextOverflowModes.Ellipsis;
                labels[i].alignment = TextAlignmentOptions.Center;
            }

            for (int i = 0; i < sideMenu.childCount; i++)
            {
                Transform child = sideMenu.GetChild(i);
                LayoutElement childLayout = child.GetComponent<LayoutElement>();
                if (childLayout == null)
                {
                    childLayout = child.gameObject.AddComponent<LayoutElement>();
                }

                childLayout.minHeight = 36f;
                childLayout.preferredHeight = 40f;
                childLayout.flexibleWidth = 1f;
            }
        }

        private void EnsurePreviewLayout()
        {
            Transform preview = FindChildRecursive(transform, "Character Preview");
            if (preview == null)
            {
                return;
            }

            LayoutElement previewLayout = preview.GetComponent<LayoutElement>();
            if (previewLayout == null)
            {
                previewLayout = preview.gameObject.AddComponent<LayoutElement>();
            }

            previewLayout.minWidth = 280f;
            previewLayout.preferredWidth = -1f;
            previewLayout.flexibleWidth = 1f;
            previewLayout.flexibleHeight = 1f;
        }

        private Sprite ResolveUiSprite()
        {
            if (_uiSprite != null)
            {
                return _uiSprite;
            }

            // Prefer a sprite already used by this canvas (Unity 6 has no UI/Skin/UISprite.psd).
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].sprite != null)
                {
                    _uiSprite = images[i].sprite;
                    return _uiSprite;
                }
            }

            Texture2D texture = Texture2D.whiteTexture;
            _uiSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return _uiSprite;
        }

        private static Sprite _uiSprite;

        private void UnwireButtons()
        {
            if (_backButton != null)
            {
                _backButton.OnPressedDown -= HandleBackPressed;
            }

            if (_hairTabButton != null)
            {
                _hairTabButton.OnPressedDown -= HandleHairTab;
            }

            if (_beardTabButton != null)
            {
                _beardTabButton.OnPressedDown -= HandleBeardTab;
            }

            if (_appearanceTabButton != null)
            {
                _appearanceTabButton.OnPressedDown -= HandleAppearanceTab;
            }
        }

        private void EnsureDoneButton()
        {
            if (_nameField == null)
            {
                return;
            }

            GameObject doneObject = new("Done", typeof(RectTransform), typeof(Image));
            doneObject.transform.SetParent(_nameField.transform.parent, false);

            RectTransform rect = doneObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(180f, -8f);
            rect.sizeDelta = new Vector2(72f, 36f);

            Image image = doneObject.GetComponent<Image>();
            image.color = new Color(0.25f, 0.45f, 0.3f, 1f);

            GameObject labelObject = new("Label");
            labelObject.transform.SetParent(doneObject.transform, false);
            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = "Done";
            label.fontSize = 16f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            Button button = doneObject.AddComponent<Button>();
            button.onClick.AddListener(() => HandleSubmitPressed(true));
        }

        private LabelButton FindLabelButton(string objectName)
        {
            Transform found = FindChildRecursive(transform, objectName);
            return found != null ? found.GetComponent<LabelButton>() : null;
        }

        private void HandleHairTab(bool _)
        {
            _category = Category.Hair;
            RefreshOptions();
        }

        private void HandleBeardTab(bool _)
        {
            _category = Category.Beard;
            RefreshOptions();
        }

        private void HandleAppearanceTab(bool _)
        {
            _category = Category.Appearance;
            RefreshOptions();
        }

        private void HandleNameChanged(string value)
        {
            _draft.Name = CharacterSheet.SanitizeName(value);
            PersistDraft();
        }

        private void HandleBackPressed(bool _)
        {
            PersistDraft();
            _previewBooth?.SetActive(false);
            GameScreens.SwitchTo(ScreenType.Lobby);
        }

        private void HandleSubmitPressed(bool _)
        {
            _draft.Name = CharacterSheet.SanitizeName(_nameField != null ? _nameField.text : _draft.Name);
            CharacterSubSystem characterSystem = SubSystems.Get<CharacterSubSystem>();
            if (characterSystem != null)
            {
                characterSystem.SubmitLocalSheet(_draft);
            }

            _previewBooth?.SetActive(false);
            GameScreens.SwitchTo(ScreenType.Lobby);
        }

        private void PersistDraft()
        {
            CharacterSubSystem characterSystem = SubSystems.Get<CharacterSubSystem>();
            if (characterSystem != null)
            {
                characterSystem.LocalDraft = _draft;
            }
        }

        private void ApplyPreview()
        {
            _previewBooth?.SetActive(true);
            _previewBooth?.ApplySheet(_draft);
        }

        private void RefreshOptions()
        {
            if (_optionsContent == null)
            {
                return;
            }

            // Only clear generated option buttons — never wipe Header / Side Menu / Preview.
            for (int i = _optionsContent.childCount - 1; i >= 0; i--)
            {
                _optionsContent.GetChild(i).gameObject.Dispose(true);
            }

            switch (_category)
            {
                case Category.Hair:
                    BuildHairOptions();
                    break;
                case Category.Beard:
                    BuildBeardOptions();
                    break;
                case Category.Appearance:
                    BuildAppearanceOptions();
                    break;
            }
        }

        private void BuildHairOptions()
        {
            int count = _catalog != null ? Mathf.Max(1, _catalog.HairStyleCount) : 1;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                string label = _catalog != null && _catalog.GetHairStyle(i) != null
                    ? _catalog.GetHairStyle(i).name
                    : $"Hair {i}";
                CreateOptionButton(label, () =>
                {
                    _draft.HairStyleId = index;
                    PersistDraft();
                    ApplyPreview();
                });
            }
        }

        private void BuildBeardOptions()
        {
            int count = _catalog != null ? Mathf.Max(1, _catalog.BeardStyleCount) : 1;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                GameObject style = _catalog != null ? _catalog.GetBeardStyle(i) : null;
                string label = style == null ? "None" : style.name;
                CreateOptionButton(label, () =>
                {
                    _draft.BeardStyleId = index;
                    PersistDraft();
                    ApplyPreview();
                });
            }
        }

        private void BuildAppearanceOptions()
        {
            int skinCount = _catalog != null ? Mathf.Max(1, _catalog.SkinToneCount) : 1;
            for (int i = 0; i < skinCount; i++)
            {
                int index = i;
                CreateOptionButton($"Skin {i + 1}", () =>
                {
                    _draft.SkinToneIndex = index;
                    PersistDraft();
                    ApplyPreview();
                });
            }

            int hairColorCount = _catalog != null ? Mathf.Max(1, _catalog.HairColorCount) : 1;
            for (int i = 0; i < hairColorCount; i++)
            {
                int index = i;
                CreateOptionButton($"Hair color {i + 1}", () =>
                {
                    _draft.HairColorIndex = index;
                    PersistDraft();
                    ApplyPreview();
                });
            }
        }

        private void CreateOptionButton(string label, System.Action onClick)
        {
            GameObject buttonObject;
            if (_optionButtonPrefab != null)
            {
                buttonObject = Instantiate(_optionButtonPrefab, _optionsContent);
            }
            else
            {
                buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                buttonObject.transform.SetParent(_optionsContent, false);
                buttonObject.layer = _optionsContent.gameObject.layer;

                RectTransform rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, 40f);

                LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
                layoutElement.minHeight = 32f;
                layoutElement.preferredHeight = 32f;
                layoutElement.flexibleWidth = 1f;

                Image image = buttonObject.GetComponent<Image>();
                image.sprite = ResolveUiSprite();
                image.type = Image.Type.Sliced;
                image.color = new Color(0.22f, 0.22f, 0.26f, 1f);
                image.raycastTarget = true;

                GameObject textObject = new("Label");
                textObject.transform.SetParent(buttonObject.transform, false);
                textObject.layer = buttonObject.layer;
                RectTransform textRect = textObject.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(8f, 2f);
                textRect.offsetMax = new Vector2(-8f, -2f);
                TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
                text.text = label;
                text.fontSize = 14f;
                text.color = Color.white;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.raycastTarget = false;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }

            buttonObject.name = label;

            TextMeshProUGUI existingLabel = buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);
            if (existingLabel != null)
            {
                existingLabel.text = label;
            }

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                button = buttonObject.AddComponent<Button>();
            }

            Image targetGraphic = buttonObject.GetComponent<Image>();
            if (targetGraphic != null)
            {
                button.targetGraphic = targetGraphic;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
