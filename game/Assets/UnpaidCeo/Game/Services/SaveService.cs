using UnityEngine;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Persistence;

namespace UnpaidCeo.Game.Services
{
    /// <summary>Local save in PlayerPrefs (IndexedDB on Web). A corrupt save is moved to a backup key, never retried.</summary>
    public static class SaveService
    {
        public const string Key = "unpaidceo.save.v1";
        public const string BackupKey = "unpaidceo.save.backup";
        /// <summary>Turn budget, streak and missions: beside the game save, not inside it (never replayed or hashed).</summary>
        public const string MetaKey = "unpaidceo.meta.v1";

        public static void SaveMeta(PlayerMeta m)
        {
            PlayerPrefs.SetString(MetaKey, SaveCodec.EncodeMeta(m));
            PlayerPrefs.Save();
        }

        /// <summary>A fresh record when there is none. Kept when the game is reset, so a new game brings no extra turns.</summary>
        public static PlayerMeta LoadMeta() => SaveCodec.DecodeMeta(PlayerPrefs.GetString(MetaKey, null));

        public static void Save(GameState s)
        {
            PlayerPrefs.SetString(Key, SaveCodec.Encode(s));
            PlayerPrefs.Save();
        }

        /// <summary>Null when there is no save yet.</summary>
        public static LoadResult Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return null;
            var r = SaveCodec.Decode(PlayerPrefs.GetString(Key));
            if (r.Status == LoadStatus.CorruptReset)
            {
                PlayerPrefs.SetString(BackupKey, r.Backup ?? "");
                PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();
            }
            return r;
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
