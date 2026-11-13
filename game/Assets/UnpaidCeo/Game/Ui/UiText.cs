using System.Runtime.CompilerServices;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>Every visible word comes from a string table; numbers and names are marked as values.</summary>
    public static class UiText
    {
        static readonly ConditionalWeakTable<TextElement, Link> Links = new ConditionalWeakTable<TextElement, Link>();

        /// <summary>
        /// Shows <paramref name="key"/> from the string table and follows locale changes. Text is pushed through
        /// LocalizedString.StringChanged rather than UI Toolkit data binding, which left labels blank in the
        /// web player. Binding the same element again replaces the previous key.
        /// </summary>
        public static T Bind<T>(T element, string key, string table = UiRoot.Table) where T : TextElement
        {
            if (Links.TryGetValue(element, out var old))
            {
                old.Dispose();
                Links.Remove(element);
            }
            Links.Add(element, new Link(element, new LocalizedString(table, key)));
            return element;
        }

        /// <summary>True when the element's text comes from a string table (see <see cref="Bind{T}"/>).</summary>
        public static bool IsLocalized(TextElement element) => Links.TryGetValue(element, out _);

        sealed class Link
        {
            readonly TextElement _element;
            readonly LocalizedString _text;
            bool _listening;

            public Link(TextElement element, LocalizedString text)
            {
                _element = element;
                _text = text;
                element.RegisterCallback<AttachToPanelEvent>(OnAttach);
                element.RegisterCallback<DetachFromPanelEvent>(OnDetach);
                Listen();
            }

            void OnAttach(AttachToPanelEvent e) => Listen();
            void OnDetach(DetachFromPanelEvent e) => Stop();
            void Set(string value) => _element.text = value;

            void Listen()
            {
                if (_listening) return;
                _listening = true;
                _text.StringChanged += Set;
            }

            /// <summary>Detaches for good: an old key must not come back when the element re-enters a panel.</summary>
            public void Dispose()
            {
                _element.UnregisterCallback<AttachToPanelEvent>(OnAttach);
                _element.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
                Stop();
            }

            void Stop()
            {
                if (!_listening) return;
                _listening = false;
                _text.StringChanged -= Set;
            }
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
