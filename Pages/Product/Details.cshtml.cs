using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebBanVLXD.Models;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;
using System;

namespace WebBanVLXD.Pages.Product
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        // Thêm = default! hoặc = null! để triệt tiêu cảnh báo CS8601
        public WebBanVLXD.Models.Product Product { get; set; } = default!; 
        public bool CanReview { get; set; } = false; 

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var productData = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (productData == null) return NotFound();

            // Đảm bảo không bị null reference assignment
            Product = productData;

            // KIỂM TRA QUYỀN ĐÁNH GIÁ 
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userName = User.Identity.Name;
                
                // 1. Đếm số đơn hàng CÓ CHỨA SẢN PHẨM NÀY đã mua thành công 
                // CÁCH MỚI: Truy vấn từ bảng Orders
                var purchasedOrdersCount = await _context.Orders
                    .Where(o => o.UserId == userId 
                             && (o.Status == "Hoàn thành" || o.Status == "Đã thanh toán")
                             && o.OrderDetails.Any(od => od.ProductId == id)) // Kiểm tra xem đơn hàng có chứa sản phẩm này không
                    .CountAsync();

                // 2. Đếm số lần user này đã review sản phẩm này
                var reviewCount = await _context.Reviews
                    .Where(r => r.ProductId == id && r.UserName == userName)
                    .CountAsync();

                // 3. Nếu số đơn > số lượt review => Được review tiếp
                CanReview = purchasedOrdersCount > reviewCount;
            }

            return Page();
        }

        // HANDLER LƯU ĐÁNH GIÁ 
        public async Task<IActionResult> OnPostAddReviewAsync(string productId, int rating, string comment)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated) return Challenge();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Dùng ?? "Unknown" để triệt tiêu cảnh báo null CS8601
            var userName = User.Identity.Name ?? "Unknown"; 

            // Xác thực lại ở Backend
            var purchasedOrdersCount = await _context.Orders
                .Where(o => o.UserId == userId 
                         && (o.Status == "Hoàn thành" || o.Status == "Đã thanh toán")
                         && o.OrderDetails.Any(od => od.ProductId == productId))
                .CountAsync();

            var reviewCount = await _context.Reviews
                .Where(r => r.ProductId == productId && r.UserName == userName)
                .CountAsync();

            if (purchasedOrdersCount > reviewCount)
            {
                var review = new Review
                {
                    ProductId = productId,
                    UserName = userName, 
                    Rating = rating,
                    Comment = comment,
                    CreatedAt = DateTime.Now
                };
                
                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { id = productId });
        }
    }
}