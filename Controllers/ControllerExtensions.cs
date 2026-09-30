using Microsoft.AspNetCore.Mvc;

namespace e_Commerce_application.Controllers
{
    public static class ControllerExtensions
    {
        public static void Success(this Controller controller, string message) => controller.TempData["Success"] = message;

        public static void Error(this Controller controller, string message) => controller.TempData["Error"] = message;

        public static IActionResult RedirectToLocal(this Controller controller, string? returnUrl, IActionResult fallback) =>
            !string.IsNullOrEmpty(returnUrl) && controller.Url.IsLocalUrl(returnUrl) ? controller.LocalRedirect(returnUrl) : fallback;
    }
}
