using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class DashManifestWidevineSource : DashManifestSource, IWidevineSource
    {
        public override string Type => "DashWidevineSource";

        [V8Property("licenseUri")]
        public string LicenseUri { get; set; }

        [V8Property("serviceCertificate", true)]
        public string? ServiceCertificate { get; set; }

        public DashManifestWidevineSource() { }
        public DashManifestWidevineSource(GrayjayPlugin plugin, IJavaScriptObject obj) : base(plugin, obj) { }
    }
}
