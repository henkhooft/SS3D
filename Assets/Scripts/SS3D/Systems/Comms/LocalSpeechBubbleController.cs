using Coimbra.Services.Events;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Comms.UI;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Events;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.Systems.Comms
{
    /// <summary>
    /// Drives the local speech bubble overlay: subscribes to CommsSubSystem's speech events,
    /// computes each active speaker's viewer-relative audibility tier and crowd-cap visibility,
    /// and positions/fades the bubble UI. Owns its own UIDocument, following
    /// RadialInteractionSubSystem's convention of a dedicated overlay per feature rather than a
    /// shared HUD document (none exists yet in this codebase).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class LocalSpeechBubbleController : Actor
    {
        private const float BubbleWorldHeightOffset = 0.4f;
        private const float FadeOutTailSeconds = 0.6f;

        [SerializeField] private UIDocument _document;
        [SerializeField] private StyleSheet _bubbleStyleSheet;
        [SerializeField] private LocalSpeechConfig _config;

        private struct ActiveSpeech
        {
            public string Text;
            public SpeechMode Mode;
            public float ExpiryTime;
            public int GarbleSeed;
            public AudibilityTier CurrentTier;
        }

        private readonly Dictionary<Entity, ActiveSpeech> _activeSpeeches = new();
        private readonly List<Entity> _shownSpeakers = new();
        private readonly List<Entity> _staleSpeakers = new();

        private LocalSpeechBubbleView _view;
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
        }

        protected override void OnDestroyed()
        {
            _view?.Detach();
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

            float duration = _config.GetFadeDuration(speechEvent.Text.Length);

            _activeSpeeches[speaker] = new ActiveSpeech
            {
                Text = speechEvent.Text,
                Mode = speechEvent.Mode,
                ExpiryTime = Time.time + duration,
                GarbleSeed = speaker.GetInstanceID() ^ speechEvent.ServerTimestamp.GetHashCode(),
                CurrentTier = AudibilityTier.Inaudible,
            };
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

            foreach (KeyValuePair<Entity, ActiveSpeech> pair in _activeSpeeches)
            {
                if (pair.Key == null || Time.time >= pair.Value.ExpiryTime)
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

            foreach (Entity speaker in new List<Entity>(_activeSpeeches.Keys))
            {
                AudibilityTier tier = _listener.ComputeTier(_localViewer, speaker);

                ActiveSpeech entry = _activeSpeeches[speaker];
                entry.CurrentTier = tier;
                _activeSpeeches[speaker] = entry;

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

            if (camera != null)
            {
                for (int i = 0; i < _shownSpeakers.Count; i++)
                {
                    Entity speaker = _shownSpeakers[i];
                    if (speaker == null || speaker.ViewPoint == null || !_activeSpeeches.TryGetValue(speaker, out ActiveSpeech entry))
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

                    string displayText = entry.CurrentTier == AudibilityTier.Clear
                        ? entry.Text
                        : TextGarbler.Garble(entry.Text, entry.GarbleSeed);

                    float opacity = ComputeFadeOpacity(entry.ExpiryTime);

                    _view.ShowBubble(shownCount, screenPoint.x, screenPoint.y, displayText, entry.CurrentTier, opacity);
                    shownCount++;
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

        private static float ComputeFadeOpacity(float expiryTime)
        {
            float remaining = expiryTime - Time.time;
            if (remaining >= FadeOutTailSeconds)
            {
                return 1f;
            }

            return Mathf.Clamp01(remaining / FadeOutTailSeconds);
        }

        private bool EnsureOverlay()
        {
            if (_overlayReady && _view != null)
            {
                return true;
            }

            if (_document == null)
            {
                return false;
            }

            VisualElement root = _document.rootVisualElement;
            if (root == null)
            {
                return false;
            }

            _view?.Detach();
            _view = new LocalSpeechBubbleView(_bubbleStyleSheet);
            _view.Attach(root);
            _overlayReady = true;
            return true;
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
