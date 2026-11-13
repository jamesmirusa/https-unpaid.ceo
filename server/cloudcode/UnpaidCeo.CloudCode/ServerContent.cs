using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Online;

namespace UnpaidCeo.CloudCode
{
    /// <summary>
    /// The game's content files embedded in this module, read in <see cref="ContentPack.Files"/> order: the same ContentDb
    /// the client builds, and the version a submission must match.
    /// </summary>
    public static class ServerContent
    {
        public static readonly IReadOnlyList<string> Texts = ContentPack.Files.Select(Read).ToArray();
        public static readonly ContentDb Db = ContentPack.Load(Texts);
        public static readonly string Version = ContentVersion.Of(Texts);

        static string Read(string file)
        {
            using var stream = typeof(ServerContent).Assembly.GetManifestResourceStream("content/" + file + ".json")
                ?? throw new FileNotFoundException("embedded content missing: " + file);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
