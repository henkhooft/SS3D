using Coimbra.Services.Events;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Comms.UI;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Events;
using SS3D.Systems.Inputs;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.Systems.Comms
{
    /// <summary>
    /// Drives the local speech subtitle overlay: subscribes to CommsSubSystem's speech events,
    /// stacks up to a few lines per speaker with fade + upward drift, and applies mode-specific
    /// visual treatments. Owns its own UIDocument, following RadialInteractionSubSystem's
    /// convention of a dedicated overlay per feature rather than a shared HUD document.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class LocalSpeechBubbleController : Actor
    {
        private const float BubbleWorldHeightOffset = 0.15f;
        private const float FadeOutTailSeconds = 1f;

        [SerializeField] private UIDocument _document;
        [SerializeField] private StyleSheet _bubbleStyleSheet;
        [SerializeField] private LocalSpeechConfig _config;

        private struct ActiveSpeech
        {
            public string Text;
            public SpeechMode Mode;
            public float StartTime;
            public float ExpiryTime;
            public int GarbleSeed;
            public AudibilityTier CurrentTier;
        }

        private readonly Dictionary<Entity, List<ActiveSpeech>> _activeSpeeches = new();
        private readonly List<Entity> _shownSpeakers = new();
        private readonly List<Entity> _staleSpeakers = new();

        private LocalSpeechBubbleView _view;
        private VisualElement _attachedRoot;
        private LocalSpeechListener _listener;
        private CrowdCapRanker _ranker;
        private Entity _localViewer;
        private CommsSubSystem _commsSubSystem;
        private float _nextRerankTime;
        private int _overflowCount;
        private bool _overlayReady;

        protected override void OnAwake()
        {
            base.OnAwake();

            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

#if UNITY_EDITOR
            EnsureEditorAssets();
#endif

            _listener = new LocalSpeechListener(_config);
            _ranker = new CrowdCapRanker();

            // Subtitles never pick (see LocalSpeechBubbleView - the whole overlay is
            // PickingMode.Ignore), so this never changes IsPointerOverInterface's answer, but
            // every runtime UIDocument owner registers per the input-arbitration convention -
            // see Documents/architecture/2026-07_input-arbitration.md.
            InputInterface.RegisterDocument(_document);

            AddHandle(LocalPlayerObjectChanged.AddListener(HandlePlayerObjectChanged));
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();

            _commsSubSystem = SubSystems.Get<CommsSubSystem>();
            if (_commsSubSystem != null)
            {
                _commsSubSystem.OnLocalSpeechReceived += HandleSpeechReceived;
            }

            EnsureOverlay();
        }

        protected override void OnDisabled()
        {
            base.OnDisabled();

            if (_commsSubSystem != null)
            {
                _commsSubSystem.OnLocalSpeechReceived -= HandleSpeechReceived;
            }

            // UIDocument destroys/rebuilds its visual tree across disable/enable. Drop the
            // cached view so EnsureOverlay re-attaches to the live root instead of driving
            // orphaned VisualElements (panel=null, NaN layout, invisible bubbles).
            TearDownOverlay();
        }

        protected override void OnDestroyed()
        {
            InputInterface.UnregisterDocument(_document);
            TearDownOverlay();
            base.OnDestroyed();
        }

        private void HandlePlayerObjectChanged(ref EventContext context, in LocalPlayerObjectChanged e)
        {
            _localViewer = e.PlayerHasObject ? e.PlayerObject.GetComponent<Entity>() : null;
        }

        private void HandleSpeechReceived(Entity speaker, SpeechEvent speechEvent)
        {
            if (speaker == null)
            {
                return;
            }

            float now = Time.time;
            float duration = _config.GetFadeDuration(speechEvent.Text.Length);

            if (!_activeSpeeches.TryGetValue(speaker, out List<ActiveSpeech> stack))
            {
                stack = new List<ActiveSpeech>(_config.MaxStackedMessagesPerSpeaker);
                _activeSpeeches[speaker] = stack;
            }

            stack.Add(new ActiveSpeech
            {
                Text = speechEvent.Text,
                Mode = speechEvent.Mode,
                StartTime = now,
                ExpiryTime = now + duration,
                GarbleSeed = speaker.GetInstanceID() ^ speechEvent.ServerTimestamp.GetHashCode(),
                CurrentTier = AudibilityTier.Inaudible,
            });

            int maxStack = Mathf.Max(1, _config.MaxStackedMessagesPerSpeaker);
            while (stack.Count > maxStack)
            {
                stack.RemoveAt(0);
            }
        }

        private void Update()
        {
            if (_localViewer == null || _activeSpeeches.Count == 0)
            {
                RenderFrame();
                return;
            }

            RemoveExpiredEntries();

            if (Time.time >= _nextRerankTime)
            {
                RecomputeVisibleSet();
                _nextRerankTime = Time.time + _config.RerankIntervalSeconds;
            }

            RenderFrame();
        }

        private void RemoveExpiredEntries()
        {
            _staleSpeakers.Clear();
            float now = Time.time;

            foreach (KeyValuePair<Entity, List<ActiveSpeech>> pair in _activeSpeeches)
            {
                if (pair.Key == null)
                {
                    _staleSpeakers.Add(pair.Key);
                    continue;
                }

                List<ActiveSpeech> stack = pair.Value;
                for (int i = stack.Count - 1; i >= 0; i--)
                {
                    if (now >= stack[i].ExpiryTime)
                    {
                        stack.RemoveAt(i);
                    }
                }

                if (stack.Count == 0)
                {
                    _staleSpeakers.Add(pair.Key);
                }
            }

            foreach (Entity speaker in _staleSpeakers)
            {
                _activeSpeeches.Remove(speaker);
            }
        }

        private void RecomputeVisibleSet()
        {
            List<Entity> audibleSpeakers = new(_activeSpeeches.Count);

            foreach (KeyValuePair<Entity, List<ActiveSpeech>> pair in _activeSpeeches)
            {
                Entity speaker = pair.Key;
                if (speaker == null)
                {
                    continue;
                }

                AudibilityTier tier = _listener.ComputeTier(_localViewer, speaker);
                List<ActiveSpeech> stack = pair.Value;

                for (int i = 0; i < stack.Count; i++)
                {
                    ActiveSpeech entry = stack[i];
                    entry.CurrentTier = tier;
                    stack[i] = entry;
                }

                if (tier != AudibilityTier.Inaudible)
                {
                    audibleSpeakers.Add(speaker);
                }
            }

            CrowdCapRanker.Result result = _ranker.Rank(_localViewer, audibleSpeakers, _config.MaxVisibleBubbles);

            _shownSpeakers.Clear();
            _shownSpeakers.AddRange(result.Shown);
            _overflowCount = result.OverflowCount;
        }

        private void RenderFrame()
        {
            if (!EnsureOverlay())
            {
                return;
            }

            Camera camera = Camera.main;
            int shownCount = 0;
            float now = Time.time;

            if (camera != null)
            {
                for (int i = 0; i < _shownSpeakers.Count; i++)
                {
                    Entity speaker = _shownSpeakers[i];
                    if (speaker == null || speaker.ViewPoint == null || !_activeSpeeches.TryGetValue(speaker, out List<ActiveSpeech> stack))
                    {
                        continue;
                    }

                    Vector3 anchor = speaker.ViewPoint.transform.position + Vector3.up * BubbleWorldHeightOffset;
                    Vector3 screenPoint = camera.WorldToScreenPoint(anchor);

                    bool onScreen = screenPoint.z > 0f
                        && screenPoint.x >= 0f && screenPoint.x <= Screen.width
                        && screenPoint.y >= 0f && screenPoint.y <= Screen.height;

                    if (!onScreen)
                    {
                        continue;
                    }

                    string speakerName = ResolveSpeakerName(speaker);

                    // Newest nearest the head; older lines stack upward with per-line drift.
                    for (int fromNewest = 0; fromNewest < stack.Count; fromNewest++)
                    {
                        int index = stack.Count - 1 - fromNewest;
                        ActiveSpeech entry = stack[index];
                        bool isNewest = fromNewest == 0;

                        string displayText = FormatDisplayText(entry, entry.CurrentTier, speakerName, isNewest);
                        float age = now - entry.StartTime;
                        float drift = age * _config.DriftPixelsPerSecond;
                        float stackLift = fromNewest * _config.StackSpacingPixels;
                        float opacity = ComputeFadeOpacity(entry.ExpiryTime) * StackOpacity(fromNewest);
                        string nameForChip = ResolveNameLabel(entry.Mode, speakerName, isNewest);

                        _view.ShowBubble(
                            shownCount,
                            screenPoint.x,
                            screenPoint.y + stackLift + drift,
                            nameForChip,
                            displayText,
                            entry.Mode,
                            entry.CurrentTier,
                            opacity,
                            fromNewest);
                        shownCount++;
                    }
                }
            }

            _view.HideBubblesFrom(shownCount);

            if (_overflowCount > 0)
            {
                _view.ShowOverflowChip(_overflowCount);
            }
            else
            {
                _view.HideOverflowChip();
            }
        }

        private static string ResolveNameLabel(SpeechMode mode, string speakerName, bool isNewest)
        {
            // Local-chat head chips only. Radio / announcements use other UI surfaces and never
            // supply a name label through this path.
            if (!isNewest || mode is SpeechMode.Whisper or SpeechMode.Emote
                or SpeechMode.Radio or SpeechMode.Announcement)
            {
                return string.Empty;
            }

            return speakerName;
        }

        private static string FormatDisplayText(ActiveSpeech entry, AudibilityTier tier, string speakerName, bool isNewest)
        {
            string body = tier == AudibilityTier.Clear
                ? entry.Text
                : TextGarbler.Garble(entry.Text, entry.GarbleSeed);

            return entry.Mode switch
            {
                // Mock 1a: name folded into the line, italic via CSS, no asterisks.
                SpeechMode.Emote => $"{speakerName} {body}",
                SpeechMode.Radio => body,
                SpeechMode.Announcement => body,
                // Whisper never quotes; speak/shout quote only the newest line.
                SpeechMode.Whisper => body,
                SpeechMode.Shout => isNewest ? $"\"{body}\"" : body,
                _ => isNewest ? $"\"{body}\"" : body,
            };
        }

        private static float StackOpacity(int fromNewest)
        {
            return fromNewest switch
            {
                0 => 1f,
                1 => 0.65f,
                _ => 0.4f,
            };
        }

        private static float ComputeFadeOpacity(float expiryTime)
        {
            float remaining = expiryTime - Time.time;
            if (remaining >= FadeOutTailSeconds)
            {
                return 1f;
            }

            return Mathf.Clamp01(remaining / FadeOutTailSeconds);
        }

        private static string ResolveSpeakerName(Entity speaker)
        {
            Mind mind = speaker.Mind;
            if (mind != null && mind != Mind.Empty && mind.player != null && !string.IsNullOrEmpty(mind.player.Ckey))
            {
                return mind.player.Ckey;
            }

            return "Someone";
        }

        private bool EnsureOverlay()
        {
            if (_document == null)
            {
                return false;
            }

            VisualElement root = _document.rootVisualElement;

            // rootVisualElement can exist before the runtime panel is ready, and UIDocument
            // replaces the root across disable/enable. Only treat the overlay as ready when we
            // are attached to the *current* rooted panel.
            if (root == null || root.panel == null)
            {
                return false;
            }

            if (_overlayReady && _view != null && _attachedRoot == root)
            {
                return true;
            }

            _view?.Detach();
            _view = new LocalSpeechBubbleView(_bubbleStyleSheet);
            _view.Attach(root);
            _attachedRoot = root;
            _overlayReady = true;
            return true;
        }

        private void TearDownOverlay()
        {
            _view?.Detach();
            _view = null;
            _attachedRoot = null;
            _overlayReady = false;
        }

#if UNITY_EDITOR
        private void EnsureEditorAssets()
        {
            if (_bubbleStyleSheet == null)
            {
                _bubbleStyleSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Assets/Content/Systems/UI/Comms/LocalSpeechBubbles/LocalSpeechBubble.uss");
            }

            if (_config == null)
            {
                _config = UnityEditor.AssetDatabase.LoadAssetAtPath<LocalSpeechConfig>(
                    "Assets/Content/Systems/UI/Comms/LocalSpeechBubbles/LocalSpeechConfig.asset");
            }

            if (_document != null && _document.panelSettings == null)
            {
                _document.panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(
                    "Assets/Content/Systems/UI/Comms/LocalSpeechBubbles/CommsOverlayPanelSettings.asset");
            }
        }
#endif
    }
}
