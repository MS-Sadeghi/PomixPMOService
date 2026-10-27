using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace IdentityManagementSystem.API.Services
{
    /// <summary>تنظیمات ورود یکپارچه با اینترانت سازمانی. Key فقط در User Secrets یا تنظیمات محرمانه‌ی استقرار نگهداری شود.</summary>
    public sealed class IntranetSsoOptions
    {
        /// <summary>آدرس API اینترانت، مثل https://intranet-api.local/ . خالی = ورود یکپارچه خاموش است.</summary>
        public string ApiBaseUrl { get; set; } = "";
        /// <summary>کد این سامانه در اینترانت (ApplicationCode)؛ مثل ACCESSREPORT.</summary>
        public string ApplicationCode { get; set; } = "";
        /// <summary>کلید SSO این سامانه؛ همان SsoKey که در تنظیمات API اینترانت ثبت شده است (دست‌کم ۳۲ نویسه).</summary>
        public string Key { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 10;
    }

    /// <summary>هویتی که اینترانت پس از تحویل بلیت برمی‌گرداند. Username همان نام کاربری این کاربر در همین سامانه است.</summary>
    public sealed record IntranetSsoIdentity(string Username, string? NationalId, string Name, string LastName);

    /// <summary>بلیت یک‌بارمصرف ورود یکپارچه را از سرور (نه مرورگر) به API اینترانت می‌دهد و هویت کاربر را می‌گیرد.</summary>
    public sealed class IntranetSsoClient
    {
        public const string HttpClientName = "IntranetSso";
        private static readonly Regex TicketPattern = new("^[A-Za-z0-9_-]{43}$", RegexOptions.Compiled);
        private static readonly Regex CodePattern = new("^[A-Za-z0-9_-]{1,50}$", RegexOptions.Compiled);
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IntranetSsoOptions _options;

        public IntranetSsoClient(IHttpClientFactory httpClientFactory, IOptions<IntranetSsoOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public bool IsConfigured =>
            Uri.TryCreate(_options.ApiBaseUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && CodePattern.IsMatch(_options.ApplicationCode ?? "")
            && (_options.Key?.Length ?? 0) >= 32;

        public static bool IsWellFormed(string? ticket) => ticket is not null && TicketPattern.IsMatch(ticket);

        /// <summary>null یعنی بلیت نامعتبر، منقضی، استفاده‌شده یا دسترسی کاربر گرفته شده است. خطای پیکربندی (کلید نادرست) و شبکه استثنا می‌دهد.</summary>
        public async Task<IntranetSsoIdentity?> RedeemAsync(string ticket, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 2, 60));
            var url = new Uri(new Uri(_options.ApiBaseUrl), $"api/integrations/applications/{Uri.EscapeDataString(_options.ApplicationCode)}/sso/redeem");
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { ticket }), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-Application-Key", _options.Key);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.BadRequest) return null;
            response.EnsureSuccessStatusCode(); // 401 = کلید یا کد سامانه اشتباه است؛ خطای پیکربندی
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var identity = JsonSerializer.Deserialize<IntranetSsoIdentity>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return identity is not null && !string.IsNullOrWhiteSpace(identity.Username) ? identity : null;
        }
    }
}
