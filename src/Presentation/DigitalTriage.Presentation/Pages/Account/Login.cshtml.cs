using DigitalTriage.Presentation.Common.Helpers;
using DigitalTriage.Application.Contracts.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace DigitalTriage.Presentation.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IPatientService _patientService;
        private readonly IAuthHelper _authHelper;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            IPatientService patientService, 
            IAuthHelper authHelper,
            ILogger<LoginModel> logger)
        {
            _patientService = patientService;
            _authHelper = authHelper;
            _logger = logger;
        }

        [BindProperty]
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string? ErrorMessage { get; set; }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                _logger.LogInformation("Attempting to authenticate user: {Email}", Email);

                // Authenticate using PatientService
                var user = await _patientService.AuthenticateAsync(Email, Password);

                if (user == null)
                {
                    _logger.LogWarning("Authentication failed for email: {Email}", Email);
                    ErrorMessage = "Invalid email or password";
                    ModelState.AddModelError(string.Empty, "Invalid login attempt");
                    return Page();
                }

                _logger.LogInformation("Authentication successful for user: {Email}, Role: {Role}", user.Email, user.Role);

                // Sign in using AuthHelper (which handles cookie creation)
                await _authHelper.SignInAsync(HttpContext, user);

                _logger.LogInformation("User signed in successfully: {Email}", user.Email);

                // Redirect based on role or return URL
                if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                {
                    return Redirect(ReturnUrl);
                }

                if (string.Equals(user.Role, "Doctor", StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect("/admin");
                }

                return Redirect("/personal-info");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", Email);
                ErrorMessage = "An error occurred during login. Please try again.";
                ModelState.AddModelError(string.Empty, "An error occurred during login");
                return Page();
            }
        }
    }
}
