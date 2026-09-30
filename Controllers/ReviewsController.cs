using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Models.ViewModels;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Controllers
{
    [Authorize]
    public class ReviewsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public ReviewsController(AppDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        [HttpPost]
        public async Task<IActionResult> Save(ReviewInput input)
        {
            var product = await _db.Products.FindAsync(input.ProductCode);
            if (product == null)
            {
                return NotFound();
            }

            var user = (await _users.GetUserAsync(User))!;
            var purchased = await _db.HasPurchasedAsync(user.Id, product.ProductCode);
            if (!product.IsListed && !purchased)
            {
                return NotFound();
            }
            if (product.SellerId == user.Id)
            {
                this.Error("You can't review your own title.");
            }
            else if (!ModelState.IsValid)
            {
                this.Error(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            }
            else
            {
                var review = await _db.Reviews.FirstOrDefaultAsync(r => r.ProductCode == product.ProductCode && r.UserId == user.Id);
                if (review == null)
                {
                    review = new Review { ProductCode = product.ProductCode, UserId = user.Id };
                    _db.Reviews.Add(review);
                }
                review.Rating = input.Rating;
                review.Title = input.Title?.Trim();
                review.Body = input.Body?.Trim();
                review.ReviewerName = user.DisplayName ?? user.UserName?.Split('@')[0] ?? "Customer";
                review.VerifiedPurchase = purchased;
                review.CreatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                this.Success("Thanks! Your review has been saved.");
            }

            return Redirect(Url.Action("Details", "Products", new { id = product.ProductCode }) + "#reviews");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int productCode)
        {
            var userId = _users.GetUserId(User);
            var deleted = await _db.Reviews.Where(r => r.ProductCode == productCode && r.UserId == userId).ExecuteDeleteAsync();
            if (deleted > 0)
            {
                this.Success("Your review was deleted.");
            }
            return Redirect(Url.Action("Details", "Products", new { id = productCode }) + "#reviews");
        }
    }
}
