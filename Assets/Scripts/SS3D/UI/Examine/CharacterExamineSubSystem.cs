using System.Collections.Generic;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Localization;
using SS3D.Systems.Examine;
using SS3D.Systems.Inputs;
using SS3D.UI.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SS3D.UI.Examine
{
    /// <summary>
    /// Owns both character-examine UI surfaces (hover quick-look + persistent full window) and the
    /// hover/Shift+Click state machine driving them. Self-bootstraps like <see cref="UiShellSubSystem"/>
    /// so adopting it needs no Boot/Game scene edit.
    /// </summary>
    public sealed class CharacterExamineSubSystem : SubSystem
    {
        private const string UnidentifiedKey = "character_examine.unidentified_crew_member";
        private const string UnidentifiedFallback = "Unidentified Crew Member";

        private ExamineSubSystem _examineSystem;
        private CharacterExamineAssetCatalog _catalog;
        private CharacterQuickLookView _quickLookView;
        private CharacterExamineWindowView _windowView;
        private CharacterExaminable _hoveredCharacter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SubSystems.TryGet(out CharacterExamineSubSystem _))
            {
                return;
            }

            GameObject host = new(nameof(CharacterExamineSubSystem));
            Object.DontDestroyOnLoad(host);
            host.AddComponent<CharacterExamineSubSystem>();
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();

            if (TryResolveExamineSystem())
            {
                _examineSystem.OnExaminableChanged += HandleExaminableChanged;
                _examineSystem.OnCharacterWindowRequested += HandleWindowRequested;
            }
        }

        protected override void OnDisabled()
        {
            base.OnDisabled();

            if (_examineSystem != null)
            {
                _examineSystem.OnExaminableChanged -= HandleExaminableChanged;
                _examineSystem.OnCharacterWindowRequested -= HandleWindowRequested;
            }
        }

        protected override void OnDestroyed()
        {
            _quickLookView?.Detach();
            _windowView?.Detach();
            _quickLookView = null;
            _windowView = null;
            base.OnDestroyed();
        }

        private void Update()
        {
            if (_windowView == null)
            {
                return;
            }

            if (_windowView.IsOpen)
            {
                _windowView.Tick(Time.deltaTime);
                return;
            }

            if (_hoveredCharacter != null && Mouse.current != null)
            {
                _quickLookView.UpdateAnchor(Mouse.current.position.ReadValue());
            }
        }

        private bool TryResolveExamineSystem()
        {
            if (_examineSystem != null)
            {
                return true;
            }

            return SubSystems.TryGet(out _examineSystem);
        }

        private void HandleExaminableChanged(IExaminable examinable)
        {
            _hoveredCharacter = examinable as CharacterExaminable;

            if (!EnsureViews())
            {
                return;
            }

            // The full window pins its own target; hover changes elsewhere don't affect the preview
            // while it's open (mirrors the design mock's `if (!fullOpen) showPreview`).
            if (_windowView.IsOpen)
            {
                return;
            }

            if (_hoveredCharacter == null)
            {
                _quickLookView.Hide();
                return;
            }

            IReadOnlyList<CharacterExamineSlotContent> slots = CharacterExamineContentBuilder.BuildSlots(_hoveredCharacter.Inventory);
            _quickLookView.Show(ResolveDisplayName(_hoveredCharacter), slots);
            if (Mouse.current != null)
            {
                _quickLookView.UpdateAnchor(Mouse.current.position.ReadValue());
            }
        }

        private void HandleWindowRequested(IExaminable examinable)
        {
            if (examinable is not CharacterExaminable character || !EnsureViews())
            {
                return;
            }

            _quickLookView.Hide();
            IReadOnlyList<CharacterExamineSlotContent> slots = CharacterExamineContentBuilder.BuildSlots(character.Inventory);
            _windowView.Show(ResolveDisplayName(character), slots);
        }

        private void HandleCloseRequested()
        {
            _windowView.Hide();

            if (_hoveredCharacter != null)
            {
                HandleExaminableChanged(_hoveredCharacter);
            }
        }

        private static string ResolveDisplayName(CharacterExaminable character)
        {
            if (CharacterExamineContentBuilder.TryGetVisibleIdentity(character.Inventory, out string name, out _))
            {
                return name;
            }

            return LocalizedTextService.GetFormattedString(
                ExamineCanonicalKeyGenerator.ExamineTableName,
                UnidentifiedKey,
                System.Array.Empty<object>(),
                UnidentifiedFallback);
        }

        private bool EnsureViews()
        {
            if (_quickLookView != null && _windowView != null)
            {
                return true;
            }

            if (!TryEnsureCatalog())
            {
                return false;
            }

            if (!SubSystems.TryGet(out UiShellSubSystem uiShell)
                || !uiShell.TryGetLayer(UiLayer.Overlay, out VisualElement overlayLayer))
            {
                Debug.LogError("CharacterExamineSubSystem could not find the UiShellSubSystem overlay layer.", this);
                return false;
            }

            // See RadialInteractionSubSystem for why this register is never paired with an unregister here.
            InputInterface.RegisterDocument(uiShell.Document);

            CharacterExamineIconSet icons = _catalog.Icons;
            _quickLookView = new CharacterQuickLookView(
                _catalog.CharacterExamineStyle, _catalog.InventorySlotStyle, _catalog.DiegeticTokensStyle, icons);
            _quickLookView.Attach(overlayLayer);

            _windowView = new CharacterExamineWindowView(
                _catalog.CharacterExamineStyle,
                _catalog.InventorySlotStyle,
                _catalog.DiegeticTokensStyle,
                _catalog.MachineWindowStyle,
                icons);
            _windowView.Attach(overlayLayer);
            _windowView.CloseRequested += HandleCloseRequested;

            return true;
        }

        private bool TryEnsureCatalog()
        {
            if (_catalog != null)
            {
                return true;
            }

            _catalog = Resources.Load<CharacterExamineAssetCatalog>(CharacterExamineAssetPaths.ResourcesCatalogName);
            if (_catalog == null)
            {
                Debug.LogError(
                    $"CharacterExamineSubSystem could not load Resources/{CharacterExamineAssetPaths.ResourcesCatalogName}. "
                    + "Run SS3D → Examine → Rebuild Character Examine Asset Catalog and commit the asset.",
                    this);
                return false;
            }

            if (!_catalog.HasRequiredAssets(out string missingField))
            {
                Debug.LogError(
                    $"CharacterExamineAssetCatalog is missing required assets ({missingField}). "
                    + "Run SS3D → Examine → Rebuild Character Examine Asset Catalog.",
                    this);
                return false;
            }

            return true;
        }
    }
}
