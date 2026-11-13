using System.Collections.Generic;
using UnityEngine.UIElements;
using UnpaidCeo.Game.Ui.Kit;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// Stage 5B · a relationship level reached, Persona-style but short: flash → sound → banner "Tony — level 3" →
    /// the bonus in one line → gone (1.5 s). A tap skips it; it never blocks the game (it queues if two come at once).
    /// </summary>
    public sealed class LevelUpMoment
    {
        public const float Seconds = 1.5f;
        readonly Label _name, _level, _bonus;
        readonly VisualElement _banner;
        readonly Queue<(string npc, int level, string key)> _queue = new Queue<(string, int, string)>();
        IVisualElementScheduledItem _leave;

        public VisualElement Root { get; }
        public bool Showing => Root.style.display == DisplayStyle.Flex;

        public LevelUpMoment()
        {
            Root = new VisualElement { name = "level-up" };
            Root.AddToClassList("level-up");
            Root.RegisterCallback<ClickEvent>(_ => Next());
            var flash = new VisualElement { name = "level-up-flash", pickingMode = PickingMode.Ignore };
            flash.AddToClassList("level-up__flash");
            Root.Add(flash);
            _banner = new VisualElement { name = "level-up-banner", pickingMode = PickingMode.Ignore };
            _banner.AddToClassList("level-up__banner");
            _banner.Add(Icons.Game("emote_level", "level-up__icon"));
            var title = new VisualElement { pickingMode = PickingMode.Ignore };
            title.AddToClassList("level-up__title");
            _name = new Label { name = "level-up-name", pickingMode = PickingMode.Ignore };
            _name.AddToClassList("level-up__name");
            title.Add(_name);
            var word = UiText.Label("levelup.level", "level-up__word");
            word.pickingMode = PickingMode.Ignore;
            title.Add(word);
            _level = UiText.ValueLabel("", "level-up__level");
            _level.name = "level-up-level";
            _level.pickingMode = PickingMode.Ignore;
            title.Add(_level);
            _banner.Add(title);
            var bonusRow = new VisualElement { pickingMode = PickingMode.Ignore };
            bonusRow.AddToClassList("level-up__bonus-row");
            bonusRow.Add(Icons.Game("level_bonus", "level-up__bonus-icon"));
            _bonus = new Label { name = "level-up-bonus", pickingMode = PickingMode.Ignore };
            _bonus.AddToClassList("level-up__bonus");
            bonusRow.Add(_bonus);
            _banner.Add(bonusRow);
            Root.Add(_banner);
            Root.style.display = DisplayStyle.None;
        }

        public void Show(string npc, int level, string bonusKey)
        {
            _queue.Enqueue((npc, level, bonusKey));
            if (!Showing) Next();
        }

        void Next()
        {
            _leave?.Pause();
            if (_queue.Count == 0) { Root.style.display = DisplayStyle.None; Root.RemoveFromClassList("level-up--in"); return; }
            var (npc, level, key) = _queue.Dequeue();
            UiText.Bind(_name, "npc." + npc);
            _level.text = level.ToString();
            if (key != null) { UiText.Bind(_bonus, key); _bonus.parent.style.display = DisplayStyle.Flex; }
            else _bonus.parent.style.display = DisplayStyle.None;
            Root.style.display = DisplayStyle.Flex;
            Root.BringToFront();
            Root.RemoveFromClassList("level-up--in");
            Root.schedule.Execute(() => Root.AddToClassList("level-up--in")).StartingIn(16);
            UiSound.Play(UiSound.Fanfare);
            Haptics.Play(Haptics.Kind.Success);
            _leave = Root.schedule.Execute(Next).StartingIn((long)(Seconds * 1000));
        }
    }
}
