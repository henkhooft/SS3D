#if UNITY_EDITOR
using System.IO;
using SS3D.UI.Shell.BugReport;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.Editor
{
    /// <summary>Isolated visual fixture. All sample values and interactions are editor-only.</summary>
    public sealed class BugReportPreviewWindow : EditorWindow
    {
        public enum PreviewState { InRound, MapEditor, Saved, RateLimited, SuccessToast, MetadataExpanded, ScreenshotRemoved }
        private VisualElement _stage;
        private VisualElement _view;
        private Texture2D _frame;
        private PreviewState _state;

        [MenuItem("SS3D/UI/Preview Bug Report")]
        public static void OpenWindow()
        {
            BugReportPreviewWindow window = GetWindow<BugReportPreviewWindow>();
            window.titleContent = new GUIContent("Bug Report · Visual Preview");
            window.minSize = new Vector2(600, 480);
            window.ShowUtility();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            Toolbar toolbar = new();
            EnumField state = new(_state) { name = "preview-state" };
            state.RegisterValueChangedCallback(e => ShowState((PreviewState)e.newValue));
            toolbar.Add(state);
            toolbar.Add(new Label("Visual fixture · sample data · actions are inert"));
            rootVisualElement.Add(toolbar);
            _stage = new VisualElement { name = "preview-stage" };
            _stage.style.flexGrow = 1;
            _stage.style.backgroundColor = new Color(0.086f, 0.086f, 0.086f);
            rootVisualElement.Add(_stage);
            string fixture = Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).FullName,
                "Testing/ui/fixtures/bug-report-frame.png");
            if (File.Exists(fixture))
            {
                if (_frame != null) DestroyImmediate(_frame);
                _frame = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                _frame.LoadImage(File.ReadAllBytes(fixture));
                _stage.style.backgroundImage = _frame;
            }
            ShowState(_state);
        }

        public void ShowState(PreviewState state)
        {
            _state = state;
            rootVisualElement.Q<EnumField>("preview-state")?.SetValueWithoutNotify(state);
            _stage.Clear();
            BugReportAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<BugReportAssetCatalog>(BugReportAssetPaths.Catalog);
            if (catalog == null || !catalog.HasRequiredAssets(out _))
            {
                _stage.Add(new Label("Rebuild All UI Catalogs to load the bug report visuals."));
                return;
            }
            _view = catalog.Template.CloneTree();
            _view.style.flexGrow = 1;
            _view.styleSheets.Add(catalog.Ss3dTokensStyle);
            _view.styleSheets.Add(catalog.Ss3dTypographyStyle);
            _view.styleSheets.Add(catalog.Style);
            _stage.Add(_view);
            bool map = state == PreviewState.MapEditor;
            bool saved = state == PreviewState.Saved;
            bool toast = state == PreviewState.SuccessToast;
            bool rate = state == PreviewState.RateLimited;
            Visible("window", !toast);
            Visible("scrim", !toast);
            Visible("success-toast", toast);
            Visible("form", !saved);
            Visible("saved", saved);
            Visible("include-map", map);
            Visible("rate-limit", rate);
            Label badge = _view.Q<Label>("context-badge");
            badge.text = "IN ROUND · ALIVE";
            if (saved) badge.text = "Saved";
            else if (map) badge.text = "MAP EDITOR · AREA TOOL";
            else if (rate) badge.text = "GHOST";
            badge.EnableInClassList("bug-report__badge--saved", saved);
            TextField summary = _view.Q<TextField>("summary");
            summary.textEdition.placeholder = "Wrench clips through the floor when dropped";
            _view.Q<TextField>("details").textEdition.placeholder = "What were you doing, step by step? What did you expect?";
            string sampleSummary = "";
            if (map) sampleSummary = "Area tool leaves ghost tiles after undo";
            else if (rate) sampleSummary = "Ghost follow camera snaps to map origin";
            summary.SetValueWithoutNotify(sampleSummary);
            Button submit = _view.Q<Button>("submit");
            submit.text = map ? "SAVE REPORT" : "SEND REPORT";
            void RefreshSummary()
            {
                _view.Q<Label>("summary-count").text = $"{summary.value.Length}/80";
                submit.SetEnabled(!rate && !string.IsNullOrWhiteSpace(summary.value));
            }
            summary.RegisterValueChangedCallback(_ => RefreshSummary());
            RefreshSummary();
            foreach (string severity in new[] { "minor", "major", "blocker" })
            {
                string selected = severity;
                _view.Q<Button>(severity).clicked += () => SelectSeverity(selected);
            }
            if (map) SelectSeverity("major");
            SetScreenshot(state != PreviewState.ScreenshotRemoved);
            _view.Q<Button>("remove-screenshot").clicked += () =>
                SetScreenshot(_view.Q<Label>("no-screenshot").style.display == DisplayStyle.Flex);
            PopulateMetadata(map);
            SetMetadata(state == PreviewState.MetadataExpanded);
            _view.Q<Button>("metadata-toggle").clicked += () =>
                SetMetadata(_view.Q<VisualElement>("metadata-fields").style.display == DisplayStyle.None);
        }

        private void Visible(string name, bool visible)
        {
            _view.Q(name).style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SelectSeverity(string selected)
        {
            foreach (string severity in new[] { "minor", "major", "blocker" })
            {
                Button button = _view.Q<Button>(severity);
                button.EnableInClassList("bug-report__button--blue", severity == selected && severity == "minor");
                button.EnableInClassList("bug-report__button--amber", severity == selected && severity == "major");
                button.EnableInClassList("bug-report__button--danger", severity == selected && severity == "blocker");
            }
        }

        private void SetScreenshot(bool included)
        {
            _view.Q("screenshot").style.backgroundImage = included && _frame != null ? new StyleBackground(_frame) : StyleKeyword.None;
            Visible("no-screenshot", !included);
            _view.Q<Label>("screenshot-title").text = included ? "Screenshot · taken when you pressed F8" : "Screenshot removed";
            _view.Q<Label>("screenshot-file").text = included ? "frame_001432.png · 1920×1080" : "Not included in the report";
            _view.Q<Button>("remove-screenshot").text = included ? "REMOVE" : "ADD BACK";
        }

        private void PopulateMetadata(bool map)
        {
            string[,] fields = {
                { "Game version", "0.0.9-prealpha · a3f9c1e" }, { "Round", "#1187 · 00:14:32" },
                { "Map", map ? "ministation · unsaved edits: yes" : "Box Station · unsaved edits: no" },
                { "Context", map ? "Map editor · area tool" : "In round · alive" },
                { "Position", map ? "(12.0, 0.0, 4.5)" : "Engineering / Main Hall (142.6, 0.0, -38.2)" },
                { "Network role", map ? "Own host" : "Client" }, { "Players", map ? "1" : "24" },
                { "FPS", map ? "144" : "58" }, { "OS / GPU", "Windows 11 · RTX 3060" },
                { "Player name", "Wrenchman" }, { map ? "Log files" : "Recent log", map ? "Included in the bundle" : "42 lines" }
            };
            VisualElement parent = _view.Q("metadata-fields");
            for (int i = 0; i < fields.GetLength(0); i++)
            {
                VisualElement row = new();
                row.AddToClassList("bug-report__meta-row");
                Label key = new(fields[i, 0]);
                key.AddToClassList("bug-report__mono");
                key.AddToClassList("bug-report__meta-key");
                Label value = new(fields[i, 1]);
                value.AddToClassList("bug-report__mono");
                value.AddToClassList("bug-report__meta-value");
                row.Add(key);
                row.Add(value);
                parent.Add(row);
            }
        }

        private void SetMetadata(bool expanded)
        {
            Visible("metadata-fields", expanded);
            _view.Q<Label>("metadata-caption").text = expanded ? "HIDE −" : "11 FIELDS · SHOW +";
        }

        private void OnDisable()
        {
            if (_frame != null) DestroyImmediate(_frame);
        }
    }
}
#endif
