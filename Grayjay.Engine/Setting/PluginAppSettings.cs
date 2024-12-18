

using System.Text.Json.Serialization;

namespace Grayjay.Engine.Setting
{
    public class PluginAppSettings : Settings<PluginAppSettings>
    {


        [SettingsField("Check for updates", SettingsField.TOGGLE, "If a plugin should be checked for updates on startup", 1)]
        public bool CheckForUpdate { get; set; } = true;

        [SettingsField("Visibility", SettingsField.GROUP, "Enable where this plugin's contents are visible", 2)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TablEnabledSettings TabEnabled { get; set; } = new TablEnabledSettings();
        public class TablEnabledSettings
        {
            [SettingsField("Home", SettingsField.TOGGLE, "Show content in home tab", 1)]
            public bool EnableHome { get; set; } = true;

            [SettingsField("Search", SettingsField.TOGGLE, "Show content in search results", 2)]
            public bool EnableSearch { get; set; } = true;
        }

        [SettingsField("Rate-limit", SettingsField.GROUP, "Settings related to reate-limiting this plugin's behavior", 3)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RateLimitSettings RateLimit { get; set; } = new RateLimitSettings();
        public class RateLimitSettings
        {
            [SettingsField("Subscriptions", SettingsField.DROPDOWN, "Limit the amount of subscription requests made", 1)]
            [SettingsDropdownOptions("Any", "25", "50", "75", "100", "125", "150", "200")]
            public int RateLimitSubs { get; set; } = 0;


            public int GetSubRateLimit()
            {
                return RateLimitSubs switch
                {
                    0 => -1,
                    1 => 25,
                    2 => 50,
                    3 => 75,
                    4 => 100,
                    5 => 125,
                    6 => 150,
                    7 => 200,
                    _ => -1
                };
            }
        }

        [SettingsField("Advanced", SettingsField.GROUP, "Settings for advanced users", 99)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AdvancedSettings Advanced { get; set; } = new AdvancedSettings();
        public class AdvancedSettings
        {
            [SettingsField("Allow tampered script", SettingsField.TOGGLE, "Show content in home tab", 1)]
            public bool AllowTamper { get; set; } = false;
        }

    }
}
