using System;
using Newtonsoft.Json;

namespace UnpaidCeo.Core.Primitives
{
    /// <summary>Money in whole cents. All game money uses this type; fractional cents round half away from zero.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public readonly struct Money : IEquatable<Money>, IComparable<Money>
    {
        public readonly long Cents;

        Money(long cents) { Cents = cents; }

        public static Money Zero => default;
        public static Money FromCents(long cents) => new Money(cents);
        public static Money FromDollars(decimal dollars) =>
            new Money((long)Math.Round(dollars * 100m, MidpointRounding.AwayFromZero));

        public Money MulRound(double factor) =>
            new Money((long)Math.Round(Cents * factor, MidpointRounding.AwayFromZero));

        public static Money operator +(Money a, Money b) => new Money(a.Cents + b.Cents);
        public static Money operator -(Money a, Money b) => new Money(a.Cents - b.Cents);
        public static Money operator -(Money a) => new Money(-a.Cents);
        public static bool operator <(Money a, Money b) => a.Cents < b.Cents;
        public static bool operator >(Money a, Money b) => a.Cents > b.Cents;
        public static bool operator <=(Money a, Money b) => a.Cents <= b.Cents;
        public static bool operator >=(Money a, Money b) => a.Cents >= b.Cents;
        public static bool operator ==(Money a, Money b) => a.Cents == b.Cents;
        public static bool operator !=(Money a, Money b) => a.Cents != b.Cents;

        public bool Equals(Money other) => Cents == other.Cents;
        public override bool Equals(object obj) => obj is Money m && Equals(m);
        public override int GetHashCode() => Cents.GetHashCode();
        public int CompareTo(Money other) => Cents.CompareTo(other.Cents);
        public override string ToString() => Cents + "c";
    }

    /// <summary>Serializes Money as a plain integer number of cents.</summary>
    public sealed class MoneyJsonConverter : JsonConverter<Money>
    {
        public override void WriteJson(JsonWriter writer, Money value, JsonSerializer serializer) =>
            writer.WriteValue(value.Cents);

        public override Money ReadJson(JsonReader reader, Type objectType, Money existingValue, bool hasExistingValue, JsonSerializer serializer) =>
            Money.FromCents(Convert.ToInt64(reader.Value));
    }
}
