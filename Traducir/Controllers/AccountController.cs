using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Traducir.Core.Services;

namespace Traducir.Controllers
{
    public class AccountController : Controller
    {
        ISEApiService _seApiService { get; }
        IConfiguration _configuration { get; }

        public AccountController(IConfiguration configuration, ISEApiService seApiService)
        {
            _seApiService = seApiService;
            _configuration = configuration;
        }

        string GetOauthReturnUrl()
        {
            return Url.Action("OauthCallback", null, null, "https");
        }

        [Route("app/login")]
        public IActionResult LogIn(string returnUrl)
        {
            return Redirect(_seApiService.GetInitialOauthUrl(GetOauthReturnUrl(), returnUrl));
        }

        [Route("app/logout")]
        public async Task<IActionResult> LogOut()
        {
            await HttpContext.SignOutAsync();
            return Content("bye!");
        }

        [Route("app/oauth-callback")]
        public async Task<IActionResult> OauthCallback(string code, string state)
        {
            var accessToken = await _seApiService.GetAccessTokenFromCodeAsync(code, GetOauthReturnUrl());

            var siteDomain = _configuration.GetValue<string>("STACKAPP_SITEDOMAIN");
            var currentUser = await _seApiService.GetMyUserAsync(siteDomain, accessToken);

            if (currentUser == null)
            {
                return Content("Could not retrieve a user account for " + siteDomain);
            }

            var identity = new ClaimsIdentity(new []
            {
                new Claim("IsEmployee", currentUser.IsEmployee.ToString()),
                    new Claim("AccountId", currentUser.AccountId.ToString()),
                    new Claim("UserId", currentUser.UserId.ToString()),
                    new Claim("Name", currentUser.DisplayName),
                    new Claim("UserType", currentUser.UserType)
            }, "login");

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return Redirect(state);
        }

        [Route("app/get-account-type")]
        public IActionResult GetAccountType()
        {
            var employeeClaimValue = User.Claims.Where(c => c.Type == "IsEmployee").Select(c => c.Value).FirstOrDefault();
            if (employeeClaimValue == null)
            {
                return Unauthorized();
            }
            return Content(employeeClaimValue == true.ToString()? "employee" : "user");
        }
    }
}