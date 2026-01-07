using Grayjay.Engine.Exceptions;
using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grayjay.Engine.Models.Video.Additions
{
    public interface IRequestModifier
    {
        bool AllowByteSkip { get; }

        Request ModifyRequest(string url, HttpHeaders headers);
    }

    public class RequestModifier : IRequestModifier
    {
        private GrayjayPlugin _plugin;
        private IJavaScriptObject _modifier;

        [V8Property("allowByteSkip", true)]
        public bool AllowByteSkip { get; set; }

        public RequestModifier(GrayjayPlugin plugin, IJavaScriptObject obj)
        {
            _plugin = plugin;
            _modifier = obj;

            if (!obj.HasFunction("modifyRequest"))
                throw new ScriptImplementationException(plugin.Config, "RequestModifier is missing modifyRequest");
        }

        public Request ModifyRequest(string url, HttpHeaders headers)
        {
            if (_modifier == null)
                return new Request(_plugin, url, headers);

            //TODO: This needs to be List<KeyValuePair<string, string>>
            var result = _modifier.InvokeV8(_plugin.Config, "modifyRequest", url, headers.ToDictionaryLastWins().ToPropertyBag<string>());
            return V8Converter.ConvertValue<Request>(_plugin, result);
        }
    }

    public class AdhocRequestModifier: IRequestModifier
    {

        public bool AllowByteSkip { get; set; }

        private Func<string, HttpHeaders, Request> _modifier = null;

        public AdhocRequestModifier(Func<string, HttpHeaders, Request> func)
        {
            _modifier = func;
        }

        public Request ModifyRequest(string url, HttpHeaders headers)
        {
            return _modifier(url, headers);
        }
    }

    public class Request
    {
        public string Url { get; set; }
        public HttpHeaders Headers { get; set; }
        public Options Options { get; set; }


        public Request(GrayjayPlugin plugin, IJavaScriptObject obj)
        {
            Url = obj.GetOrDefault<string>(plugin, "url", nameof(Request), null);
            //TODO: This needs to be List<KeyValuePair<string, string>>
            var headers = obj.GetOrDefault<Dictionary<string, string>>(plugin, "headers", nameof(Request), null);
            Headers = headers != null ? new HttpHeaders(headers) : new HttpHeaders();
            Options = obj.GetOrDefault<Options>(plugin, "options", nameof(Request), null);
            if (Options == null)
                Options = new Options()
                {
                    ApplyOtherHeaders = true
                };

            Initialize(plugin, null, null);
        }

        public Request(GrayjayPlugin plugin, string url, HttpHeaders headers, Options options = null, string originalUrl = null, HttpHeaders originalHeaders = null, bool applyOtherHeadersByDefault = false)
        {
            Url = url;
            Headers = headers;
            Options = options;
            if (Options == null)
                Options = new Options()
                {
                    ApplyOtherHeaders = applyOtherHeadersByDefault
                };

            Initialize(plugin, originalUrl, originalHeaders);
        }


        public void Initialize(GrayjayPlugin plugin, string originalUrl, HttpHeaders originalHeaders)
        {
            var config = plugin.Config;

            if (Options != null)
            {
                if (Options?.ApplyOtherHeaders ?? false)
                {
                    var headersToSet = new HttpHeaders(Headers);
                    if (originalHeaders != null)
                    {
                        foreach (var header in originalHeaders)
                        {
                            var existing = headersToSet.FirstOrDefault(x => x.Key.Equals(header.Key, StringComparison.OrdinalIgnoreCase));
                            if (string.IsNullOrEmpty(existing.Key))
                                headersToSet[header.Key] = header.Value;
                        }
                        Headers = headersToSet;
                    }
                    else
                        Headers = Headers ?? originalHeaders ?? new HttpHeaders();
                }

                if (Options.ApplyCookieClient != null && Url != null)
                {
                    var client = plugin.GetHttpClientById(Options.ApplyCookieClient);
                    if (client != null)
                    {
                        var toModifyHeaders = new HttpHeaders(Headers);
                        client.ApplyHeaders(new Uri(Url), toModifyHeaders, false, true);
                        Headers = toModifyHeaders;
                    }
                }
            }
        }


        public Request Modify(GrayjayPlugin plugin, string originalUrl, HttpHeaders originalHeaders)
        {
            return new Request(plugin, Url ?? originalUrl, Headers ?? originalHeaders, Options, originalUrl, originalHeaders, true);
        }
    }

    public interface IModifierOptions
    {
        string ApplyAuthClient { get; }
        string ApplyCookieClient { get; }

        bool ApplyOtherHeaders { get; }
    }
    public class Options : IModifierOptions
    {
        [V8Property("applyAuthClient")]
        public string ApplyAuthClient { get; set; }
        [V8Property("applyCookieClient")]
        public string ApplyCookieClient { get; set; }
        [V8Property("applyOtherHeaders")]
        public bool ApplyOtherHeaders { get; set; }
        [V8Property("impersonateTarget")]
        public string? ImpersonateTarget { get; set; }
    }
}
