#nullable enable
using System.Linq;
using System.Text.RegularExpressions;

namespace UnpaidCeo.Game.Services.Online
{
    /// <summary>
    /// The UGS username/password rules, checked before any request so the account form can explain them:
    /// usernames 3–20 characters of letters, digits and . - @ _; passwords 8–30 characters with an upper- and a
    /// lowercase letter, a digit and a symbol.
    /// </summary>
    public static class AccountRules
    {
        static readonly Regex Username = new Regex(@"^[A-Za-z0-9.\-@_]{3,20}$");

        public static bool UsernameOk(string? user) => user != null && Username.IsMatch(user);

        public static bool PasswordOk(string? pass) =>
            pass != null && pass.Length >= 8 && pass.Length <= 30
            && pass.Any(char.IsUpper) && pass.Any(char.IsLower) && pass.Any(char.IsDigit)
            && pass.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
    }
}
