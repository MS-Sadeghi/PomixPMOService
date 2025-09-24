using ServicePomixPMO.API.Models;
using System.Threading.Tasks;

namespace ServicePomixPMO.API.Services
{
    public interface ITokenService
    {
        Task<object> GenerateTokensAsync(User user);
        Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken);
        Task<object> RevokeRefreshTokenAsync(string refreshToken);
    }
}