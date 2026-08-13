using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Inputs;
using SS3D.UI.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.Lobby
{
    /// <summary>
    /// Self-bootstrapping host for the UITK pre-round lobby shell and Character Creator.
    /// Attaches into <see cref="UiLayer.Modal"/>. Phase A/B are visual + local mock state;
    /// Phase C gates visibility on spawn/round state and retires the condemned uGUI lobby.
    /// </summary>
    public sealed class LobbyUiSubSystem : SubSystem
    {
        private LobbyAssetCatalog _catalog;
        private LobbyShellView _shellView;
        private CharacterCreatorView _creatorView;
        private bool _attached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SubSystems.TryGet(out LobbyUiSubSystem _))
            {
                return;
            }

            GameObject host = new(nameof(LobbyUiSubSystem));
            DontDestroyOnLoad(host);
            host.AddComponent<LobbyUiSubSystem>();
        }

        protected override void OnDestroyed()
        {
            if (_shellView != null)
            {
                _shellView.CharacterCreatorRequested -= HandleCharacterCreatorRequested;
                _shellView.Detach();
                _shellView = null;
            }

            if (_creatorView != null)
            {
                _creatorView.ReturnToLobbyRequested -= HandleReturnToLobbyRequested;
                _creatorView.Detach();
                _creatorView = null;
            }

            base.OnDestroyed();
        }

        private void Update()
        {
            if (_attached)
            {
                return;
            }

            TryAttachShell();
        }

        private void TryAttachShell()
        {
            if (!TryEnsureCatalog())
            {
                // Permanent failure until catalogs are rebuilt — stop retrying.
                _attached = true;
                return;
            }

            if (!SubSystems.TryGet(out UiShellSubSystem uiShell)
                || !uiShell.TryGetLayer(UiLayer.Modal, out VisualElement modalLayer))
            {
                // Shell may not have bootstrapped yet — retry next frame.
                return;
            }

            _attached = true;
            InputInterface.RegisterDocument(uiShell.Document);

            _shellView = new LobbyShellView(_catalog);
            _shellView.Attach(modalLayer);
            _shellView.CharacterCreatorRequested += HandleCharacterCreatorRequested;

            _creatorView = new CharacterCreatorView(_catalog);
            _creatorView.Attach(modalLayer);
            _creatorView.ReturnToLobbyRequested += HandleReturnToLobbyRequested;

            // Phase A/B: keep lobby visible for visual QA. Old uGUI LobbyCanvas may still draw
            // underneath or above depending on canvas sort — disable that canvas while checking.
            _shellView.SetVisible(true);
            _creatorView.SetVisible(false);
            Debug.Log("[Lobby] UITK Lobby Shell + Character Creator attached (Phase A/B mock data).");
        }

        private bool TryEnsureCatalog()
        {
            if (_catalog != null)
            {
                return true;
            }

            _catalog = Resources.Load<LobbyAssetCatalog>(LobbyAssetPaths.ResourcesCatalogName);
            if (_catalog == null)
            {
                Debug.LogError(
                    $"LobbyUiSubSystem could not load Resources/{LobbyAssetPaths.ResourcesCatalogName}. "
                    + "Run SS3D → Data → Rebuild All UI Catalogs and commit the asset.",
                    this);
                return false;
            }

            if (!_catalog.HasRequiredAssets(out string missingField))
            {
                Debug.LogError(
                    $"LobbyAssetCatalog is missing required field '{missingField}'. "
                    + "Run SS3D → Data → Rebuild All UI Catalogs.",
                    this);
                return false;
            }

            return true;
        }

        private void HandleCharacterCreatorRequested()
        {
            if (_shellView == null || _creatorView == null)
            {
                return;
            }

            _shellView.SetVisible(false);
            _creatorView.Open();
            Debug.Log("[Lobby] Character Creator opened.");
        }

        private void HandleReturnToLobbyRequested()
        {
            if (_shellView == null || _creatorView == null)
            {
                return;
            }

            _creatorView.SetVisible(false);
            _shellView.SetVisible(true);
            Debug.Log("[Lobby] Returned to Lobby Shell from Character Creator.");
        }
    }
}
