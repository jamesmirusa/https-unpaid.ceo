using NUnit.Framework;
using UnityEngine;
using UnpaidCeo.Game.Ui.Kit;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Stage 6: the interface keeps out of the notch, the Dynamic Island and the home bar; the web is unaffected.</summary>
    public class SafeAreaTests
    {
        [Test]
        public void An_iPhone_with_a_Dynamic_Island_pushes_the_hud_down_and_the_dock_up()
        {
            // iPhone 15/16/17/18 Pro: 1179×2556 px, safe area y 102 … 2397 (bottom 102, top 159 in pixels from the edges)
            var screen = new Vector2(1179, 2556);
            var safe = new Rect(0, 102, 1179, 2556 - 102 - 177);
            var panel = new Vector2(1080, 2341.4f);   // ScaleWithScreenSize, Expand, reference 1080×1920
            var i = SafeArea.Insets(safe, screen, panel);
            float scale = panel.y / screen.y;
            Assert.AreEqual(177 * scale, i.top, 0.5f);
            Assert.AreEqual(102 * scale, i.bottom, 0.5f);
            Assert.AreEqual(0f, i.left, 0.01f);
            Assert.AreEqual(0f, i.right, 0.01f);
        }

        [Test]
        public void A_full_screen_safe_area_adds_nothing()
        {
            var i = SafeArea.Insets(new Rect(0, 0, 1920, 1080), new Vector2(1920, 1080), new Vector2(1920, 1080));
            Assert.AreEqual(0f, i.top + i.bottom + i.left + i.right, 0.01f);
        }

        [Test]
        public void Landscape_notch_insets_the_side()
        {
            var i = SafeArea.Insets(new Rect(132, 63, 2556 - 264, 1179 - 63), new Vector2(2556, 1179), new Vector2(2556, 1179));
            Assert.AreEqual(132f, i.left, 0.5f);
            Assert.AreEqual(132f, i.right, 0.5f);
            Assert.AreEqual(63f, i.bottom, 0.5f);
        }
    }
}
