using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class HLSManifestWidevineSource : HLSManifestSource, IWidevineSource
    {
        public override string Type => "HLSWidevineSource";

        [V8Property("licenseUri")]
        public string LicenseUri { get; set; }

        [V8Property("serviceCertificate", true)]
        public string? ServiceCertificate { get; set; }

        public HLSManifestWidevineSource() { }
        public HLSManifestWidevineSource(GrayjayPlugin plugin, IJavaScriptObject obj) : base(plugin, obj) { }
    }
}
