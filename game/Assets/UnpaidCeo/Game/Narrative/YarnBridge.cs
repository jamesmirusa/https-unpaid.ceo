using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Yarn.Unity;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Narrative;

namespace UnpaidCeo.Game.Narrative
{
    /// <summary>
    /// Plays the month's storylets through Yarn Spinner. Each option in a Yarn node runs
    /// <c>&lt;&lt;choose "id"&gt;&gt;</c>; the bridge forwards it to <see cref="GameEngine.ChooseStoryletOption"/>,
    /// so effects stay in the deterministic core and the decision log, never in Yarn variables.
    /// </summary>
    public sealed class YarnBridge : MonoBehaviour
    {
        [SerializeField] DialogueRunner runner;
        [SerializeField] LineProviderBehaviour lineProvider;

        GameEngine _engine;
        GameState _state;
        string _currentStorylet;

        /// <summary>Raised after a choice was applied: (storyletId, choiceId).</summary>
        public event Action<string, string> ChoiceApplied;

        public bool IsPlaying => runner != null && runner.IsDialogueRunning;

        void Awake()
        {
            if (runner == null) runner = GetComponent<DialogueRunner>();
            runner.AddCommandHandler("choose", (Action<string>)Choose);
        }

        void OnDestroy()
        {
            if (runner != null) runner.RemoveCommandHandler("choose");
        }

        public void Bind(GameEngine engine, GameState state)
        {
            _engine = engine;
            _state = state;
        }

        /// <summary>Only "en" and "ru" have Yarn text; every other UI language reads the English lines.</summary>
        public void SetLanguage(string languageCode)
        {
            var code = UnpaidCeo.Game.Audio.AudioAddresses.VoiceLanguage(languageCode);
            if (lineProvider != null) lineProvider.LocaleCode = code;
            if (lineProvider is BuiltinLocalisedLineProvider builtin) builtin.AssetLocaleCode = code;
        }

        /// <summary>Text of a Yarn line in the current dialogue language (used by the journal to quote chosen options).</summary>
        public string LineText(string lineId)
        {
            var project = runner != null ? runner.YarnProject : null;
            if (project == null) return null;
            string code = lineProvider != null ? lineProvider.LocaleCode : "en";
            var text = project.GetLocalization(code)?.GetLocalizedString(lineId) ?? project.GetLocalization("en")?.GetLocalizedString(lineId);
            if (text == null) return null;
            int colon = text.IndexOf(": ", System.StringComparison.Ordinal);
            return colon > 0 && colon < 24 ? text.Substring(colon + 2) : text;
        }

        /// <summary>Plays every storylet still pending this month, one after another.</summary>
        public async YarnTask PlayPendingAsync()
        {
            if (_engine == null || _state == null) throw new InvalidOperationException("YarnBridge.Bind must be called first");
            var byId = _engine.Content.Storylets.ToDictionary(s => s.Id);
            foreach (var id in _state.PendingStorylets.ToList())
            {
                if (!byId.TryGetValue(id, out var storylet)) { Debug.LogError($"Unknown pending storylet '{id}'"); continue; }
                await PlayAsync(storylet);
            }
        }

        public async YarnTask PlayAsync(StoryletDef storylet)
        {
            _currentStorylet = storylet.Id;
            try
            {
                await runner.StartDialogue(storylet.YarnNode);
                await runner.DialogueTask;
            }
            finally
            {
                _currentStorylet = null;
            }
        }

        void Choose(string choiceId)
        {
            if (_currentStorylet == null) { Debug.LogError($"<<choose \"{choiceId}\">> ran outside a storylet"); return; }
            try
            {
                _engine.ChooseStoryletOption(_state, _currentStorylet, choiceId);
                ChoiceApplied?.Invoke(_currentStorylet, choiceId);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"Storylet '{_currentStorylet}' rejected choice '{choiceId}': {e.Message}");
            }
        }
    }
}
