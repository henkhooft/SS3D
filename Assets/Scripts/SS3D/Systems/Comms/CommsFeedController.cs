using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Comms.UI;
using SS3D.Systems.Inputs;
using SS3D.UI.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.Systems.Comms
{
    /// <summary>
    /// Drives the non-diegetic radio feed and announcement banner on the UiShell HUD layer.
    /// </summary>
    public sealed class CommsFeedController : SubSystem
    {
        private const string StyleSheetPath = "Assets/Content/Systems/UI/Comms/Feed/CommsFeed.uss";

        [SerializeField] private StyleSheet _styleSheet;

        private CommsFeedView _view;
        private CommsSubSystem _comms;
        private bool _attached;

        protected override void OnAwake()
        {
            base.OnAwake();

#if UNITY_EDITOR
            EnsureEditorAssets();
#endif
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();

            _comms = SubSystems.Get<CommsSubSystem>();
            if (_comms != null)
            {
                _comms.OnCommsMessageReceived += HandleCommsMessage;
            }
        }

        protected override void OnDisabled()
        {
            if (_comms != null)
            {
                _comms.OnCommsMessageReceived -= HandleCommsMessage;
                _comms = null;
            }

            DetachView();
            base.OnDisabled();
        }

        private void Update()
        {
            if (!EnsureView())
            {
                return;
            }

            _view.Tick();
        }

        private void HandleCommsMessage(CommsMessage message)
        {
            if (!EnsureView())
            {
                return;
            }

            CommsChannel channel = null;
            _comms?.TryGetChannel(message.ChannelId, out channel);

            if (message.Kind == CommsChannelKind.Announcement
                || (channel != null && channel.Kind == CommsChannelKind.Announcement))
            {
                string title = channel != null ? channel.ResolveAnnouncementTitle() : "ALL-STATION";
                _view.ShowAnnouncement(title, message.Text);
                return;
            }

            if (message.Kind != CommsChannelKind.Radio)
            {
                return;
            }

            string header = channel != null
                ? channel.ResolveRadioHeader()
                : (message.ChannelId ?? "RADIO").ToUpperInvariant();
            Color accent = channel != null ? channel.Color : new Color(0.37f, 0.53f, 0.7f);
            _view.PushRadio(header, message.Sender, message.Text, accent);
        }

        private bool EnsureView()
        {
            if (_attached && _view != null)
            {
                return true;
            }

            if (!SubSystems.TryGet(out UiShellSubSystem uiShell)
                || !uiShell.TryGetLayer(UiLayer.Hud, out VisualElement layerRoot))
            {
                return false;
            }

            InputInterface.RegisterDocument(uiShell.Document);

            _view = new CommsFeedView(_styleSheet);
            _view.Attach(layerRoot);
            _attached = true;
            return true;
        }

        private void DetachView()
        {
            _view?.Detach();
            _view = null;
            _attached = false;
        }

#if UNITY_EDITOR
        private void EnsureEditorAssets()
        {
            if (_styleSheet == null)
            {
                _styleSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            }
        }
#endif
    }
}
