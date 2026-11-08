using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>Every visible word comes from a string table; numbers and names are marked as values.</summary>
    public static class UiText
    {
        public static T Bind<T>(T element, string key, string table = UiRoot.Table) where T : TextElement
        {
            element.SetBinding("text", new LocalizedString(table, key));
            return element;
        }

        public static T Value<T>(T element, string text) where T : TextElement
        {
            element.AddToClassList(UiRoot.ValueClass);
            element.text = text;
            return element;
        }

        public static Label Label(string key, string className = null, string table = UiRoot.Table)
        {
            var l = Bind(new Label(), key, table);
            if (className != null) l.AddToClassList(className);
            return l;
        }

        public static Label ValueLabel(string text, string className = null)
        {
            var l = Value(new Label(), text);
            if (className != null) l.AddToClassList(className);
            return l;
        }
    }
}
