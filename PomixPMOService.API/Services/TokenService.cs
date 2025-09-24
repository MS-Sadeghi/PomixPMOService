using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ServicePomixPMO.API.Data;
using ServicePomixPMO.API.Models;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ServicePomixPMO.API.Services
{
    public class TokenService : ITokenService
    {
        private readonly PomixServiceContext _context;
        private readonly IConfiguration _configuration;

        public TokenService(PomixServiceContext context, IConfiguration configuration)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<object> GenerateTokensAsync(User user)
        {
            var accessToken = await GenerateJwtToken(user);
            var refreshTokenString = Guid.NewGuid().ToString();
            var refreshToken = new RefreshToken
            {
                UserId = user.UserId,
                Token = refreshTokenString,
                ExpiryDate = DateTime.UtcNow.AddDays(7),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return new
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token
            };
        }

        public async Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken)
        {
            return await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken
                    && rt.ExpiryDate > DateTime.UtcNow
                    && !rt.IsRevoked);
        }

        public async Task<object> RevokeRefreshTokenAsync(string refreshToken)
        {
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);
            if (token == null)
            {
                return new { Success = false, Message = "Refresh Token یافت نشد." };
            }

            token.IsRevoked = true;
            _context.RefreshTokens.Update(token);
            await _context.SaveChangesAsync();

            return new { Success = true, Message = "Refresh Token با موفقیت باطل شد." };
        }

        private async Task<string> GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // گرفتن دسترسی‌های کاربر از جدول UserAccesses
            var permissions = await _context.UserAccesses
                .Where(ua => ua.UserId == user.UserId)
                .Select(ua => ua.Permission)
                .ToListAsync();

            var claims = new List<Claim>
            {
                new Claim("UserId", user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            // اضافه کردن دسترسی‌ها به عنوان ادعاها
            claims.AddRange(permissions.Select(p => new Claim("Permission", p)));

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}