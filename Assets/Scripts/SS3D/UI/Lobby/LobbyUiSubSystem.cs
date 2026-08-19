using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Entities.Character;
using SS3D.Systems.Inputs;
using SS3D.UI.Shell;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.Lobby
{
    /// <summary>
    /// Self-bootstrapping host for the UITK pre-round lobby shell and Character Creator.
    /// Attaches into <see cref="UiLayer.Modal"/>. Phase E1: live booth preview + local draft.
    /// Phase C gates visibility on spawn/round state and retires the condemned uGUI lobby.
    /// </summary>
    public sealed class LobbyUiSubSystem : SubSystem
    {
        private LobbyAssetCatalog _catalog;
        private LobbyShellView _shellView;
        private CharacterCreatorView _creatorView;
        private CharacterPreviewBooth _booth;
        private CharacterCreatorDraft _draft;
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
                _creatorView.CharacterSaved -= HandleCharacterSaved;
                _creatorView.PreviewAngleChanged -= HandlePreviewAngleChanged;
            _creatorView.BodyMorphsChanged -= HandleBodyMorphsChanged;
            _creatorView.StyleChanged -= HandleStyleChanged;
            _creatorView.Detach();
                _creatorView = null;
            }

            DisposeBooth();
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
                _attached = true;
                return;
            }

            if (!SubSystems.TryGet(out UiShellSubSystem uiShell)
                || !uiShell.TryGetLayer(UiLayer.Modal, out VisualElement modalLayer))
            {
                return;
            }

            _attached = true;
            InputInterface.RegisterDocument(uiShell.Document);

            _draft = new CharacterCreatorDraft();

            _shellView = new LobbyShellView(_catalog);
            _shellView.Attach(modalLayer);
            _shellView.CharacterCreatorRequested += HandleCharacterCreatorRequested;
            _shellView.SetCharacterName(_draft.Name);

            _creatorView = new CharacterCreatorView(_catalog);
            _creatorView.Attach(modalLayer);
            _creatorView.ReturnToLobbyRequested += HandleReturnToLobbyRequested;
            _creatorView.CharacterSaved += HandleCharacterSaved;
            _creatorView.PreviewAngleChanged += HandlePreviewAngleChanged;
            _creatorView.BodyMorphsChanged += HandleBodyMorphsChanged;
            _creatorView.StyleChanged += HandleStyleChanged;
            _creatorView.ApplyDraft(_draft);

            _shellView.SetVisible(true);
            _creatorView.SetVisible(false);
            Debug.Log("[Lobby] UITK Lobby Shell + Character Creator attached (Phase E1 + hair styles).");
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

            if (!EnsureBooth())
            {
                Debug.LogWarning("[Lobby] Character Creator opened without live preview (human prefab missing).");
            }
            else
            {
                ApplyBodyMorphsToBooth(_creatorView.CurrentBodyMorphs);
                _creatorView.NotifyStyleChanged();
            }

            PushPreviewTexture();
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

            if (_booth != null)
            {
                // Keep booth warm for lobby sidebar RT; camera stays on.
                _booth.SetActive(true);
            }

            PushPreviewTexture();
            _shellView.SetCharacterName(_draft?.Name ?? LobbyMockData.CharacterName);
            _creatorView.SetVisible(false);
            _shellView.SetVisible(true);
            Debug.Log("[Lobby] Returned to Lobby Shell from Character Creator.");
        }

        private void HandleCharacterSaved(CharacterCreatorDraft draft)
        {
            if (draft == null)
            {
                return;
            }

            _draft = draft;
            _shellView?.SetCharacterName(_draft.Name);
            ApplyBodyMorphsToBooth(_draft.BodyMorphs);
            ApplyStyleToBooth(_draft.HairStyleIndex, _draft.BeardStyleIndex, _draft.HairColorIndex);
            Debug.Log($"[Lobby] Character draft saved locally: {_draft.Name}");
        }

        private void HandlePreviewAngleChanged(int angleIndex)
        {
            if (_booth != null)
            {
                _booth.SetAngleIndex(angleIndex);
            }
        }

        private void HandleBodyMorphsChanged(IReadOnlyDictionary<string, float> morphs)
        {
            if (_draft != null)
            {
                _draft.CopyMorphsFrom(morphs);
            }

            ApplyBodyMorphsToBooth(morphs);
        }

        private void HandleStyleChanged(int hairIndex, int beardIndex, int colorIndex)
        {
            if (_draft != null)
            {
                _draft.HairStyleIndex = hairIndex;
                _draft.BeardStyleIndex = beardIndex;
                _draft.HairColorIndex = colorIndex;
            }

            ApplyStyleToBooth(hairIndex, beardIndex, colorIndex);
        }

        private void ApplyStyleToBooth(int hairIndex, int beardIndex, int colorIndex)
        {
            if (_booth == null || _catalog == null)
            {
                return;
            }

            GameObject hairPrefab = _catalog.GetHairStyle(hairIndex);
            GameObject beardPrefab = _catalog.GetBeardStyle(beardIndex);
            Color hairColor = _catalog.GetHairColor(colorIndex);
            _booth.ApplyStyle(hairPrefab, beardPrefab, hairColor);
        }

        private void ApplyBodyMorphsToBooth(IReadOnlyDictionary<string, float> morphs)
        {
            if (_booth == null || morphs == null)
            {
                return;
            }

            float Get(string key, float fallback) =>
                morphs.TryGetValue(key, out float value) ? value : fallback;

            _booth.ApplyBodyMorphs(
                female: Get("female", 0f),
                breasts: Get("breasts", 0f),
                fat: Get("fat", 0f),
                muscle: Get("muscle", 0f),
                jaw: Get("jaw", 0.5f),
                height: Get("height", 0.5f));
        }

        private bool EnsureBooth()
        {
            if (_booth != null)
            {
                _booth.SetActive(true);
                return _booth.PreviewTexture != null;
            }

            GameObject prefab = _catalog != null ? _catalog.PreviewHumanPrefab : null;
            if (prefab == null)
            {
                return false;
            }

            GameObject host = new("CharacterPreviewBooth");
            DontDestroyOnLoad(host);
            _booth = host.AddComponent<CharacterPreviewBooth>();
            _booth.Initialize(prefab);
            return _booth.PreviewTexture != null;
        }

        private void PushPreviewTexture()
        {
            Texture texture = _booth != null ? _booth.PreviewTexture : null;
            _creatorView?.SetPreviewTexture(texture);
            _shellView?.SetPreviewTexture(texture);
        }

        private void DisposeBooth()
        {
            if (_booth == null)
            {
                return;
            }

            _booth.DisposeBooth();
            _booth = null;
        }
    }
}
