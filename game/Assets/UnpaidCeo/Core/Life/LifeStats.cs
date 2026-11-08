using System;

namespace UnpaidCeo.Core.Life
{
    /// <summary>The hero's life scales, each clamped to 0..100 on every write.</summary>
    public sealed class LifeStats
    {
        int _energy, _relationships, _reputation, _honesty, _connections;

        public int Energy { get => _energy; set => _energy = Clamp(value); }
        public int Relationships { get => _relationships; set => _relationships = Clamp(value); }
        public int Reputation { get => _reputation; set => _reputation = Clamp(value); }
        public int Honesty { get => _honesty; set => _honesty = Clamp(value); }
        public int Connections { get => _connections; set => _connections = Clamp(value); }

        public static int Clamp(int v) => Math.Max(0, Math.Min(100, v));

        public int Get(string stat)
        {
            switch (stat)
            {
                case "energy": return Energy;
                case "relationships": return Relationships;
                case "reputation": return Reputation;
                case "honesty": return Honesty;
                case "connections": return Connections;
                default: throw new ArgumentException($"unknown stat '{stat}'", nameof(stat));
            }
        }

        public void Add(string stat, int delta)
        {
            switch (stat)
            {
                case "energy": Energy += delta; break;
                case "relationships": Relationships += delta; break;
                case "reputation": Reputation += delta; break;
                case "honesty": Honesty += delta; break;
                case "connections": Connections += delta; break;
                default: throw new ArgumentException($"unknown stat '{stat}'", nameof(stat));
            }
        }

        public static bool IsKnown(string stat) =>
            stat == "energy" || stat == "relationships" || stat == "reputation" || stat == "honesty" || stat == "connections";
    }
}
