using Grayjay.Engine.Models;
using Grayjay.Engine.Models.Video.Additions;

namespace Grayjay.Engine.Tests
{
    [TestClass]
    public class RequestModifierTests
    {
        private const string KeyUrl = "https://www.example.com/api/key?id=abc";
        private const string CdnUrl = "https://cdn.example.net/segment0.ts";

        private static GrayjayPlugin CreateLoggedInPlugin()
        {
            var config = new PluginConfig { ID = Guid.NewGuid().ToString(), Name = "Test" };
            var auth = new SourceAuth
            {
                CookieMap = new Dictionary<string, Dictionary<string, string>>
                {
                    [".example.com"] = new Dictionary<string, string> { ["session_id"] = "session-value" }
                },
                Headers = new Dictionary<string, Dictionary<string, string>>
                {
                    ["www.example.com"] = new Dictionary<string, string> { ["X-Auth-Test"] = "auth-header-value" }
                }
            };
            var descriptor = new PluginDescriptor(config);
            var authClient = new PluginHttpClient(null, auth);
            return new GrayjayPlugin(descriptor, "", clientAuth: authClient);
        }

        private static Request ModifyLikeJSSource(GrayjayPlugin plugin, Options options, string url, HttpHeaders originalHeaders)
        {
            var parsed = new Request(plugin, null, null, options);
            return parsed.Modify(plugin, url, originalHeaders);
        }

        private static HttpHeaders OriginalHeaders()
        {
            return new HttpHeaders(new Dictionary<string, string> { ["User-Agent"] = "test-agent" });
        }

        [TestMethod]
        public void ApplyAuthClientAddsAuthCookieAndHeadersForMatchingDomain()
        {
            using var plugin = CreateLoggedInPlugin();
            var options = new Options { ApplyAuthClient = plugin.HttpClientAuth.ClientID, ApplyOtherHeaders = true };

            var modified = ModifyLikeJSSource(plugin, options, KeyUrl, OriginalHeaders());

            Assert.AreEqual(KeyUrl, modified.Url);
            Assert.AreEqual("session_id=session-value", modified.Headers["Cookie"]);
            Assert.AreEqual("auth-header-value", modified.Headers["X-Auth-Test"]);
            Assert.AreEqual("test-agent", modified.Headers["User-Agent"]);
        }

        [TestMethod]
        public void ApplyAuthClientAddsNothingForOtherDomains()
        {
            using var plugin = CreateLoggedInPlugin();
            var options = new Options { ApplyAuthClient = plugin.HttpClientAuth.ClientID, ApplyOtherHeaders = true };

            var modified = ModifyLikeJSSource(plugin, options, CdnUrl, OriginalHeaders());

            Assert.IsFalse(modified.Headers.Contains("Cookie"));
            Assert.IsFalse(modified.Headers.Contains("X-Auth-Test"));
            Assert.AreEqual("test-agent", modified.Headers["User-Agent"]);
        }

        [TestMethod]
        public void ApplyAuthClientAppendsToExistingCookieHeader()
        {
            using var plugin = CreateLoggedInPlugin();
            var options = new Options { ApplyAuthClient = plugin.HttpClientAuth.ClientID, ApplyOtherHeaders = true };
            var originalHeaders = OriginalHeaders();
            originalHeaders["Cookie"] = "existing=1";

            var modified = ModifyLikeJSSource(plugin, options, KeyUrl, originalHeaders);

            Assert.AreEqual("existing=1;session_id=session-value", modified.Headers["Cookie"]);
        }

        [TestMethod]
        public void ApplyCookieClientDoesNotSendAuthCookie()
        {
            using var plugin = CreateLoggedInPlugin();
            var options = new Options { ApplyCookieClient = plugin.HttpClientAuth.ClientID, ApplyOtherHeaders = true };

            var modified = ModifyLikeJSSource(plugin, options, KeyUrl, OriginalHeaders());

            Assert.IsFalse(modified.Headers.Contains("Cookie"));
        }

        [TestMethod]
        public void WithoutClientOptionsNoAuthIsApplied()
        {
            using var plugin = CreateLoggedInPlugin();
            var options = new Options { ApplyOtherHeaders = true };

            var modified = ModifyLikeJSSource(plugin, options, KeyUrl, OriginalHeaders());

            Assert.IsFalse(modified.Headers.Contains("Cookie"));
            Assert.AreEqual("test-agent", modified.Headers["User-Agent"]);
        }

        [TestMethod]
        public void UnknownAuthClientIdIsIgnored()
        {
            using var plugin = CreateLoggedInPlugin();
            var options = new Options { ApplyAuthClient = "missing-client", ApplyOtherHeaders = true };

            var modified = ModifyLikeJSSource(plugin, options, KeyUrl, OriginalHeaders());

            Assert.IsFalse(modified.Headers.Contains("Cookie"));
        }
    }
}
