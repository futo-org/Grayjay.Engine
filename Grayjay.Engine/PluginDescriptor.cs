using Grayjay.Engine.Setting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Grayjay.Engine
{
    public class PluginDescriptor
    {
        public PluginConfig Config { get; set; }
        public Dictionary<string, string?> Settings { get; set; } = new Dictionary<string, string?>();
        public PluginAppSettings AppSettings { get; set; } = new PluginAppSettings();

        public string AuthEncrypted { get; private set; } = null;
        private string _authEncryptedLast = null;
        public string CaptchaEncrypted { get; private set; } = null;
        private string _captchaEncryptedLast = null;

        public List<string> Flags { get; set; }

        private readonly object _authCookieLock = new object();
        private SourceAuth _authCookieCache = null;
        private string _authCookieCacheEncrypted = null;

        public event Action OnAuthChanged;
        public event Action OnAuthCookiesUpdated;
        public event Action OnCaptchaChanged;

        public bool HasLoggedIn => !string.IsNullOrEmpty(AuthEncrypted);
        public bool HasCaptcha => !string.IsNullOrEmpty(CaptchaEncrypted);

        public SourceAuth GetAuth()
        {
            if (string.IsNullOrEmpty(AuthEncrypted))
                return null;
            if (Encryption == null)
                throw new InvalidOperationException("No encryption provider set");
            try
            {
                return JsonSerializer.Deserialize<SourceAuth>(Encryption.Decrypt(AuthEncrypted));
            }
            catch(Exception ex)
            {
                AuthEncrypted = null;
                return null;
            }
        }
        public void SetAuth(SourceAuth auth)
        {
            if (Encryption == null)
                throw new InvalidOperationException("No encryption provider set");
            if (auth == null)
            {
                AuthEncrypted = null;
                return;
            }
            string json = JsonSerializer.Serialize(auth);
            AuthEncrypted = Encryption.Encrypt(json);
            OnAuthChanged?.Invoke();
        }

        private SourceAuth GetAuthCookieCache()
        {
            if (_authCookieCache == null || _authCookieCacheEncrypted != AuthEncrypted)
            {
                _authCookieCache = GetAuth();
                _authCookieCacheEncrypted = AuthEncrypted;
            }
            return _authCookieCache;
        }

        public Dictionary<string, string> GetAuthCookies(Func<string, bool> domainFilter)
        {
            var result = new Dictionary<string, string>();
            lock (_authCookieLock)
            {
                var cookieMap = GetAuthCookieCache()?.CookieMap;
                if (cookieMap == null)
                    return result;
                foreach (var domainCookies in cookieMap.Where(x => domainFilter(x.Key)))
                    foreach (var cookie in domainCookies.Value)
                        result[cookie.Key] = cookie.Value;
            }
            return result;
        }

        public bool UpdateAuthCookie(string domain, string name, string value)
        {
            lock (_authCookieLock)
            {
                var auth = GetAuthCookieCache();
                if (auth?.CookieMap == null || !auth.CookieMap.TryGetValue(domain, out var cookies) || !cookies.TryGetValue(name, out var existing) || existing == value)
                    return false;
                cookies[name] = value;
                AuthEncrypted = Encryption.Encrypt(JsonSerializer.Serialize(auth));
                _authCookieCacheEncrypted = AuthEncrypted;
            }
            OnAuthCookiesUpdated?.Invoke();
            return true;
        }

        public Dictionary<string, string?> GetSettingsWithDefaults()
        {
            var map = new Dictionary<string, string?>(Settings);
            foreach(var field in Config.Settings)
            {
                if (!map.ContainsKey(field.VariableOrName) || map[field.VariableOrName] == null)
                    map[field.VariableOrName] = field.Default;
            }
            return map;
        }

        public SourceCaptcha GetCaptchaData()
        {
            if (string.IsNullOrEmpty(CaptchaEncrypted))
                return null;
            if (Encryption == null)
                throw new InvalidOperationException("No encryption provider set");
            try
            {
                var captcha = Encryption.Decrypt(CaptchaEncrypted);
                return JsonSerializer.Deserialize<SourceCaptcha>(captcha);
            }
            catch(Exception ex)
            {
                CaptchaEncrypted = null;
                return null;
            }
            finally
            {
                if (_captchaEncryptedLast != CaptchaEncrypted)
                    OnCaptchaChanged?.Invoke();
                _captchaEncryptedLast = CaptchaEncrypted;
            }
        }
        public void SetCaptchaData(SourceCaptcha captcha)
        {
            if (Encryption == null)
                throw new InvalidOperationException("No encryption provider set");
            string json = JsonSerializer.Serialize(captcha);
            CaptchaEncrypted = Encryption.Encrypt(json);
        }


        public PluginDescriptor(PluginConfig config, string authEncrypted = null, string captchaEncrypted = null, Dictionary<string, string?> settings = null)
        {
            this.Config = config;
            this.AuthEncrypted = authEncrypted;
            this.CaptchaEncrypted = captchaEncrypted;
            this.Flags = new List<string>();
            this.Settings = settings ?? new Dictionary<string, string?>();
        }


        public SettingsObject<Dictionary<string, string>> GetSettingsObject()
        {
            List<SettingsField> fields = new List<SettingsField>();
            Dictionary<string, string> currentSettings = new Dictionary<string, string>();
            SettingsFieldGroup lastGroup = null;
            foreach(var setting in Config.Settings)
            {
                SettingsField field = null;
                switch (setting.Type)
                {
                    case "Boolean":
                        bool defaultBool = false;
                        bool.TryParse(setting.Default, out defaultBool);

                        field = new SettingsFieldToggle(setting.Name, setting.Description, setting.Variable, defaultBool);
                        if (!Settings.ContainsKey(setting.Variable))
                            Settings.Add(setting.Variable, defaultBool.ToString());
                        break;
                    case "Header":
                        lastGroup = new SettingsFieldGroupFlat(setting.Name, setting.Description, setting.Variable);
                        fields.Add(lastGroup);
                        break;
                    case "Dropdown":
                        int defaultIndex = 0;
                        int.TryParse(setting.Default, out defaultIndex);
                        field = new SettingsFieldDropDown(setting.Name, setting.Description, setting.Variable, defaultIndex, null, setting.Options?.ToArray() ?? new string[0]);
                        if (!Settings.ContainsKey(setting.Variable))
                            Settings.Add(setting.Variable, defaultIndex.ToString());
                        break;
                }
                if(field != null)
                {
                    field.Dependency = setting.Dependency;
                    field.WarningDialog = setting.WarningDialog;

                    if (lastGroup != null)
                        lastGroup.Fields = lastGroup.Fields.Concat(new[] { field }).ToArray();
                    else
                        fields.Add(field);
                }
            }

            return new SettingsObject<Dictionary<string, string>>()
            {
                ID = Config.ID,
                Fields = fields.ToArray(),
                Object = Settings ?? new Dictionary<string, string?>()
            };
        }

        public static IPluginEncryptionProvider Encryption { get; set; }
    }

    public interface IPluginEncryptionProvider
    {
        string Encrypt(string data);
        string Decrypt(string data);
    }

    public class SourceAuth
    {
        public Dictionary<string, Dictionary<string, string>> CookieMap { get; set; }
        public Dictionary<string, Dictionary<string, string>> Headers { get; set; }
        public string? UserAgent { get; set; }
    }
    public class SourceCaptcha
    {
        public Dictionary<string, Dictionary<string, string>> CookieMap { get; set; }
        public Dictionary<string, Dictionary<string, string>> Headers { get; set; }
        public string? UserAgent { get; set; }
    }
}
