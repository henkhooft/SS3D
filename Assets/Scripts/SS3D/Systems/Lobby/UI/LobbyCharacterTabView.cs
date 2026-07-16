using SS3D.Core.Behaviours;
using SS3D.Systems.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace SS3D.Systems.Lobby.UI
{
    /// <summary>
    /// Opens the character customizer from the lobby Character tab.
    /// </summary>
    public sealed class LobbyCharacterTabView : Actor
    {
        [SerializeField] private Button _openCustomizerButton;

        protected override void OnStart()
        {
            base.OnStart();

            if (_openCustomizerButton == null)
            {
                Transform buttonTransform = FindChildRecursive(transform, "Character Button");
                if (buttonTransform != null)
                {
                    _openCustomizerButton = buttonTransform.GetComponent<Button>();
                    if (_openCustomizerButton == null)
                    {
                        _openCustomizerButton = buttonTransform.gameObject.AddComponent<Button>();
                    }
                }
            }

            if (_openCustomizerButton != null)
            {
                _openCustomizerButton.onClick.AddListener(HandleOpenCustomizer);
            }
        }

        protected override void OnDestroyed()
        {
            base.OnDestroyed();

            if (_openCustomizerButton != null)
            {
                _openCustomizerButton.onClick.RemoveListener(HandleOpenCustomizer);
            }
        }

        private void HandleOpenCustomizer()
        {
            GameScreens.SwitchTo(ScreenType.CharacterCustomizer);
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
