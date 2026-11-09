#nullable enable
using System;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using UnpaidCeo.Game.Services.Online;

namespace UnpaidCeo.Game.Ui
{
    public enum AccountChange { SignedUp, SignedIn, SignedOut, Renamed }

    /// <summary>
    /// Settings → Account: guest status and name; create an account (rules checked before sending), sign in, change the
    /// ranking name, sign out. Every outcome shows one localized message; offline says so and disables the buttons.
    /// </summary>
    public sealed class AccountPanel
    {
        readonly UiRoot _ui;
        readonly Label _status, _player;
        readonly VisualElement _guest, _signed;
        bool _busy;

        public TextField User { get; }
        public TextField Password { get; }
        public TextField NameField { get; }
        public Button SignUpButton { get; }
        public Button SignInButton { get; }
        public Button SetNameButton { get; }
        public Button SignOutButton { get; }
        public Label Message { get; }
        /// <summary>The string-table key of the message on screen, or null when none is shown.</summary>
        public string? MessageKey { get; private set; }
        public bool IsGuestFormShown => _guest.style.display != DisplayStyle.None;

        /// <summary>The player's identity changed: the game re-syncs its cloud save.</summary>
        public event Action<AccountChange>? Changed;

        public AccountPanel(VisualElement root, UiRoot ui)
        {
            _ui = ui;
            UiText.Bind(root.Q<Label>("account-caption"), "account.title");
            UiText.Bind(root.Q<Label>("account-user-caption"), "account.user");
            UiText.Bind(root.Q<Label>("account-pass-caption"), "account.pass");
            UiText.Bind(root.Q<Label>("account-rules"), "account.rules");
            UiText.Bind(root.Q<Label>("account-name-caption"), "account.name");
            _status = root.Q<Label>("account-status");
            _player = root.Q<Label>("account-player");
            _guest = root.Q("account-guest");
            _signed = root.Q("account-signed");
            User = root.Q<TextField>("account-user");
            Password = root.Q<TextField>("account-pass");
            Password.isPasswordField = true;
            NameField = root.Q<TextField>("account-name");
            Message = root.Q<Label>("account-message");

            SignUpButton = UiText.Bind(root.Q<Button>("account-signup"), "account.signup");
            SignInButton = UiText.Bind(root.Q<Button>("account-signin"), "account.signin");
            SetNameButton = UiText.Bind(root.Q<Button>("account-setname"), "account.setname");
            SignOutButton = UiText.Bind(root.Q<Button>("account-signout"), "account.signout");
            SignUpButton.clicked += () => _ = SignUp();
            SignInButton.clicked += () => _ = SignIn();
            SetNameButton.clicked += () => _ = SetName();
            SignOutButton.clicked += () => _ = SignOut();

            ui.OnlineChanged += _ => { MessageKey = null; Refresh(); };
            Refresh();
        }

        IOnlineServices Online => _ui.Online;

        public Task SignUp() => Act(async auth =>
        {
            if (!AccountRules.UsernameOk(User.value)) return "account.bad_user";
            if (!AccountRules.PasswordOk(Password.value)) return "account.weak";
            var r = await auth.SignUp(User.value, Password.value);
            if (r != AuthResult.Ok) return Key(r);
            Password.value = "";
            Changed?.Invoke(AccountChange.SignedUp);
            return "account.created";
        });

        public Task SignIn() => Act(async auth =>
        {
            if (!AccountRules.UsernameOk(User.value) || string.IsNullOrEmpty(Password.value)) return "account.invalid";
            var r = await auth.SignIn(User.value, Password.value);
            if (r != AuthResult.Ok) return Key(r);
            Password.value = "";
            Changed?.Invoke(AccountChange.SignedIn);
            return "account.welcome";
        });

        public Task SetName() => Act(async auth =>
        {
            var name = NameField.value?.Trim();
            if (string.IsNullOrEmpty(name)) return "account.bad_name";
            var r = await auth.SetName(name!);
            if (r != AuthResult.Ok) return Key(r);
            Changed?.Invoke(AccountChange.Renamed);
            return "account.renamed";
        });

        public Task SignOut() => Act(async auth =>
        {
            await auth.SignOut();
            Changed?.Invoke(AccountChange.SignedOut);
            return "account.signed_out";
        });

        static string Key(AuthResult r) => r switch
        {
            AuthResult.InvalidCredentials => "account.invalid",
            AuthResult.NameTaken => "account.taken",
            AuthResult.WeakPassword => "account.weak",
            AuthResult.Offline => "account.offline",
            _ => "account.error",
        };

        async Task Act(Func<IOnlineAuth, Task<string>> action)
        {
            if (_busy) return;
            if (!Online.Available) { Show("account.offline"); return; }
            _busy = true;
            Refresh();
            try { Show(await action(Online.Auth)); }
            finally { _busy = false; Refresh(); }
        }

        public void Refresh()
        {
            bool online = Online.Available, account = online && !Online.Auth.IsGuest;
            UiText.Bind(_status, !online ? "account.status.offline" : account ? "account.status.signed" : "account.status.guest");
            UiText.Value(_player, account ? Online.Auth.PlayerName ?? "" : "");
            _player.style.display = account ? DisplayStyle.Flex : DisplayStyle.None;
            _guest.style.display = account ? DisplayStyle.None : DisplayStyle.Flex;
            _signed.style.display = account ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var b in new[] { SignUpButton, SignInButton, SetNameButton, SignOutButton }) b.SetEnabled(online && !_busy);
            if (!online) Show("account.offline");
            else if (MessageKey == null) Message.style.display = DisplayStyle.None;
        }

        void Show(string key)
        {
            MessageKey = key;
            UiText.Bind(Message, key);
            Message.style.display = DisplayStyle.Flex;
        }
    }
}
