using DigitalTriage.Presentation.Common.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DigitalTriage.Presentation.Pages.Account
{
 public class LogoutModel : PageModel
 {
 private readonly IAuthHelper _authHelper;
 private readonly ILogger<LogoutModel> _logger;

 public LogoutModel(IAuthHelper authHelper, ILogger<LogoutModel> logger)
 {
 _authHelper = authHelper;
 _logger = logger;
 }

 public async Task<IActionResult> OnGet()
 {
 _logger.LogInformation("Logout requested");

 try
 {
 await _authHelper.SignOutAsync(HttpContext);
 _logger.LogInformation("User signed out successfully");
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error during sign-out");
 }

 // Always redirect to login page after sign-out
 return Redirect("/Account/Login");
 }

 public async Task<IActionResult> OnPost()
 {
 return await OnGet();
 }
 }
}
