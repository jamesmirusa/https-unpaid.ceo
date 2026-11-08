using NUnit.Framework;
using UnityEngine;
using UnpaidCeo.Game.Narrative;
using Yarn.Unity;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>The dialogue presenter announces a choice so the hero can play its think clip.</summary>
    public class DialogueOptionsTests
    {
        [Test]
        public void Presenter_announces_when_options_are_shown()
        {
            var go = new GameObject("presenter");
            try
            {
                var presenter = go.AddComponent<UitkDialoguePresenter>();
                int shown = 0;
                presenter.OptionsShown = () => shown++;
                presenter.RunOptionsAsync(new DialogueOption[0], default(LineCancellationToken));
                Assert.AreEqual(1, shown);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
