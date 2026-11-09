#nullable enable
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Persistence;

namespace UnpaidCeo.Game.Services.Online
{
    public enum SyncChoice { UseLocal, UseCloud, Equal }

    /// <summary>
    /// Keeps the device save and the cloud save in step. The longer game wins (months played), then the newer one; so a
    /// fresh install downloads real progress instead of uploading a newcomer over it. Nothing is written before the cloud
    /// has been read, and writes are debounced to one per <see cref="DebounceSeconds"/>.
    /// </summary>
    public sealed class CloudSync
    {
        public const int DebounceSeconds = 20;
        /// <summary>Set by a deliberate reset in Settings: the next sync keeps the new game and overwrites the cloud.</summary>
        public const string ResetKey = "unpaidceo.cloud.reset";

        readonly ICloudSaves _saves;
        readonly Func<long> _now;
        long _lastWrite = long.MinValue;
        CloudSaveEnvelope? _pending;

        public CloudSync(ICloudSaves saves, Func<long> nowUnix) { _saves = saves; _now = nowUnix; }

        /// <summary>The cloud has been read this session, so writing to it is safe.</summary>
        public bool Pulled { get; private set; }

        public static SyncChoice Choose(CloudSaveEnvelope? local, CloudSaveEnvelope? cloud)
        {
            bool hasCloud = cloud?.SaveJson != null, hasLocal = local?.SaveJson != null;
            if (!hasCloud) return hasLocal ? SyncChoice.UseLocal : SyncChoice.Equal;
            if (!hasLocal) return SyncChoice.UseCloud;
            if (local!.Months != cloud!.Months) return local.Months > cloud.Months ? SyncChoice.UseLocal : SyncChoice.UseCloud;
            if (local.UpdatedUnix != cloud.UpdatedUnix) return local.UpdatedUnix > cloud.UpdatedUnix ? SyncChoice.UseLocal : SyncChoice.UseCloud;
            return SyncChoice.Equal;
        }

        /// <summary>The device's game as a cloud save: months played are the months completed.</summary>
        public static CloudSaveEnvelope Envelope(GameState s, PlayerMeta? meta, long nowUnix) => new CloudSaveEnvelope
        {
            SaveJson = SaveCodec.Encode(s),
            MetaJson = meta != null ? SaveCodec.EncodeMeta(meta) : null,
            Months = Math.Max(0, s.Month - 1),
            UpdatedUnix = nowUnix,
        };

        public static void MarkReset() { PlayerPrefs.SetInt(ResetKey, 1); PlayerPrefs.Save(); }

        /// <summary>
        /// Reads the cloud and settles the conflict. Returns the cloud save when the device should adopt it, otherwise
        /// null (the device save stays and, if it is the one to keep, is written to the cloud). Unreachable: null, and
        /// writes stay disabled until a later pull succeeds.
        /// </summary>
        public async Task<CloudSaveEnvelope?> Pull(CloudSaveEnvelope local)
        {
            var load = await _saves.Load();
            if (!load.Reachable) return null;
            Pulled = true;
            bool reset = PlayerPrefs.GetInt(ResetKey, 0) == 1;
            var choice = reset ? SyncChoice.UseLocal : Choose(local, load.Envelope);
            if (choice == SyncChoice.UseCloud) return load.Envelope;
            if (choice == SyncChoice.UseLocal && await Write(local) && reset) { PlayerPrefs.DeleteKey(ResetKey); PlayerPrefs.Save(); }
            return null;
        }

        /// <summary>Queues the latest save and writes it unless the last write was under <see cref="DebounceSeconds"/> ago.</summary>
        public async Task<bool> Push(CloudSaveEnvelope e)
        {
            _pending = e;
            if (!Pulled || _now() - _lastWrite < DebounceSeconds) return false;
            return await FlushPending();
        }

        /// <summary>Writes the waiting save now (the game is pausing or closing).</summary>
        public async Task<bool> FlushPending()
        {
            if (!Pulled || _pending == null) return false;
            var e = _pending;
            _pending = null;
            if (await Write(e)) return true;
            _pending ??= e;   // keep it for the next try unless a newer one arrived meanwhile
            return false;
        }

        async Task<bool> Write(CloudSaveEnvelope e)
        {
            _lastWrite = _now();
            return await _saves.Save(e);
        }
    }
}
