using Grpc.Core;
using Spectrum.API.Exceptions;
using Spectrum.API.Grpc.Drops;

namespace Spectrum.API.Services.Drops
{
    public interface IDropsService
    {
        Task<ClaimKeyResponse> ClaimAccessKeyAsync(ClaimKeyRequest request);
    }

    public class DropsService : IDropsService
    {
        private readonly DropService.DropServiceClient _dropServiceClient;

        public DropsService(DropService.DropServiceClient dropServiceClient)
        {
            _dropServiceClient = dropServiceClient;
        }

        // TODO: Implementation of Java microservice call
        public async Task<ClaimKeyResponse> ClaimAccessKeyAsync(ClaimKeyRequest request)
        {
            try
            {
                var response = await _dropServiceClient.ClaimAccessKeyAsync(request);
                if (response.Success)
                {
                    return response;
                }
                else
                {
                    return new ClaimKeyResponse
                    {
                        Success = false,
                        AccessKeyCode = string.Empty,
                        ClaimedAt = 0
                    };
                }
            }
            catch (RpcException ex)
            {
                throw new SpectrumServiceUnavailableException("The drop service is currently unavailable.", ex);
            }
        }
    }
}
