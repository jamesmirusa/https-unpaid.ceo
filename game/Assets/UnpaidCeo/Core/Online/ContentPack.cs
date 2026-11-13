using System;
using System.Collections.Generic;
using UnpaidCeo.Core.Content;

namespace UnpaidCeo.Core.Online
{
    /// <summary>
    /// The shipped content files in one fixed order, shared by the game (Addressables) and the Cloud Code module
    /// (embedded resources), so both build the same ContentDb and the same ContentVersion.
    /// </summary>
    public static class ContentPack
    {
        public static readonly string[] Files = { "industries", "balance", "free_actions", "storylets_ch1", "npcs", "lessons", "missions", "storylets_ch2", "storylets_ch3" };

        /// <summary>Builds the content from the files' texts in <see cref="Files"/> order.</summary>
        public static ContentDb Load(IReadOnlyList<string> texts)
        {
            if (texts == null || texts.Count != Files.Length) throw new ArgumentException($"expected {Files.Length} content files", nameof(texts));
            return ContentDb.FromJson(new ContentFiles
            {
                Industries = texts[0], Balance = texts[1], FreeActions = texts[2],
                Storylets = texts[3], Npcs = texts[4], Lessons = texts[5], Missions = texts[6],
                MoreStorylets = new[] { texts[7], texts[8] },
            });
        }
    }
}
