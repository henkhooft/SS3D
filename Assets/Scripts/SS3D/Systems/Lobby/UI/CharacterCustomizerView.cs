using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Entities.Character;
using SS3D.Systems.Screens;
using SS3D.UI.Buttons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Coimbra;

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
                boothObject.transform.SetParent(transform, false);
                _previewBooth = boothObject.AddComponent<CharacterPreviewBooth>();
            }

            if (_previewImage == null)
            {
                Transform preview = FindChildRecursive(transform, "Character Preview");
                if (preview != null)
                {
                    _previewImage = preview.GetComponent<RawImage>();
                    if (_previewImage == null)
                    {
                        _previewImage = preview.gameObject.AddComponent<RawImage>();
                        _previewImage.color = Color.white;
                    }
                }
            }

            _previewBooth.Initialize(_humanPrefab, _catalog, _previewImage);
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
                Transform content = FindChildRecursive(transform, "Content");
                _optionsContent = content != null ? content : transform;
            }

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
                buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(_optionsContent, false);

                RectTransform rect = buttonObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(0f, 36f);

                Image image = buttonObject.GetComponent<Image>();
                image.color = new Color(0.22f, 0.22f, 0.26f, 1f);

                GameObject textObject = new("Label");
                textObject.transform.SetParent(buttonObject.transform, false);
                RectTransform textRect = textObject.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(8f, 0f);
                textRect.offsetMax = new Vector2(-8f, 0f);
                TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
                text.text = label;
                text.fontSize = 16f;
                text.color = Color.white;
                text.alignment = TextAlignmentOptions.MidlineLeft;
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
