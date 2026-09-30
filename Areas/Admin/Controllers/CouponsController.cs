using e_Commerce_application.Controllers;
using e_Commerce_application.Data;
using e_Commerce_application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class CouponsController : Controller
    {
        private readonly AppDbContext _db;

        public CouponsController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index() =>
            View(await _db.Coupons.AsNoTracking().OrderByDescending(c => c.CreatedAt).ToListAsync());

        public IActionResult Create() => View("Edit", new Coupon());

        [HttpPost]
        public async Task<IActionResult> Create(Coupon model)
        {
            await ValidateAsync(model, null);
            if (!ModelState.IsValid)
            {
                return View("Edit", model);
            }
            model.Code = model.Code.Trim().ToUpperInvariant();
            model.UsedCount = 0;
            model.CreatedAt = DateTime.UtcNow;
            _db.Coupons.Add(model);
            await _db.SaveChangesAsync();
            this.Success($"Coupon {model.Code} created.");
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var coupon = await _db.Coupons.FindAsync(id);
            return coupon == null ? NotFound() : View(coupon);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, Coupon model)
        {
            var coupon = await _db.Coupons.FindAsync(id);
            if (coupon == null)
            {
                return NotFound();
            }
            await ValidateAsync(model, id);
            if (!ModelState.IsValid)
            {
                model.Id = id;
                model.UsedCount = coupon.UsedCount;
                return View(model);
            }

            coupon.Code = model.Code.Trim().ToUpperInvariant();
            coupon.Description = model.Description;
            coupon.PercentOff = model.PercentOff;
            coupon.AmountOff = model.PercentOff.HasValue ? null : model.AmountOff;
            coupon.MinSubtotal = model.MinSubtotal;
            coupon.ExpiresAt = model.ExpiresAt;
            coupon.MaxUses = model.MaxUses;
            coupon.Active = model.Active;
            await _db.SaveChangesAsync();
            this.Success($"Coupon {coupon.Code} saved.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _db.Coupons.Where(c => c.Id == id).ExecuteDeleteAsync();
            this.Success("Coupon deleted.");
            return RedirectToAction(nameof(Index));
        }

        private async Task ValidateAsync(Coupon model, int? id)
        {
            if (model.PercentOff == null && model.AmountOff == null)
            {
                ModelState.AddModelError(nameof(model.PercentOff), "Enter a percent off or an amount off.");
            }
            var code = model.Code?.Trim().ToUpperInvariant();
            if (code != null && await _db.Coupons.AnyAsync(c => c.Code == code && c.Id != id))
            {
                ModelState.AddModelError(nameof(model.Code), "A coupon with this code already exists.");
            }
        }
    }
}
