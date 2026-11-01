using NUnit.Framework;
using SS3D.Systems.Rounds;
using SS3D.UI.Lobby;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEngine;

namespace SS3D.Tests.EditMode
{
    public sealed class LobbyMapStopPromptTests
    {
        [Test]
        public void PromptPreservesDraftOnRefreshAndSendsExplicitChoice()
        {
            LobbyShellView view = new(null);
            EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>();
            window.Show();
            VisualElement parent = window.rootVisualElement;
            try
            {
                view.Attach(parent);
                view.SetMapStopPrompt(7, "", "");
                TextField name = parent.Q<TextField>("map-stop-name");
                name.value = "My station";
                view.SetMapStopPrompt(7, "", "Choose a name");
                Assert.AreEqual("My station", name.value);
                int submitted = 0;
                view.MapStopResolved += (token, choice, mapName) =>
                {
                    Assert.AreEqual(7, token);
                    Assert.AreEqual(MapStopChoice.SaveAndStop, choice);
                    Assert.AreEqual("My station", mapName);
                    submitted++;
                };
                using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = parent.Q<Button>("map-stop-save");
                    parent.Q<Button>("map-stop-save").SendEvent(submit);
                }
                Assert.AreEqual(1, submitted);
                view.SetMapStopPrompt(0, "", "");
                Assert.AreEqual(DisplayStyle.None, parent.Q("map-stop-prompt").style.display.value);
            }
            finally { view.Detach(); window.Close(); }
        }
    }
}
