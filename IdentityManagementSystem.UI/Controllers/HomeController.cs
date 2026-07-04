using DNTCaptcha.Core;
using IdentityManagementSystem.UI.Areas.Security.Models;
using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.UI.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDNTCaptchaValidatorService _captchaValidatorService;
        private readonly HttpClient _client;

        public HomeController(
            IHttpClientFactory httpClientFactory,
            IDNTCaptchaValidatorService captchaValidatorService)
        {
            _client = httpClientFactory.CreateClient("PomixApi");
            _captchaValidatorService = captchaValidatorService ?? throw new ArgumentNullException(nameof(captchaValidatorService));
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new LoginViewModel());
        }
    }
}
