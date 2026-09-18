using Grayjay.Engine.Models.Video.Additions;

namespace Grayjay.Engine.Models.Video.Sources
{
    public interface IWidevineSource
    {
        string LicenseUri { get; }
        //Base64 Widevine service certificate; when set the CDM enables privacy mode (encrypted client id)
        string? ServiceCertificate { get; }
        bool HasLicenseRequestExecutor { get; }
        RequestExecutor GetLicenseRequestExecutor();
    }
}
