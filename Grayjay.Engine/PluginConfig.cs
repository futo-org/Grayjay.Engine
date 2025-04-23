using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Grayjay.Engine
{
    public class PluginConfig
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public int Version { get; set; }

        public string Author { get; set; }
        public string AuthorUrl { get; set; }


        public string IconUrl { get; set; }
        public string SourceUrl { get; set; }
        public string ScriptUrl { get; set; }

        public List<string> AllowUrls { get; set; }
        public List<string> Packages { get; set; } = new List<string>();
        public List<string> PackagesOptional { get; set; } = new List<string>();

        public string ScriptSignature { get; set; }
        public string ScriptPublicKey { get; set; }

        public PluginCaptchaConfig Captcha { get; set; }
        public PluginAuthConfig Authentication { get; set; }

        public Dictionary<string, string> Constants { get; set; }

        public int SubscriptionRateLimit { get; set; }
        public bool EnableInSearch { get; set; }
        public bool EnableInHome { get; set; }
        public List<int> SupportedClaimTypes { get; set; }
        public int PrimaryClaimFieldType { get; set; }

        public string DeveloperSubmitUrl { get; set; } //Not implemented yet
        public bool AllowAllHttpHeaderAccess { get; set; }
        public int MaxDownloadParallelism { get; set; } //Not implemented yet


        public List<PluginSetting> Settings { get; set; } = new List<PluginSetting>();

        private bool? _allowAnywhereVal = null;
        private bool _allowAnywhere
        {
            get
            {
                if (_allowAnywhereVal == null)
                    _allowAnywhereVal = AllowUrls.Any(x => x.ToLower() == "everywhere");
                return _allowAnywhereVal.Value;
            }
        }
        private List<string> _allowUrlsLowerVal = null;
        private List<string> _allowUrlsLower
        {
            get
            {
                if (_allowUrlsLowerVal == null)
                    _allowUrlsLowerVal = AllowUrls.Select(x => x.ToLower()).ToList();
                return _allowUrlsLowerVal;
            }
        }

        public string AbsoluteIconUrl => (SourceUrl == null || IconUrl == null) ? null : new Uri(new Uri(SourceUrl), IconUrl).AbsoluteUri.ToString();
        public string AbsoluteScriptUrl => (SourceUrl == null || ScriptUrl == null) ? null : new Uri(new Uri(SourceUrl), ScriptUrl).AbsoluteUri.ToString();


        private static JsonSerializerOptions _serializerOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        };
        public static PluginConfig FromJson(string json)
        {
            return JsonSerializer.Deserialize<PluginConfig>(json, _serializerOptions);
        }
        public static PluginConfig FromUrl(string url)
        {
            using (WebClient client = new WebClient())
            {
                string data = client.DownloadString(url);
                return JsonSerializer.Deserialize<PluginConfig>(data, _serializerOptions);
            }
        }
        public bool IsUrlAllowed(string url)
        {
            if (_allowAnywhere)
                return true;
            var uri = new Uri(url);
            var host = uri.Host?.ToLower() ?? "";
            return _allowUrlsLower.Any(x => x == host || (x.Length > 0 && x[0] == '.' && host.MatchesDomain(x)));
        }

        public bool VerifyAuthority()
        {
            return true;
        }
        public bool VerifySignature(string script)
        {
            return SignatureProvider.Verify(script, ScriptSignature, ScriptPublicKey);
        }

        public List<PluginWarning> GetWarnings()
        {
            List<PluginWarning> warnings = new List<PluginWarning>();

            string script = null;
            try
            {
                using (WebClient client = new WebClient())
                {
                    script = client.DownloadString(AbsoluteScriptUrl);
                }
            }
            catch (Exception ex)
            {
                warnings.Add(new PluginWarning()
                {
                    Title = "Broken Script url",
                    Description = "Unable to download the script, plugin config appears broken"
                });
            }

            if (AllowUrls.Any(x=>x == "everywhere"))
                warnings.Add(new PluginWarning()
                {
                    Title = "Unrestricted Web Access",
                    Description = "This plugin requires access to all domains, this may inlcude malicious domains."
                });
            if (AllowAllHttpHeaderAccess)
                warnings.Add(new PluginWarning()
                {
                    Title = "Unrestricted HTTP Header Access",
                    Description = "Allows this plugin to access all headers (including cookies and authorization headers) for unauthenticated requests."
                });

            if (string.IsNullOrEmpty(ScriptSignature) || string.IsNullOrEmpty(ScriptPublicKey))
                warnings.Add(new PluginWarning()
                {
                    Title = "Missing Signature",
                    Description = "This plugin does not have a signature. This makes updating the plugin less safe as it makes it easier for a malicious actor besides the developer to update to a malicious version."
                });
            else if(script != null)
            {
                if(!VerifyAuthority())
                    warnings.Add(new PluginWarning()
                    {
                        Title = "Invalid Public Key",
                        Description = "The developer's public key appears invalid"
                    });
                if (!VerifySignature(script))
                    warnings.Add(new PluginWarning()
                    {
                        Title = "Tampered Script",
                        Description = "The script does not match its signature, this may imply tampering by a malicious party."
                    });
            }

            return warnings;
        }
    }

    public class PluginWarning
    {
        public string Title { get; set; }
        public string Description { get; set; }
    }


    public class PluginSetting
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public string Default { get; set; }
        public string Variable { get; set; }
        public string Dependency { get; set; }
        public string WarningDialog { get; set; }
        public List<string> Options { get; set; }

        [JsonIgnore]
        public string VariableOrName => (string.IsNullOrEmpty(Variable)) ? Name : Variable;
    }
    public class PluginCaptchaConfig
    {
        public string CaptchaUrl { get; set; }
        public string CompletionUrl { get; set; }
        public List<string> CookiesToFind { get; set; }
        public string UserAgent { get; set; }
        public bool CookiesExclOthers { get; set; }
    }
    public class PluginAuthConfig
    {
        public string LoginUrl { get; set; }
        public string CompletionUrl { get; set; }
        public List<string> AllowedDomains { get; set; }
        public List<string> HeadersToFind { get; set; }
        public List<string> CookiesToFind { get; set; }
        public bool CookiesExclOthers { get; set; }
        public string UserAgent { get; set; }
        public string LoginButton { get; set; }
        public Dictionary<string, List<string>> DomainHeadersToFind { get; set; }
    }
}
