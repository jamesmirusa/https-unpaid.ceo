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
    /// has been read for the player signed in now, nothing after the cloud won (the device restarts on it), and a write
    /// never replaces a longer cloud game; writes are debounced to one per <see cref="DebounceSeconds"/>.
    /// </summary>
    public sealed class CloudSync
    {
        public const int DebounceSeconds = 20;
        /// <summary>Set by a deliberate reset in Settings: the next sync keeps the new game and overwrites the cloud.</summary>
        public const string ResetKey = "unpaidceo.cloud.reset";

        readonly ICloudSaves _saves;
        readonly Func<long> _now;
        readonly Func<string?> _player;
        long _lastWrite = long.MinValue;
        CloudSaveEnvelope? _pending;
        string? _pulledFor;

        /// <param name="playerId">The signed-in player now (the cloud save belongs to them).</param>
        public CloudSync(ICloudSaves saves, Func<long> nowUnix, Func<string?> playerId) { _saves = saves; _now = nowUnix; _player = playerId; }

        /// <summary>The cloud of the player signed in now has been read and the device game kept, so writing to it is safe.</summary>
        public bool Pulled => _pulledFor != null && _pulledFor == _player();

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
            string? player = _player();
            _pulledFor = null;
            var load = await _saves.Load();
            if (!load.Reachable || player == null) return null;
            bool reset = PlayerPrefs.GetInt(ResetKey, 0) == 1;
            var choice = reset ? SyncChoice.UseLocal : Choose(local, load.Envelope);
            if (choice == SyncChoice.UseCloud) return load.Envelope;   // writes stay off: the device restarts on the cloud game
            _pulledFor = player;
            if (choice == SyncChoice.UseLocal && await Write(local, checkCloud: false) && reset) { PlayerPrefs.DeleteKey(ResetKey); PlayerPrefs.Save(); }
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
            if (await Write(e, checkCloud: true)) return true;
            _pending ??= e;   // keep it for the next try unless a newer one arrived meanwhile
            return false;
        }

        /// <param name="checkCloud">
        /// Read the cloud first: if another device has gone further since the pull, nothing is written and writes stay off
        /// until the next pull (which adopts that game).
        /// </param>
        async Task<bool> Write(CloudSaveEnvelope e, bool checkCloud)
        {
            _lastWrite = _now();
            if (checkCloud)
            {
                var load = await _saves.Load();
                if (!load.Reachable) return false;
                if (Choose(e, load.Envelope) == SyncChoice.UseCloud) { _pulledFor = null; return false; }
            }
            if (!Pulled) return false;   // the player changed meanwhile
            return await _saves.Save(e);
        }
    }
}
