using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Entities.Character;
using SS3D.Systems.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SS3D.Systems.Lobby.UI
{
    /// <summary>
    /// Opens the character customizer from the lobby character summary (name + preview panel).
    /// </summary>
    public sealed class LobbyCharacterTabView : Actor
    {
        [SerializeField] private Button _openCustomizerButton;
        [SerializeField] private TMP_Text _characterNameLabel;

        private Button _nameHeaderButton;

        protected override void OnAwake()
        {
            base.OnAwake();

            EnsureReferences();
            WireOpenButtons();
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();

            RefreshCharacterName();
        }

        protected override void OnDestroyed()
        {
            base.OnDestroyed();

            UnwireOpenButtons();
        }

        /// <summary>
        /// Invoked by the Character Button in the lobby UI (inspector or runtime wiring).
        /// </summary>
        public void OpenCustomizer()
        {
            GameScreens.SwitchTo(ScreenType.CharacterCustomizer);
        }

        private void EnsureReferences()
        {
            if (_openCustomizerButton == null)
            {
                Transform buttonTransform = FindChildRecursive(transform, "Character Button");
                if (buttonTransform != null)
                {
                    _openCustomizerButton = buttonTransform.GetComponent<Button>();
                }
            }

            if (_characterNameLabel == null)
            {
                Transform nameRoot = FindChildRecursive(transform, "Character Name");
                if (nameRoot != null)
                {
                    _characterNameLabel = nameRoot.GetComponentInChildren<TMP_Text>(true);
                }
            }
        }

        private void WireOpenButtons()
        {
            if (_openCustomizerButton != null)
            {
                _openCustomizerButton.onClick.RemoveListener(OpenCustomizer);
                _openCustomizerButton.onClick.AddListener(OpenCustomizer);
            }

            Transform nameRoot = FindChildRecursive(transform, "Character Name");
            if (nameRoot == null)
            {
                return;
            }

            _nameHeaderButton = nameRoot.GetComponent<Button>();
            if (_nameHeaderButton == null)
            {
                _nameHeaderButton = nameRoot.gameObject.AddComponent<Button>();

                Image image = nameRoot.GetComponent<Image>();
                if (image != null)
                {
                    _nameHeaderButton.targetGraphic = image;
                }
            }

            _nameHeaderButton.onClick.RemoveListener(OpenCustomizer);
            _nameHeaderButton.onClick.AddListener(OpenCustomizer);
        }

        private void UnwireOpenButtons()
        {
            if (_openCustomizerButton != null)
            {
                _openCustomizerButton.onClick.RemoveListener(OpenCustomizer);
            }

            if (_nameHeaderButton != null)
            {
                _nameHeaderButton.onClick.RemoveListener(OpenCustomizer);
            }
        }

        private void RefreshCharacterName()
        {
            if (_characterNameLabel == null)
            {
                return;
            }

            CharacterSubSystem characterSystem = SubSystems.Get<CharacterSubSystem>();
            if (characterSystem == null)
            {
                return;
            }

            CharacterSheet sheet = characterSystem.LocalDraft;
            _characterNameLabel.text = string.IsNullOrWhiteSpace(sheet.Name)
                ? "Character name"
                : sheet.Name;
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
