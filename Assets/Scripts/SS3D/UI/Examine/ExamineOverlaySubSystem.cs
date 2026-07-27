using System.Collections.Generic;
using System.Text;
using Coimbra.Services.Events;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Localization;
using SS3D.Systems.Entities.Events;
using SS3D.Systems.Examine;
using SS3D.Systems.Inputs;
using SS3D.Systems.Interactions;
using SS3D.Systems.Inventory.Containers;
using SS3D.UI.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SS3D.UI.Examine
{
    /// <summary>
    /// Owns every examine hover/detail UI surface: the generic hover-name/detailed-text/detailed-image
    /// view (<see cref="GenericExamineHoverView"/>, replacing the condemned uGUI <c>ExamineUI</c>) for
    /// ordinary examinables, and the character-examine hover preview / persistent window
    /// (<see cref="CharacterQuickLookView"/> / <see cref="CharacterExamineWindowView"/>) for characters
    /// — discriminated via <see cref="ExamineData.Type"/> == <see cref="ExamineType.CHARACTER"/>, not a
    /// dedicated marker component (<c>Human.prefab</c>'s existing <c>Selectable</c>/<c>SimpleExaminable</c>
    /// already make characters examinable). Self-bootstraps like <see cref="UiShellSubSystem"/> so
    /// adopting it needs no Boot/Game scene edit.
    /// </summary>
    public sealed class ExamineOverlaySubSystem : SubSystem
    {
        private const string UnidentifiedKey = "character_examine.unidentified_crew_member";
        private const string UnidentifiedFallback = "Unidentified Crew Member";

        private readonly ExamineContentResolver _contentResolver = new();

        private ExamineSubSystem _examineSystem;
        private InputSubSystem _inputSystem;
        private ExamineOverlayAssetCatalog _catalog;
        private GenericExamineHoverView _genericView;
        private CharacterQuickLookView _quickLookView;
        private CharacterExamineWindowView _windowView;

        private IExaminable _currentExaminable;
        private HumanInventory _hoveredCharacterInventory;
        private HumanInventory _windowVictimInventory;
        private InteractionController _interactionController;
        private GameObject _localPlayer;
        private bool _pinnedDetailedExamine;
        private bool _wasDetailedExamineHeld;
        private bool _takeInProgress;

        private IExaminable _cachedExaminable;
        private ExamineContent _cachedContent;
        private bool _hasCachedContent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SubSystems.TryGet(out ExamineOverlaySubSystem _))
            {
                return;
            }

            GameObject host = new(nameof(ExamineOverlaySubSystem));
            Object.DontDestroyOnLoad(host);
            host.AddComponent<ExamineOverlaySubSystem>();
        }

        protected override void OnStart()
        {
            base.OnStart();
            AddHandle(LocalPlayerObjectChanged.AddListener(HandleLocalPlayerObjectChanged));
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();

            LocalizedTextService.EnsureInitialized();
            LocalizedTextService.LocaleChanged += HandleLocaleChanged;
            _inputSystem = SubSystems.Get<InputSubSystem>();

            if (TryResolveExamineSystem())
            {
                _examineSystem.OnExaminableChanged += HandleExaminableChanged;
                _examineSystem.OnDetailedExamineRequested += HandleDetailedExamineRequested;
                _examineSystem.OnCharacterWindowRequested += HandleWindowRequested;
            }
        }

        protected override void OnDisabled()
        {
            base.OnDisabled();

            LocalizedTextService.LocaleChanged -= HandleLocaleChanged;
            UnsubscribeInteractionController();

            if (_examineSystem != null)
            {
                _examineSystem.OnExaminableChanged -= HandleExaminableChanged;
                _examineSystem.OnDetailedExamineRequested -= HandleDetailedExamineRequested;
                _examineSystem.OnCharacterWindowRequested -= HandleWindowRequested;
            }
        }

        protected override void OnDestroyed()
        {
            UnsubscribeWindowView();
            UnsubscribeInteractionController();
            _genericView?.Detach();
            _quickLookView?.Detach();
            _windowView?.Detach();
            _genericView = null;
            _quickLookView = null;
            _windowView = null;
            base.OnDestroyed();
        }

        private void Update()
        {
            bool detailedHeld = IsDetailedExamineHeld();
            if (detailedHeld != _wasDetailedExamineHeld)
            {
                _wasDetailedExamineHeld = detailedHeld;
                RefreshGenericDisplay();
            }
            else if (detailedHeld)
            {
                RefreshGenericDisplay();
            }

            if (_windowView != null && _windowView.IsOpen)
            {
                _windowView.Tick(Time.deltaTime);
            }

            if (_takeInProgress
                && (_interactionController == null || !_interactionController.HasActiveDelayedInteraction))
            {
                HandleTakeFromCharacterEnded();
            }

            if (Mouse.current == null)
            {
                return;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            _genericView?.UpdateAnchor(mousePosition);
            if (_windowView == null || !_windowView.IsOpen)
            {
                _quickLookView?.UpdateAnchor(mousePosition);
            }
        }

        private void HandleLocalPlayerObjectChanged(ref EventContext context, in LocalPlayerObjectChanged e)
        {
            UnsubscribeInteractionController();
            _localPlayer = e.PlayerHasObject ? e.PlayerObject : null;
            TryBindInteractionController();
        }

        private void HandleLocaleChanged()
        {
            InvalidateContentCache();
            RefreshGenericDisplay();
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
            if (_pinnedDetailedExamine && examinable != _currentExaminable && examinable != null)
            {
                _pinnedDetailedExamine = false;
            }

            _currentExaminable = examinable;
            _wasDetailedExamineHeld = IsDetailedExamineHeld();
            _hoveredCharacterInventory = ResolveCharacterInventory(examinable);

            if (!EnsureViews())
            {
                return;
            }

            if (_windowView.IsOpen)
            {
                // The full window pins its own target; hover changes elsewhere don't affect either
                // preview while it's open (mirrors the design mock's `if (!fullOpen) showPreview`).
                return;
            }

            if (_hoveredCharacterInventory != null)
            {
                _genericView.Hide();
                ShowQuickLook(_hoveredCharacterInventory);
                return;
            }

            _quickLookView.Hide();
            RefreshGenericDisplay();
        }

        private void HandleDetailedExamineRequested(IExaminable examinable)
        {
            _pinnedDetailedExamine = true;
            HandleExaminableChanged(examinable);
        }

        private void HandleWindowRequested(IExaminable examinable)
        {
            HumanInventory inventory = ResolveCharacterInventory(examinable);
            if (inventory == null || !EnsureViews())
            {
                return;
            }

            CancelActiveTake();
            _windowVictimInventory = inventory;
            TryBindInteractionController();
            _quickLookView.Hide();
            _genericView.Hide();
            IReadOnlyList<CharacterExamineSlotContent> slots = CharacterExamineContentBuilder.BuildSlots(inventory);
            bool takeAllowed = CharacterLootUtility.IsLootable(inventory);
            _windowView.Show(ResolveDisplayName(inventory), slots, takeAllowed);
        }

        private void HandleCloseRequested()
        {
            CancelActiveTake();
            _windowVictimInventory = null;
            _windowView.Hide();
            HandleExaminableChanged(_currentExaminable);
        }

        private void HandleTakeHoldStarted(CharacterExamineSlot slot)
        {
            if (_windowVictimInventory == null || !CharacterLootUtility.IsLootable(_windowVictimInventory))
            {
                return;
            }

            if (!TryBindInteractionController())
            {
                return;
            }

            _interactionController.RequestTakeFromCharacter(_windowVictimInventory, slot);
        }

        private void HandleTakeHoldCancelled()
        {
            CancelActiveTake();
        }

        private void HandleTakeFromCharacterStarted(CharacterExamineSlot slot, float delaySeconds)
        {
            if (_windowView == null || !_windowView.IsOpen)
            {
                return;
            }

            _takeInProgress = true;
            _windowView.BeginTakeProgress(slot, delaySeconds);
        }

        private void HandleTakeFromCharacterEnded()
        {
            bool wasTaking = _takeInProgress;
            _takeInProgress = false;
            _windowView?.ClearTakeProgress();

            if (wasTaking || (_windowView != null && _windowView.IsOpen))
            {
                RefreshWindowSlots();
            }
        }

        private void CancelActiveTake()
        {
            if (_interactionController != null && _interactionController.HasActiveDelayedInteraction)
            {
                _interactionController.CancelActiveDelayedInteraction();
            }

            _takeInProgress = false;
            _windowView?.ClearTakeProgress();
        }

        private void RefreshWindowSlots()
        {
            if (_windowView == null || !_windowView.IsOpen || _windowVictimInventory == null)
            {
                return;
            }

            IReadOnlyList<CharacterExamineSlotContent> slots =
                CharacterExamineContentBuilder.BuildSlots(_windowVictimInventory);
            bool takeAllowed = CharacterLootUtility.IsLootable(_windowVictimInventory);
            _windowView.TakeAllowed = takeAllowed;
            _windowView.RefreshSlots(ResolveDisplayName(_windowVictimInventory), slots);
        }

        private bool TryBindInteractionController()
        {
            if (_interactionController != null)
            {
                return true;
            }

            if (_localPlayer == null)
            {
                return false;
            }

            _interactionController = _localPlayer.GetComponent<InteractionController>()
                ?? _localPlayer.GetComponentInChildren<InteractionController>();
            if (_interactionController == null)
            {
                return false;
            }

            _interactionController.TakeFromCharacterStarted += HandleTakeFromCharacterStarted;
            _interactionController.TakeFromCharacterEnded += HandleTakeFromCharacterEnded;
            return true;
        }

        private void UnsubscribeInteractionController()
        {
            if (_interactionController == null)
            {
                return;
            }

            _interactionController.TakeFromCharacterStarted -= HandleTakeFromCharacterStarted;
            _interactionController.TakeFromCharacterEnded -= HandleTakeFromCharacterEnded;
            _interactionController = null;
        }

        private void UnsubscribeWindowView()
        {
            if (_windowView == null)
            {
                return;
            }

            _windowView.CloseRequested -= HandleCloseRequested;
            _windowView.TakeHoldStarted -= HandleTakeHoldStarted;
            _windowView.TakeHoldCancelled -= HandleTakeHoldCancelled;
        }

        private static HumanInventory ResolveCharacterInventory(IExaminable examinable)
        {
            if (examinable?.GetData()?.Type != ExamineType.CHARACTER || examinable is not Component component)
            {
                return null;
            }

            return component.GetComponent<HumanInventory>();
        }

        private void ShowQuickLook(HumanInventory inventory)
        {
            IReadOnlyList<CharacterExamineSlotContent> slots = CharacterExamineContentBuilder.BuildSlots(inventory);
            _quickLookView.Show(ResolveDisplayName(inventory), slots);
        }

        private static string ResolveDisplayName(HumanInventory inventory)
        {
            if (CharacterExamineContentBuilder.TryGetVisibleIdentity(inventory, out string name, out _))
            {
                return name;
            }

            return LocalizedTextService.GetFormattedString(
                ExamineCanonicalKeyGenerator.ExamineTableName,
                UnidentifiedKey,
                System.Array.Empty<object>(),
                UnidentifiedFallback);
        }

        private void RefreshGenericDisplay()
        {
            if (_genericView == null)
            {
                return;
            }

            if (_hoveredCharacterInventory != null || _currentExaminable?.GetData() == null)
            {
                _genericView.Hide();
                InvalidateContentCache();
                return;
            }

            ExamineContent content = GetCachedContent(_currentExaminable);

            if (IsDetailedExamineHeld())
            {
                ExamineData data = _currentExaminable.GetData();
                if (data.Type == ExamineType.SIMPLE_IMAGE
                    && IsWithinDetailedImageRange(_currentExaminable, data)
                    && TryGetImageDetailedContent(_currentExaminable, content, out Sprite image, out string caption, out Vector2 imageSize))
                {
                    _genericView.ShowDetailedImage(image, caption, imageSize);
                    return;
                }

                if (content.HasDescription || content.Sections.Count > 0)
                {
                    _genericView.ShowDetailedText(content.Name, BuildDetailedText(content));
                    return;
                }
            }

            _genericView.ShowHoverName(content.Name);
        }

        private ExamineContent GetCachedContent(IExaminable examinable)
        {
            if (_hasCachedContent && _cachedExaminable == examinable)
            {
                return _cachedContent;
            }

            _cachedContent = _contentResolver.Resolve(examinable);
            _cachedExaminable = examinable;
            _hasCachedContent = true;
            return _cachedContent;
        }

        private void InvalidateContentCache()
        {
            _hasCachedContent = false;
            _cachedExaminable = null;
            _cachedContent = ExamineContent.Empty;
        }

        private bool TryGetImageDetailedContent(
            IExaminable examinable,
            ExamineContent content,
            out Sprite image,
            out string caption,
            out Vector2 imageSize)
        {
            image = null;
            caption = string.Empty;
            imageSize = Vector2.zero;

            ExamineData data = examinable?.GetData();
            if (data == null || data.Type != ExamineType.SIMPLE_IMAGE || examinable is not IImageExaminable imageExaminable)
            {
                return false;
            }

            image = imageExaminable.GetDetailedImage();
            if (image == null)
            {
                return false;
            }

            imageSize = data.DetailedImageSize;
            caption = content.Description;
            return true;
        }

        private bool IsWithinDetailedImageRange(IExaminable examinable, ExamineData data)
        {
            if (!TryGetPlayerExamineOrigin(out Vector3 origin))
            {
                return false;
            }

            return ExamineRangeUtility.IsWithinRange(examinable, origin, data.DetailedImageRange);
        }

        private bool TryGetPlayerExamineOrigin(out Vector3 origin)
        {
            origin = default;

            if (_localPlayer == null)
            {
                return false;
            }

            Hands hands = _localPlayer.GetComponentInChildren<Hands>();
            Hand hand = hands?.SelectedHand;
            if (hand == null)
            {
                return false;
            }

            origin = hand.InteractionOrigin;
            return true;
        }

        private bool IsDetailedExamineHeld()
        {
            if (_pinnedDetailedExamine)
            {
                return true;
            }

            return _inputSystem != null && _inputSystem.DetailedExamine.IsPressed();
        }

        private static string BuildDetailedText(ExamineContent content)
        {
            if (content.Sections == null || content.Sections.Count == 0)
            {
                return content.Description;
            }

            StringBuilder builder = new(content.Description);
            foreach (ExamineSection section in content.Sections)
            {
                if (string.IsNullOrWhiteSpace(section.Text))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("\n\n");
                }

                builder.Append(section.Text);
            }

            return builder.ToString();
        }

        private bool EnsureViews()
        {
            if (_genericView != null && _quickLookView != null && _windowView != null)
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
                Debug.LogError("ExamineOverlaySubSystem could not find the UiShellSubSystem overlay layer.", this);
                return false;
            }

            // See RadialInteractionSubSystem for why this register is never paired with an unregister here.
            InputInterface.RegisterDocument(uiShell.Document);

            _genericView = new GenericExamineHoverView(_catalog.ExamineStyle);
            _genericView.Attach(overlayLayer);

            CharacterExamineIconSet icons = _catalog.Icons;
            _quickLookView = new CharacterQuickLookView(
                _catalog.ExamineStyle, _catalog.InventorySlotStyle, _catalog.DiegeticTokensStyle, icons);
            _quickLookView.Attach(overlayLayer);

            _windowView = new CharacterExamineWindowView(
                _catalog.ExamineStyle,
                _catalog.InventorySlotStyle,
                _catalog.DiegeticTokensStyle,
                _catalog.MachineWindowStyle,
                icons);
            _windowView.Attach(overlayLayer);
            _windowView.CloseRequested += HandleCloseRequested;
            _windowView.TakeHoldStarted += HandleTakeHoldStarted;
            _windowView.TakeHoldCancelled += HandleTakeHoldCancelled;

            return true;
        }

        private bool TryEnsureCatalog()
        {
            if (_catalog != null)
            {
                return true;
            }

            _catalog = Resources.Load<ExamineOverlayAssetCatalog>(ExamineOverlayAssetPaths.ResourcesCatalogName);
            if (_catalog == null)
            {
                Debug.LogError(
                    $"ExamineOverlaySubSystem could not load Resources/{ExamineOverlayAssetPaths.ResourcesCatalogName}. "
                    + "Run SS3D → Examine → Rebuild Examine Asset Catalog and commit the asset.",
                    this);
                return false;
            }

            if (!_catalog.HasRequiredAssets(out string missingField))
            {
                Debug.LogError(
                    $"ExamineOverlayAssetCatalog is missing required assets ({missingField}). "
                    + "Run SS3D → Examine → Rebuild Examine Asset Catalog.",
                    this);
                return false;
            }

            return true;
        }
    }
}
