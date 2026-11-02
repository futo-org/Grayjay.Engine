using Grayjay.Engine.Models;

namespace Grayjay.Engine.Tests
{
    [TestClass]
    public class AuthCookieTests
    {
        private class PlainEncryption : IPluginEncryptionProvider
        {
            public string Encrypt(string data) => data;
            public string Decrypt(string data) => data;
        }

        private static readonly Uri YoutubeUri = new Uri("https://www.youtube.com/youtubei/v1/player");

        private static PluginDescriptor CreateLoggedInDescriptor()
        {
            PluginDescriptor.Encryption = new PlainEncryption();
            var descriptor = new PluginDescriptor(new PluginConfig());
            descriptor.SetAuth(new SourceAuth()
            {
                CookieMap = new Dictionary<string, Dictionary<string, string>>()
                {
                    { ".youtube.com", new Dictionary<string, string>() { { "SID", "sid" }, { "__Secure-1PSIDTS", "old" } } }
                },
                Headers = new Dictionary<string, Dictionary<string, string>>()
            });
            return descriptor;
        }

        private static void ReceiveSetCookie(GrayjayPlugin plugin, string setCookie)
        {
            var headers = new HttpHeaders();
            headers.Add("Set-Cookie", setCookie);
            plugin.HttpClientAuth.ProcessRequest("POST", 200, YoutubeUri, headers);
        }

        private static string GetSentCookies(GrayjayPlugin plugin)
        {
            var headers = new HttpHeaders();
            plugin.HttpClientAuth.ApplyHeaders(YoutubeUri, headers, true);
            return headers["Cookie"];
        }

        [TestMethod]
        public void RotatedAuthCookie_IsSharedWithOtherClients()
        {
            var descriptor = CreateLoggedInDescriptor();
            var rotating = new GrayjayPlugin(descriptor, "");
            var other = new GrayjayPlugin(descriptor, "");

            ReceiveSetCookie(rotating, "__Secure-1PSIDTS=new; Domain=.youtube.com; Path=/; Secure; HttpOnly");

            StringAssert.Contains(GetSentCookies(other), "__Secure-1PSIDTS=new");
            StringAssert.Contains(GetSentCookies(new GrayjayPlugin(descriptor, "")), "__Secure-1PSIDTS=new");
        }

        [TestMethod]
        public void RotatedAuthCookie_IsPersistedAndNotified()
        {
            var descriptor = CreateLoggedInDescriptor();
            var updates = 0;
            var authChanges = 0;
            descriptor.OnAuthCookiesUpdated += () => updates++;
            descriptor.OnAuthChanged += () => authChanges++;
            var plugin = new GrayjayPlugin(descriptor, "");

            ReceiveSetCookie(plugin, "__Secure-1PSIDTS=new; Domain=.youtube.com; Path=/");
            ReceiveSetCookie(plugin, "__Secure-1PSIDTS=new; Domain=.youtube.com; Path=/");

            var reloaded = new PluginDescriptor(new PluginConfig(), descriptor.AuthEncrypted);
            Assert.AreEqual("new", reloaded.GetAuth().CookieMap[".youtube.com"]["__Secure-1PSIDTS"]);
            Assert.AreEqual(1, updates);
            Assert.AreEqual(0, authChanges);
        }

        [TestMethod]
        public void NonAuthCookie_IsNotPersisted()
        {
            var descriptor = CreateLoggedInDescriptor();
            var plugin = new GrayjayPlugin(descriptor, "");

            ReceiveSetCookie(plugin, "YSC=abc; Domain=.youtube.com; Path=/");
            ReceiveSetCookie(plugin, "SID=; Domain=.youtube.com; Max-Age=0");

            var cookies = descriptor.GetAuth().CookieMap[".youtube.com"];
            Assert.IsFalse(cookies.ContainsKey("YSC"));
            Assert.AreEqual("sid", cookies["SID"]);
        }
    }
}
