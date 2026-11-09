using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;

namespace UnpaidCeo.CloudCode
{
    /// <summary>Registers the UGS API client the endpoints use.</summary>
    public sealed class ModuleConfig : ICloudCodeSetup
    {
        public void Setup(ICloudCodeConfig config) => config.AddGameApiClient();
    }
}
