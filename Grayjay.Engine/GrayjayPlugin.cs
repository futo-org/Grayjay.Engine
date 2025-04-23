﻿using Grayjay.Engine.Exceptions;
using Grayjay.Engine.Models.Capabilities;
using Grayjay.Engine.Models.Channel;
using Grayjay.Engine.Models.Comments;
using Grayjay.Engine.Models.Detail;
using Grayjay.Engine.Models.Feed;
using Grayjay.Engine.Models.General;
using Grayjay.Engine.Models.Playback;
using Grayjay.Engine.Models.Video.Additions;
using Grayjay.Engine.Packages;
using Grayjay.Engine.Pagers;
using Grayjay.Engine.V8;
using Grayjay.Engine.Web;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Grayjay.Engine
{
    public class GrayjayPlugin : IDisposable
    {

        private static ConcurrentDictionary<ScriptEngine, GrayjayPlugin> _activePlugins = new ConcurrentDictionary<ScriptEngine, GrayjayPlugin>();

        private string _script;
        private V8ScriptEngine _engine = null;

        private object _busyLock = new object();
        private int _busyCount = 0;
        public bool IsBusy => _busyCount > 0;

        public virtual string ID => Config?.ID;

        private SourceAuth _auth;
        private SourceCaptcha _captcha;

        private string? _savedState = null;

        private object _enableLock = new object();

        private Dictionary<string, string?> _settings => Descriptor?.GetSettingsWithDefaults() ?? new Dictionary<string, string>();

        private ResultCapabilities? _channelCapabilities = null;
        private ResultCapabilities? _searchCapabilities = null;
        private List<string> _peekChannelTypes = null;

        public PluginConfig Config { get; private set; }
        public PluginDescriptor Descriptor { get; private set; }

        public bool IsInitialized { get; private set; }
        public bool IsEnabled { get; private set; }

        private List<Package> _packages = new List<Package>();

        public PlatformClientCapabilities Capabilities { get; private set; } = new PlatformClientCapabilities();

        public PluginHttpClient HttpClient { get; private set; }
        public PluginHttpClient HttpClientAuth { get; private set; }
        public Dictionary<string, PluginHttpClient> HttpClientOthers { get; private set; } = new Dictionary<string, PluginHttpClient>();
        public void RegisterHttpClient(PluginHttpClient httpClient)
        {
            lock (HttpClientOthers)
            {
                HttpClientOthers.Add(httpClient.ClientID, httpClient);
            }
        }

        public event Action<GrayjayPlugin> BeforeInitialize;
        public event Action<PluginConfig, string> OnLog;
        public event Action<PluginConfig, string> OnToast;
        public event Action<GrayjayPlugin> OnStopped;

        public event Action<PluginConfig, ScriptException> OnScriptException;

        public event Action<PluginConfig, string> OnBusyCallStart;
        public event Action<PluginConfig, string, long> OnBusyCallEnd;

        public GrayjayPlugin(PluginConfig config, string script, Dictionary<string, string?>? settings = null, string savedState = null)
        {
            Config = config;
            _script = script;
            _savedState = savedState;
            Descriptor = new PluginDescriptor(config, null, null, settings ?? new Dictionary<string, string?>());

            HttpClient = new PluginHttpClient(this, null, _captcha);
            HttpClientAuth = new PluginHttpClient(this, _auth, _captcha);
        }
        public GrayjayPlugin(PluginDescriptor descriptor, string script, string? savedState = null, PluginHttpClient client = null, PluginHttpClient clientAuth = null)
        {
            Config = descriptor.Config;
            Descriptor = descriptor;
            _script = script;
            _savedState = savedState;

            _auth = descriptor.GetAuth();
            _captcha = descriptor.GetCaptchaData();

            if (client != null)
                client.SetPlugin(this);
            if (clientAuth != null)
                clientAuth.SetPlugin(this);

            HttpClient = client ?? new PluginHttpClient(this, null, _captcha);
            HttpClientAuth = clientAuth ?? new PluginHttpClient(this, _auth, _captcha);
        }

        public void ReplaceDescriptorSettings(Dictionary<string, string> settings)
        {
            Descriptor.Settings = settings;
        }

        public void UpdateDescriptor(PluginDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        public void TriggerToast(string toast)
        {
            OnToast?.Invoke(Config, toast);
        }

        public V8ScriptEngine? GetUnderlyingEngine()
        {
            return _engine;
        }

        public PluginHttpClient GetHttpClientById(string id)
        {
            if (HttpClient.ClientID == id)
                return HttpClient;
            if (HttpClientAuth.ClientID == id)
                return HttpClientAuth;
            if (HttpClientOthers.ContainsKey(id))
                return HttpClientOthers[id];
            return null;
        }

        public void Initialize()
        {
            _engine = new V8ScriptEngine(V8ScriptEngineFlags.AddPerformanceObject);
            SetupEngine(_engine);
            BeforeInitialize?.Invoke(this);
            _engine.Execute(Resources.ScriptPolyfil);
            _engine.Execute(Resources.ScriptSource);


            PackageBridge bridgePackage = new PackageBridge(this, (log) => OnLog?.Invoke(Config, log));
            bridgePackage.Initialize(_engine);
            _packages.Add(bridgePackage);

            foreach(string package in Config.Packages ?? new List<string>())
            {
                Package pack = GetPackage(package);
                pack.Initialize(_engine);
                _packages.Add(pack);
            }
            foreach (string package in Config.PackagesOptional ?? new List<string>())
            {
                Package pack = GetPackage(package, true);
                if (pack != null)
                {
                    pack.Initialize(_engine);
                    _packages.Add(pack);
                }
            }


            if (!string.IsNullOrEmpty(Config.ScriptPublicKey) && !(Descriptor?.AppSettings?.Advanced?.AllowTamper ?? false))
            {
                if (!Config.VerifySignature(_script))
                    throw new PluginException(Config, "Invalid script signature");
            }

            _engine.Execute(_script);
            Capabilities = new PlatformClientCapabilities()
            {
                HasChannelSearch = (bool)_engine.Evaluate("!!source.searchChannels"),
                HasGetUserSubscriptions = (bool)_engine.Evaluate("!!source.getUserSubscriptions"),
                HasGetComments = (bool)_engine.Evaluate("!!source.getComments"),
                HasSearchSuggestions = (bool)_engine.Evaluate("!!source.searchSuggestions"),
                HasSearchPlaylists = (bool)_engine.Evaluate("!!source.searchPlaylists"),
                HasGetPlaylist = (bool)_engine.Evaluate("!!source.getPlaylist"),
                HasGetUserPlaylists = (bool)_engine.Evaluate("!!source.getUserPlaylists"),
                HasSearchChannelContents = (bool)_engine.Evaluate("!!source.searchChannelContents"),
                HasSaveState = (bool)_engine.Evaluate("!!source.saveState"),
                HasGetPlaybackTracker = (bool)_engine.Evaluate("!!source.getPlaybackTracker"),
                HasGetChannelUrlByClaim = (bool)_engine.Evaluate("!!source.getChannelUrlByClaim"),
                HasGetChannelTemplateByClaimMap = (bool)_engine.Evaluate("!!source.getChannelTemplateByClaimMap"),
                HasGetSearchCapabilities = (bool)_engine.Evaluate("!!source.getSearchCapabilities"),
                HasGetChannelCapabilities = (bool)_engine.Evaluate("!!source.getChannelCapabilities"),
                HasGetLiveEvents = (bool)_engine.Evaluate("!!source.getLiveEvents"),
                HasGetLiveChatWindow = (bool)_engine.Evaluate("!!source.getLiveChatWindow"),
                HasGetContentChapters = (bool)_engine.Evaluate("!!source.getContentChapters"),
                HasPeekChannelContents = (bool)_engine.Evaluate("!!source.peekChannelContents"),
                HasGetContentRecommendations = (bool)_engine.Evaluate("!!source.getContentRecommendations"),
                HasGetChannelPlaylists = (bool)_engine.Evaluate("!!source.getChannelPlaylists")
            };

            _engine.Execute("plugin.config = " + SerializeConfig());
            _engine.Execute("plugin.settings = parseSettings(" + SerializeSettings() + ")");

            _activePlugins.AddOrUpdate(_engine, this, (key, obj) => this);
            IsInitialized = true;
        }

        private void SetupEngine(V8ScriptEngine engine)
        {
            var timeoutIdGenerator = 0;
            var timeouts = new ConcurrentDictionary<int, Timer>();
            engine.Script.___setTimeout = new Func<ScriptObject, int, int>((func, delay) =>
            {
                var id = Interlocked.Increment(ref timeoutIdGenerator);
                var timer = new Timer(_ => {
                    try
                    {
                        timeouts.TryRemove(id, out var _);
                        func.Invoke(false);
                    }
                    catch(Exception ex) 
                    {
                        if (ex is ScriptException scriptEx)
                            OnScriptException?.Invoke(Config, scriptEx);
                        else
                        {
                            Logger.Error<GrayjayPlugin>("Uncaught script exception in timeout callback", ex);
                        }
                    }
                });
                timer.Change(delay, Timeout.Infinite);
                timeouts[id] = timer;
                return id;
            });
            engine.Script.___clearTimeout = new Action<int>((id) =>
            {
                if (timeouts.TryRemove(id, out var timer))
                    timer.Change(Timeout.Infinite, Timeout.Infinite);
            });
            engine.Execute(@"
                function setTimeout(func, delay) {
                    let args = Array.prototype.slice.call(arguments, 2);
                    return ___setTimeout(func.bind(globalThis, ...args), delay || 0);
                }
            ");
            engine.Execute(@"
               function clearTimeout(id) {
                    ___clearTimeout(id);
                }
            ");
        }

        public Package GetPackage(string name, bool allowNull = false)
        {
            switch (name)
            {
                case "Http":
                    return new PackageHttp(this);
                case "Utilities":
                    return new PackageUtilities(this);
                case "DOMParser":
                    return new PackageDOMParser(this);
                case "JSDOM":
                    return new PackageJSDOM(this);

                default:
                    if (allowNull)
                        return null;
                    throw new NotImplementedException($"Package [{name}] is not implemented");
            }
        }
        public Package GetPackageByVariable(string variableName)
        {
            return _packages.FirstOrDefault(x => x.VariableName == variableName);
        }

        public List<string> GetPackageVariables()
        {
            return _packages.Where(x => x.VariableName != null).Select(x => x.VariableName).ToList();
        }

        public void Test()
        {
            Initialize();
            Enable();
        }

        public virtual GrayjayPlugin GetCopy(bool privateCopy = false)
        {
            if(!privateCopy)
                return new GrayjayPlugin(Descriptor, _script, GetSavedState());
            else
                return new GrayjayPlugin(Descriptor.Config, _script, _settings, GetSavedState());
        }

        [JSDocs(0, "enable", "source.enable(...)", "")]
        public virtual void Enable()
        {
            lock (_enableLock)
            {
                if (!IsInitialized)
                    throw new InvalidOperationException("Call Initialize first");
                string config = JsonSerializer.Serialize(Config, new JsonSerializerOptions()
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                string enableCall = $"source.enable({SerializeConfig()}, parseSettings({SerializeSettings()}), {SerializeParameter(_savedState)})";
                Stopwatch w = Stopwatch.StartNew();
                RawExecute(enableCall);
                IsEnabled = true;
                OnLog?.Invoke(Config, $"Enabled in [{w.Elapsed.TotalMilliseconds}ms] {((!string.IsNullOrEmpty(_savedState)) ? "with saved state" : "")}");
                w.Stop();
            }
        }
        public void EnsureEnabled()
        {
            if (!IsEnabled)
                Enable();
        }

        public virtual void Disable()
        {
            try
            {
                OnStopped?.Invoke(this);
            }
            catch(Exception ex) { }
            IsInitialized = false;
            IsEnabled = false;
            _activePlugins.TryRemove(_engine, out _);
            _engine?.Dispose();
            _engine = null;
            foreach (Package package in _packages ?? new List<Package>())
                package.Dispose();
            _packages.Clear();
        }

        public string? GetSavedState()
        {
            if (!Capabilities.HasSaveState)
                return null;

            EnsureEnabled();

            var res = _engine.Evaluate($"source.saveState()");
            if (res is Undefined)
                return null;

            return (string)res;
        }

        [JSDocs(1, "getHome", "source.getHome()", "")]
        public virtual IPager<PlatformContent> GetHome() => WithIsBusy(() =>
        {
            EnsureEnabled();
            return EvaluatePager<PlatformContent>($"source.getHome()", (content) => { content.ID.PluginID = Config.ID; });
        });

        public virtual PlatformPlaylistDetails GetPlaylist(string url) => WithIsBusy(() =>
        {
            EnsureEnabled();
            return EvaluateObject<PlatformPlaylistDetails>($"source.getPlaylist({SerializeParameter(url)})");
        });

        public virtual ResultCapabilities GetSearchCapabilities()
        {
            if (!Capabilities.HasGetSearchCapabilities)
            {
                return new ResultCapabilities()
                {
                    Types = new List<string>() { ResultCapabilities.TYPE_MIXED }
                };
            }

            try
            {
                if (_searchCapabilities == null)
                    _searchCapabilities = EvaluateObject<ResultCapabilities>("source.getSearchCapabilities()");
                return _searchCapabilities;
            }
            catch (Exception ex)
            {
                Logger.Error<GrayjayPlugin>($"Failed [{Config.Name}].getSearchCapabilities", ex);
                return new ResultCapabilities()
                {
                    Types = new List<string>() { ResultCapabilities.TYPE_MIXED }
                };
            }
        }

        [JSDocs(2, "search", "source.search()", "")]
        [JSDocsParameter("query", "", 0)]
        [JSDocsParameter("type", "", 1)]
        [JSDocsParameter("order", "", 2)]
        [JSDocsParameter("filters", "", 3)]
        public virtual IPager<PlatformContent> Search(string query, string? type = null, string? order = null, Dictionary<string, string[]>? filters = null) => WithIsBusy(() =>
        {
            EnsureEnabled();
            return EvaluatePager<PlatformContent>($"source.search({SerializeParameter(query)}, {SerializeParameter(type)}, {SerializeParameter(order)}, {SerializeParameter(filters)})", (content) => { content.ID.PluginID = Config.ID; });
        });

        [JSDocs(3, "searchChannels", "source.searchChannels()", "")]
        [JSDocsParameter("query", "", 0)]
        [JSDocsParameter("type", "", 1)]
        [JSDocsParameter("order", "", 2)]
        [JSDocsParameter("filters", "", 3)]
        public virtual IPager<PlatformAuthorLink> SearchChannels(string query, string? type = null, string? order = null, Dictionary<string, string[]>? filters = null) => WithIsBusy(() =>
        {
            EnsureEnabled();
            return EvaluatePager<PlatformAuthorLink>($"source.searchChannels({SerializeParameter(query)}, {SerializeParameter(type)}, {SerializeParameter(order)}, {SerializeParameter(filters)})", (content) => { content.ID.PluginID = Config.ID; });
        });
        public virtual IPager<PlatformContent> SearchChannelsAsContent(string query) => WithIsBusy(() =>
        {
            EnsureEnabled();
            return EvaluatePager<PlatformAuthorLink, PlatformContent>($"source.searchChannels({SerializeParameter(query)})", (content) =>
            {
                content.ID.PluginID = Config.ID;
                return new PlatformAuthorContent(content);
            });
        });


        [JSDocs(4, "searchPlaylists", "source.searchPlaylists()", "")]
        [JSDocsParameter("query", "", 0)]
        public virtual IPager<PlatformContent> SearchPlaylists(string query) => WithIsBusy(() =>
        {
            EnsureEnabled();
            return EvaluatePager<PlatformContent>($"source.searchPlaylists({SerializeParameter(query)})", (content) => { content.ID.PluginID = Config.ID; });
        });

        public virtual List<string> SearchSuggestions(string query) => WithIsBusy(() =>
        {
            EnsureEnabled();
            return EvaluateObject<List<string>>($"source.searchSuggestions({SerializeParameter(query)})");
        });

        [JSDocs(5, "isContentDetailsUrl", "source.isContentDetailsUrl()", "")]
        [JSDocsParameter("url", "", 0)]
        public virtual bool IsContentDetailsUrl(string url)
        {
            EnsureEnabled();
            return (bool)_engine.Evaluate($"source.isContentDetailsUrl({SerializeParameter(url)})");
        }
        [JSDocs(6, "getContentDetails", "source.getContentDetails()", "")]
        [JSDocsParameter("url", "", 0)]
        public virtual IPlatformContentDetails GetContentDetails(string url) => WithIsBusy(() =>
        {
            EnsureEnabled();
            var result = EvaluateObject<IPlatformContentDetails>($"source.getContentDetails({SerializeParameter(url)})");
            result.ID.PluginID = Config.ID;
            return result;
        });
        [JSDocs(7, "getContentChapters", "source.getContentChapters()", "")]
        [JSDocsParameter("url", "", 0)]
        public virtual List<Chapter> GetContentChapters(string url) => WithIsBusy(() =>
        {
            if (!Capabilities.HasGetContentChapters)
                return new List<Chapter>();
            EnsureEnabled();
            return EvaluateObject<List<Chapter>>($"source.getContentChapters({SerializeParameter(url)})");
        });

        public virtual LiveChatWindowDescriptor GetLiveChatWindow(string url)
        {
            if (!Capabilities.HasGetLiveChatWindow)
                return null;
            EnsureEnabled();
            return EvaluateObject<LiveChatWindowDescriptor>($"source.getLiveChatWindow({SerializeParameter(url)})");
        }


        [JSDocs(8, "getContentRecommendations", "source.getContentRecommendations()", "")]
        [JSDocsParameter("url", "", 0)]
        public virtual IPager<PlatformContent> getContentRecommendations(string url)
        {
            if (!Capabilities.HasGetContentRecommendations)
                return null;
            EnsureEnabled();
            return EvaluatePager<PlatformContent>($"source.getContentRecommendations({SerializeParameter(url)})", (content) =>
            {
                content.ID.PluginID = Config.ID;
            });
        }



        [JSDocs(8, "isChannelUrl", "source.isChannelUrl()", "")]
        [JSDocsParameter("url", "", 0)]
        public virtual bool IsChannelUrl(string url)
            => (bool)_engine.Evaluate($"source.isChannelUrl({SerializeParameter(url)})");
        [JSDocs(9, "isPlaylistUrl", "source.isPlaylistUrl()", "")]
        [JSDocsParameter("url", "", 0)]
        public virtual bool IsPlaylistUrl(string url)
        {
            if (!Capabilities.HasGetPlaylist)
                return false;
            return (bool)_engine.Evaluate($"source.isPlaylistUrl({SerializeParameter(url)})");
        }
        [JSDocs(10, "getChannel", "source.getChannel()", "")]
        [JSDocsParameter("url", "", 0)]
        public virtual PlatformChannel GetChannel(string url) => WithIsBusy(() =>
        {
            EnsureEnabled();
            var channel = EvaluateObject<PlatformChannel>($"source.getChannel({SerializeParameter(url)})");
            channel.ID.PluginID = Config.ID;
            return channel;
        });

        public virtual ResultCapabilities GetChannelCapabilities()
        {
            if (!Capabilities.HasGetChannelCapabilities)
                return new ResultCapabilities()
                {
                    Types = new List<string>() { ResultCapabilities.TYPE_MIXED }
                };
            try
            {
                if (_channelCapabilities == null)
                    _channelCapabilities = EvaluateObject<ResultCapabilities>("source.getChannelCapabilities()");
                return _channelCapabilities;
            }
            catch(Exception ex)
            {
                Logger.Error<GrayjayPlugin>($"Failed [{Config.Name}].getChannelCapabilities", ex);
                return new ResultCapabilities()
                {
                    Types = new List<string>() { ResultCapabilities.TYPE_MIXED }
                };
            }
        }
        [JSDocs(11, "searchChannelContents", "source.searchChannelContents()", "")]
        [JSDocsParameter("channelUrl", "", 0)]
        [JSDocsParameter("query", "", 1)]
        public virtual IPager<PlatformContent> SearchChannelContents(string channelUrl, string query) => WithIsBusy(() =>
        {
            EnsureEnabled();
            return EvaluatePager<PlatformContent>($"source.searchChannelContents({SerializeParameter(channelUrl)}, {SerializeParameter(query)})", (content) =>
            {
                content.ID.PluginID = Config.ID;
            });
        });
        [JSDocs(12, "getChannelContents", "source.getChannelContents()", "")]
        [JSDocsParameter("channelUrl", "", 0)]
        [JSDocsParameter("type", "", 1)]
        [JSDocsParameter("order", "", 2)]
        [JSDocsParameter("filters", "", 3)]
        public virtual IPager<PlatformContent> GetChannelContents(string channelUrl, string? type = null, string? order = null, Dictionary<string, List<string>>? filters = null) => WithIsBusy(() =>
        {
            EnsureEnabled();
            return EvaluatePager<PlatformContent>($"source.getChannelContents({SerializeParameter(channelUrl)}, {SerializeParameter(type)}, {SerializeParameter(order)}, {SerializeParameter(filters)})", (content) =>
            {
                content.ID.PluginID = Config.ID;
            });
        });
        [JSDocs(12, "getChannelPlaylists", "source.getChannelPlaylists()", "")]
        [JSDocsParameter("channelUrl", "", 0)]
        public virtual IPager<PlatformPlaylist> getChannelPlaylists(string channelUrl) => WithIsBusy(() =>
        {
            EnsureEnabled();
            if (!Capabilities.HasGetChannelPlaylists)
                return null;
            return EvaluatePager<PlatformPlaylist>($"source.getChannelPlaylists({SerializeParameter(channelUrl)})", (content) =>
            {
                content.ID.PluginID = Config.ID;
            });
        });

        public virtual List<string> GetPeekChannelTypes()
        {
            if (!Capabilities.HasPeekChannelContents)
                return new List<string>();
            try
            {
                if (_peekChannelTypes != null)
                    return _peekChannelTypes;
                var arr = EvaluateObject<List<string>>("source.getPeekChannelTypes()");
                _peekChannelTypes = arr;
                return arr ?? new List<string>();
            }
            catch(Exception ex)
            {
                Logger.Error<GrayjayPlugin>("Peek channel contents getTypes failed", ex);
                return new List<string>();
            }
        }
        [JSDocs(13, "peekChannelContents", "source.peekChannelContents()", "")]
        [JSDocsParameter("channelUrl", "", 0)]
        [JSDocsParameter("type", "", 1)]
        public virtual List<PlatformContent> PeekChannelContents(string channelUrl, string type) => WithIsBusy(() =>
        {
            EnsureEnabled();
            return EvaluateObject<List<PlatformContent>>($"source.peekChannelContents({SerializeParameter(channelUrl)}, {SerializeParameter(type)})");
        });

        [JSDocs(14, "getComments", "source.getcomments()", "")]
        [JSDocsParameter("url", "", 0)]
        public virtual IPager<PlatformComment> GetComments(string url) => WithIsBusy(() =>
        {
            EnsureEnabled();
            return EvaluatePager<PlatformComment>($"source.getComments({SerializeParameter(url)})", (content) => { content.Author.ID.PluginID = Config.ID; });
        });

        public virtual IPager<PlatformComment> GetSubComments(PlatformComment comment) => WithIsBusy(() =>
        {
            EnsureEnabled();
            return comment.GetReplies() ?? EvaluatePager<PlatformComment>($"source.getSubComments({SerializeParameter(comment)})", (content) => { content.Author.ID.PluginID = Config.ID; });
        });

        public virtual PlaybackTracker? GetPlaybackTracker(string url) => WithIsBusy(() =>
        {
            EnsureEnabled();
            if (!Capabilities.HasGetPlaybackTracker)
                return null;
            return EvaluateObject<PlaybackTracker>($"source.getPlaybackTracker({SerializeParameter(url)})", true);
        });


        [JSDocs(15, "getUserSubscriptions", "source.getUserSubscriptions()", "")]
        public virtual List<string> GetUserSubscriptions() => WithIsBusy(() =>
        {
            if (!Capabilities.HasGetUserSubscriptions)
                return null;
            EnsureEnabled();
            return EvaluateObject<List<string>>($"source.getUserSubscriptions()");
        });
        [JSDocs(15, "getUserPlaylists", "source.getUserPlaylists()", "")]
        public virtual List<string> GetUserPlaylists() => WithIsBusy(() =>
        {
            if (!Capabilities.HasGetUserPlaylists)
                return null;
            EnsureEnabled();
            return EvaluateObject<List<string>>($"source.getUserPlaylists()");
        });



        public int GetSubscriptionRateLimit()
        {
            var pluginRateLimit = Config.SubscriptionRateLimit;
            var settingsRateLimit = Descriptor.AppSettings.RateLimit.GetSubRateLimit();
            if (settingsRateLimit > 0)
            {
                if (pluginRateLimit > 0)
                    return Math.Min(pluginRateLimit, settingsRateLimit);
                else
                    return settingsRateLimit;

            }
            else
                return pluginRateLimit;
        }

        public T InterceptExceptions<T>(Func<T> act, string script = "")
        {
            try
            {
                return act();
            }
            catch (ScriptEngineException ex)
            {
                if(ex.ScriptExceptionAsObject != null && ex.ScriptExceptionAsObject is IJavaScriptObject jex)
                {
                    if(jex.PropertyNames.Contains("plugin_type"))
                    {
                        string pluginType = (string)jex.GetProperty("plugin_type");
                        ScriptException scriptEx;
                        switch(pluginType)
                        {
                            case "CaptchaRequiredException":
                                scriptEx = ScriptCaptchaRequiredException.FromV8(this, ex, jex);
                                break;
                            default:
                                scriptEx = GetExceptionFromV8(pluginType, ExtractJSExceptionMessage(ex, jex));
                                break;
                        }
                        if(scriptEx != null)
                        {
                            OnScriptException?.Invoke(Config, scriptEx);
                            throw scriptEx;
                        }
                    }
                }
                string codeStripped = script;
                if (codeStripped != null)
                {
                    if (codeStripped.Contains("(") && codeStripped.Contains(")"))
                    {
                        int start = codeStripped.IndexOf("(");
                        int end = codeStripped.LastIndexOf(")");
                        codeStripped = codeStripped.Substring(0, start) + "(...)" + codeStripped.Substring(end + 1);
                    }
                }
                string stack = (ex.ErrorDetails.StartsWith(ex.Message) ? ex.ErrorDetails.Substring(ex.Message.Length).Trim() : null);
                throw new ScriptException(Config,
                    ((ex.Message.StartsWith("Error: ")) ? ex.Message.Substring("Error: ".Length) : ex.Message),
                    ex, stack, codeStripped);
            }
        }

        private string ExtractJSExceptionMessage(ScriptEngineException ex, IJavaScriptObject obj)
        {
            string lineInfo = $" (Unknown)[Unknown-Unknown]";
            if (!string.IsNullOrEmpty(ex.ErrorDetails))
            {
                return ex.ErrorDetails;
            }
            else
                return ex.Message;
        }

        private static Regex _regexScriptMessage = new Regex("Error:(.*?)\\n\\s*?at .*?\\[([0-9]*)\\]:([0-9]*):([0-9]*)");
        private static Regex _regexScriptStacktrace = new Regex("Error:(.*?)\\n\\s*?(at .*)");
        private ScriptException GetExceptionFromV8(string pluginType, string msg, Exception innerEx = null, string stack = null, string code = null)
        {
            Match m = _regexScriptMessage.Match(msg);
            int line = -1;
            int col = -1;
            if (m.Success)
            {
                msg = m.Groups[1].Value.Trim();
                var u = m.Groups[2].Value;
                if (int.TryParse(m.Groups[3].Value, out line))
                    if (int.TryParse(m.Groups[4].Value, out col))
                        msg = msg + $" (Line {line}, Col {col})";
                Match stackRegex = _regexScriptStacktrace.Match(msg);
                if (stackRegex.Success)
                    stack = stackRegex.Groups[1].Value.Trim();
            }
            switch(pluginType)
            {
                case "ScriptException":
                    return new ScriptException(Config, msg, innerEx, stack, code);
                case "CriticalException":
                    return new ScriptCriticalException(Config, msg, innerEx, stack, code);
                case "AgeException":
                    return new ScriptException(Config, msg, innerEx, stack, code);
                case "UnavailableException":
                    return new ScriptUnavailableException(Config, msg, innerEx, stack, code);
                case "ScriptLoginRequiredException":
                    return new ScriptLoginRequiredException(Config, msg, innerEx, stack, code);
                case "ScriptExecutionException":
                    return new ScriptException(Config, msg, innerEx, stack, code);
                case "ScriptCompilationException":
                    return new ScriptException(Config, msg, innerEx, stack, code);
                case "ScriptImplementationException":
                    return new ScriptException(Config, msg, innerEx, stack, code);
                case "ScriptTimeoutException":
                    return new ScriptException(Config, msg, innerEx, stack, code);
                case "NoInternetException":
                    return new ScriptException(Config, msg, innerEx, stack, code);
                default:
                    return new ScriptException(Config, msg, innerEx, stack, code);

            }
        }

        public void RawExecute(string script)
        {
            InterceptExceptions<object>(() =>
            {
                _engine.Execute(script);
                return null;
            });
        }
        public dynamic RawEvaluate(string script)
        {
            return InterceptExceptions<dynamic>(() =>
            {
                return _engine.Evaluate(script);
            });
        }


        private IJavaScriptObject EvaluateRawObject(string script, bool nullable = false)
        {

            return InterceptExceptions<IJavaScriptObject>(() =>
            {
                var obj = (IJavaScriptObject)_engine.Evaluate(script);
                if (obj == null && nullable)
                    return obj;
                if (!(obj is IJavaScriptObject))
                    throw new InvalidCastException($"Found {obj?.GetType()?.Name}, expected IJavaScriptObject");
                return obj;
            }, script);
        }
        private T EvaluateObject<T>(string js, bool nullable = false)
        {
            var obj = EvaluateRawObject(js, nullable);
            if (obj == null && nullable)
                return default(T);
            return V8Converter.ConvertValue<T>(this, obj);
        }
        private IPager<T> EvaluatePager<T>(string js, Action<T> modifier = null)
        {
            var obj = EvaluateRawObject(js);
            return new V8Pager<T>(this, obj, modifier);
        }
        private IPager<R> EvaluatePager<T, R>(string js, Func<T, R> modifier)
        {
            var obj = EvaluateRawObject(js);
            return new V8Pager<T, R>(this, obj, modifier);
        }

        public void ValidateUrlOrThrow(string url)
        {
            var allowed = Config.IsUrlAllowed(url);
            if (!allowed)
                throw new ScriptException(Config, "Attempted to access non-whitelisted url: " + url);
        }


        private T WithIsBusy<T>(Func<T> work)
        {
            Stopwatch sw = (OnBusyCallEnd != null) ? Stopwatch.StartNew() : null;
            string caller = (OnBusyCallStart != null || OnBusyCallEnd != null) ? (new System.Diagnostics.StackTrace()).GetFrame(1).GetMethod().Name : null;

            try
            {
                if (OnBusyCallStart != null) OnBusyCallStart?.Invoke(Config, caller);

                lock (_busyLock)
                {
                    _busyCount++;
                }
                return work();
            }
            finally
            {
                lock(_busyLock)
                {
                    _busyCount--;
                }

                if (OnBusyCallEnd != null) OnBusyCallEnd?.Invoke(Config, caller, (long)sw.Elapsed.TotalMilliseconds);
                sw?.Stop();
            }
        }

        public void Dispose()
        {
            Disable();
        }


        private string SerializeConfig()
        {
            return JsonSerializer.Serialize(Config, new JsonSerializerOptions()
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        private string SerializeSettings()
        {
            return SerializeParameter(_settings.ToDictionary(x => x.Key, y =>
            {
                if (y.Value == "")
                    return null;
                //TODO: Workaround..
                if (y.Value == "True")
                    return "true";
                if (y.Value == "False")
                    return "false";
                return y.Value;
            }));
        }
        private static string SerializeParameter(object? obj)
        {
            if (obj == null)
                return "null";
            string parameterJson = JsonSerializer.Serialize(obj, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return parameterJson;
        }

        public static GrayjayPlugin FromFiles(string configPath, string scriptPath, Dictionary<string, string> settings = null)
        {
            PluginConfig config = JsonSerializer.Deserialize<PluginConfig>(File.ReadAllText(configPath), new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });
            string script = File.ReadAllText(scriptPath);

            return new GrayjayPlugin(config, script, settings);
        }
        public static GrayjayPlugin FromUrl(string configUrl)
        {
            PluginConfig config = null;

            using (WebClient client = new WebClient())
            {
                string configJson = client.DownloadString(configUrl);
                if (string.IsNullOrEmpty(configJson.Trim()))
                    throw new ArgumentException($"No config found on url {configUrl}");
                config = JsonSerializer.Deserialize<PluginConfig>(configJson, new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true
                })!;
            }

            return FromConfig(config);
        }
        public static GrayjayPlugin FromConfig(PluginConfig config)
        {
            if (config.ScriptUrl == null)
                throw new ArgumentException("No script url configured");

            string scriptUrl = config.ScriptUrl;
            if (scriptUrl.StartsWith(".")) //TODO: proper parsing
                scriptUrl = config.AbsoluteScriptUrl;

            string script = null;
            using (WebClient client = new WebClient())
                script = client.DownloadString(scriptUrl);

            return new GrayjayPlugin(config, script);
        }


        public static GrayjayPlugin GetObjectPlugin(IJavaScriptObject obj)
        {
            return GetObjectPlugin(obj);
        }
        public static GrayjayPlugin GetEnginePlugin(ScriptEngine engine)
        {
            GrayjayPlugin plugin = null;
            if (_activePlugins.TryGetValue(engine, out plugin))
                return plugin;
            return null;
        }

        public static List<JSCallDocs> GetJSDocs()
        {
            return typeof(GrayjayPlugin).GetMethods().Select(x =>
            {
                var attr = x.GetCustomAttribute<JSDocsAttribute>();
                var parameters = x.GetCustomAttributes<JSDocsParameterAttribute>().OrderBy(x => x.Order).ToArray();

                return (x, attr, parameters);
            }).Where(x => x.attr != null)
            .OrderBy(x => x.attr.Order)
            .Select(y => new JSCallDocs(y.attr?.Name ?? y.x.Name, y.attr.Code, y.attr.Description, 
                y.parameters?.Select(z=>new JSParameterDocs(z.Name, z.Description))?.ToList() ?? new List<JSParameterDocs>(), 
                y.x.GetCustomAttribute<JSOptionalAttribute>() != null, null))
            .ToList();
        }

        [Serializable]
        public class JSCallDocs
        {
            public string Title { get; set; }
            public string Code { get; set; }
            public string Description { get; set; }
            public List<JSParameterDocs> Parameters { get; set; }
            public bool IsOptional { get; set; } = false;
            public string DocsUrl { get; set; } = null;

            public JSCallDocs(string title, string code, string description, List<JSParameterDocs> parameters, bool isOptional = false, string docsUrl = null)
            {
                Title = title;
                Code = code;
                Description = description;
                Parameters = parameters;
                IsOptional = isOptional;
                DocsUrl = docsUrl;
            }
        }

        [Serializable]
        public class JSParameterDocs
        {
            public string Name { get; set; }
            public string Description { get; set; }

            public JSParameterDocs(string name, string description)
            {
                Name = name;
                Description = description;
            }
        }

        public class PlatformClientCapabilities
        {
            public bool HasChannelSearch { get; set; }
            public bool HasGetComments { get; set; }
            public bool HasGetUserSubscriptions { get; set; }
            public bool HasSearchSuggestions { get; set; }
            public bool HasSearchPlaylists { get; set; }
            public bool HasGetPlaylist { get; set; }
            public bool HasGetUserPlaylists { get; set; }
            public bool HasSearchChannelContents { get; set; }
            public bool HasSaveState { get; set; }
            public bool HasGetPlaybackTracker { get; set; }
            public bool HasGetChannelUrlByClaim { get; set; }
            public bool HasGetChannelTemplateByClaimMap { get; set; }
            public bool HasGetSearchCapabilities { get; set; }
            public bool HasGetChannelCapabilities { get; set; }
            public bool HasGetLiveEvents { get; set; }
            public bool HasGetLiveChatWindow { get; set; }
            public bool HasGetContentChapters { get; set; }
            public bool HasPeekChannelContents { get; set; }
            public bool HasGetContentRecommendations { get; set; }
            public bool HasGetChannelPlaylists { get; set; }
        }
    }

    public class PluginHttpClient: ManagedHttpClient
    {
        private GrayjayPlugin _plugin;
        private ManagedHttpClient _client = new ManagedHttpClient();
        private SourceAuth _auth;
        private SourceCaptcha _captcha;


        public string ClientID { get; } = Guid.NewGuid().ToString();

        public bool DoUpdateCookies { get; set; } = true;
        public bool DoApplyCookies { get; set; } = true;
        public bool DoAllowNewCookies { get; set; } = true;

        public bool IsLoggedIn => _auth != null;

        private Dictionary<string, Dictionary<string, string>> _currentCookieMap = null;
        private Dictionary<string, Dictionary<string, string>> _otherCookieMap = null;

        public void SetPlugin(GrayjayPlugin plugin)
        {
            _plugin = plugin;
        }

        public PluginHttpClient(GrayjayPlugin plugin, SourceAuth auth = null, SourceCaptcha captcha = null)
        {
            _plugin = plugin;
            _auth = auth;
            _captcha = captcha;

            _currentCookieMap = new Dictionary<string, Dictionary<string, string>>();
            _otherCookieMap = new Dictionary<string, Dictionary<string, string>>();
            if (auth?.CookieMap?.Any() ?? false)
            {
                foreach (var domainCookies in auth.CookieMap)
                    _currentCookieMap[domainCookies.Key] = new Dictionary<string, string>(domainCookies.Value);
            }
            if (captcha?.CookieMap?.Any() ?? false)
            {
                foreach (var domainCookies in captcha.CookieMap)
                {
                    if (_currentCookieMap.ContainsKey(domainCookies.Key))
                    {
                        foreach (var cookie in domainCookies.Value)
                            _currentCookieMap[domainCookies.Key][cookie.Key] = cookie.Value;
                    }
                    else
                        _currentCookieMap[domainCookies.Key] = new Dictionary<string, string>(domainCookies.Value);
                }
            }
        }


        public override void BeforeRequest(HttpRequestMessage request)
        {
            var domain = request.RequestUri.Host.ToLower();
            var auth = _auth;

            if (auth != null)
            {
                foreach (var header in auth.Headers.Where(x => domain.MatchesDomain(x.Key)).SelectMany(x => x.Value).ToList())
                {
                    if (request.Headers.Contains(header.Key))
                        request.Headers.Remove(header.Key);
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            if (DoApplyCookies)
            {
                if (_currentCookieMap.Any())
                {
                    var cookiesToApply = new Dictionary<string, string>();
                    lock (_currentCookieMap)
                    {
                        foreach (var cookie in _currentCookieMap.Where(x => domain.MatchesDomain(x.Key)).SelectMany(x => x.Value))
                            cookiesToApply[cookie.Key] = cookie.Value;
                    }

                    if (cookiesToApply?.Any() ?? false)
                    {
                        var cookieString = string.Join("; ", cookiesToApply.Select(x => x.Key + "=" + x.Value));

                        var existingCookies = (request.Headers.Contains("Cookie")) ? request.Headers.GetValues("Cookie") : null;
                        if (existingCookies?.Any() ?? false)
                        {
                            request.Headers.Remove("Cookie");
                            request.Headers.Add("Cookie", existingCookies.Concat(cookieString.Split(";")));
                        }
                        else
                            request.Headers.Add("Cookie", cookieString);
                        /*
                        var existingCookies = request.Headers[HttpRequestHeader.Cookie];
                        if (existingCookies?.Any() ?? false)
                            request.Headers[HttpRequestHeader.Cookie] = existingCookies.Trim(';') + ";" + cookieString;
                        else
                            request.Headers[HttpRequestHeader.Cookie] = cookieString;
                        */
                    }
                }
            }

            if (_plugin != null)
                _plugin.ValidateUrlOrThrow(request.RequestUri.AbsoluteUri);
            else
                throw new NotImplementedException();
        }

        
        public void ApplyHeaders(Uri url, Dictionary<string, string> headers, bool applyAuth = false, bool applyOtherCookies = false)
        {
            var domain = url.Host.ToLower();
            var auth = _auth;

            if (applyAuth && auth != null)
            {
                foreach (var header in auth.Headers.Where(x => domain.MatchesDomain(x.Key)).SelectMany(x => x.Value).ToList())
                {
                    if (headers.ContainsKey(header.Key))
                        headers.Remove(header.Key);
                    headers.Add(header.Key, header.Value);
                }
            }

            if (DoApplyCookies && (applyAuth || applyOtherCookies))
            {
                if (_currentCookieMap.Any())
                {
                    var cookiesToApply = new Dictionary<string, string>();

                    if(applyOtherCookies)
                    {
                        lock(_otherCookieMap)
                        {
                            foreach (var cookie in _otherCookieMap.Where(x => domain.MatchesDomain(x.Key)).SelectMany(x => x.Value))
                                cookiesToApply[cookie.Key] = cookie.Value;
                        }
                    }

                    if (applyAuth)
                    {
                        lock (_currentCookieMap)
                        {
                            foreach (var cookie in _currentCookieMap.Where(x => domain.MatchesDomain(x.Key)).SelectMany(x => x.Value))
                                cookiesToApply[cookie.Key] = cookie.Value;
                        }
                    }

                    if (cookiesToApply?.Any() ?? false)
                    {
                        var cookieString = string.Join("; ", cookiesToApply.Select(x => x.Key + "=" + x.Value));

                        var existingCookies = (headers.ContainsKey("Cookie")) ? headers["Cookie"] : null;
                        if (!string.IsNullOrEmpty(existingCookies))
                        {
                            headers.Remove("Cookie");
                            headers.Add("Cookie", existingCookies + ";" + cookieString);
                        }
                        else
                            headers.Add("Cookie", cookieString);
                        /*
                        var existingCookies = request.Headers[HttpRequestHeader.Cookie];
                        if (existingCookies?.Any() ?? false)
                            request.Headers[HttpRequestHeader.Cookie] = existingCookies.Trim(';') + ";" + cookieString;
                        else
                            request.Headers[HttpRequestHeader.Cookie] = cookieString;
                        */
                    }
                }
            }
        }
        

        public override void AfterRequest(HttpResponseMessage response)
        {
            if (DoUpdateCookies)
            {
                var domain = response.RequestMessage.RequestUri.Host.ToLower();
                var domainParts = domain.Split(".");
                var defaultCookieDomain = "." + string.Join(".", domainParts.Skip(domainParts.Length - 2));
                foreach(var header in response.Headers)
                {
                    if(header.Key.ToLower() == "set-cookie")
                    {
                        if(header.Key.ToLower() == "set-cookie")
                        {
                            var domainToUse = domain;
                            var str = header.Value.FirstOrDefault();
                            if (string.IsNullOrEmpty(str))
                                continue;
                            (var cookieKey, var cookieValue) = CookieStringToPair(str);
                            if(!string.IsNullOrEmpty(cookieKey) && !string.IsNullOrEmpty(cookieValue))
                            {
                                var cookieParts = cookieValue.Split(";");
                                if (cookieParts.Length == 0)
                                    continue;
                                cookieValue = cookieParts[0].Trim();
                                var cookieVariables = cookieParts.Skip(1).Select((it) =>
                                {
                                    var splitIndex = it.IndexOf("=");
                                    if (splitIndex < 0)
                                        return (it.Trim().ToLower(), "");
                                    return (it.Substring(0, splitIndex).ToLower().Trim(), it.Substring(splitIndex + 1).Trim());
                                }).ToDictionary(x => x.Item1, y => y.Item2);
                                domainToUse = (cookieVariables.ContainsKey("domain")) ? cookieVariables["domain"].ToLower() : defaultCookieDomain;
                                if(!domainToUse.StartsWith("."))
                                    domainToUse = "." + domainToUse;
                            }

                            if((_auth != null || _currentCookieMap.Count != 0))
                            {
                                Dictionary<string, string> cookieMap;
                                if (_currentCookieMap.ContainsKey(domainToUse))
                                    cookieMap = _currentCookieMap[domainToUse];
                                else
                                {
                                    var newMap = new Dictionary<string, string>();
                                    _currentCookieMap[domainToUse] = newMap;
                                    cookieMap = newMap;
                                }
                                if(cookieMap.ContainsKey(cookieKey) || DoAllowNewCookies)
                                    cookieMap[cookieKey] = cookieValue;
                            }
                            else
                            {
                                Dictionary<string, string> cookieMap;
                                if (_currentCookieMap.ContainsKey(domainToUse))
                                    cookieMap = _otherCookieMap[domainToUse];
                                else
                                {
                                    var newMap = new Dictionary<string, string>();
                                    _otherCookieMap[domainToUse] = newMap;
                                    cookieMap = newMap;
                                }
                                if(cookieMap.ContainsKey(cookieKey) || DoAllowNewCookies)
                                {
                                    cookieMap[cookieKey] = cookieValue;
                                }
                            }
                        }
                    }
                }
            }
        }
        private static (string, string) CookieStringToPair(string cookie)
        {
            var cookieKey = cookie.Substring(0, cookie.IndexOf("="));
            var cookieVal = cookie.Substring(cookie.IndexOf("=") + 1);
            return (cookieKey.Trim(), cookieVal.Trim());
        }

    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class JSDocsAttribute : Attribute
    {
        public int Order { get; }
        public string Name { get; set; }
        public string Code { get; }
        public string Description { get; }

        public JSDocsAttribute(int order, string name, string code, string description)
        {
            Order = order;
            Name = name;
            Code = code;
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class JSOptionalAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public class JSDocsParameterAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public int Order { get; }

        public JSDocsParameterAttribute(string name, string description, int order = 0)
        {
            Name = name;
            Description = description;
            Order = order;
        }
    }
}
