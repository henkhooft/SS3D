using NUnit.Framework;
using SS3D.Systems.Rounds;
using SS3D.UI.Lobby;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEngine;

namespace SS3D.Tests.EditMode
{
    public sealed class LobbyMapStartPromptTests
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
                view.SetMapStartPrompt(7, "ministation", "", "");
                TextField name = parent.Q<TextField>("map-start-name");
                name.value = "My station";
                view.SetMapStartPrompt(7, "ministation", "", "Choose a name");
                Assert.AreEqual("My station", name.value);
                int submitted = 0;
                view.MapStartResolved += (token, choice, mapName) =>
                {
                    Assert.AreEqual(7, token);
                    Assert.AreEqual(MapStartChoice.SaveAndStart, choice);
                    Assert.AreEqual("My station", mapName);
                    submitted++;
                };
                using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = parent.Q<Button>("map-start-save");
                    parent.Q<Button>("map-start-save").SendEvent(submit);
                }
                Assert.AreEqual(1, submitted);
                view.SetMapStartPrompt(0, "", "", "");
                Assert.AreEqual(DisplayStyle.None, parent.Q("map-start-prompt").style.display.value);
            }
            finally { view.Detach(); window.Close(); }
        }
    }
}
