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

        public WebBanVLXD.Models.Product Product { get; set; } = default!; 
        public bool CanReview { get; set; } = false; 

        // Hàm bổ trợ dùng chung để nạp sản phẩm & kiểm tra quyền Review
        private async Task<bool> LoadProductDataAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            var productData = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (productData == null) return false;

            Product = productData;

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userName = User.Identity.Name;
                
                var purchasedOrdersCount = await _context.Orders
                    .Where(o => o.UserId == userId 
                             && o.Status == "Hoàn thành"
                             && o.OrderDetails.Any(od => od.ProductId == id))
                    .CountAsync();

                var reviewCount = await _context.Reviews
                    .Where(r => r.ProductId == id && r.UserName == userName)
                    .CountAsync();

                CanReview = purchasedOrdersCount > reviewCount;

                ViewData["Debug_Purchased"] = purchasedOrdersCount;
                ViewData["Debug_Review"] = reviewCount;
            }

            return true;
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            bool found = await LoadProductDataAsync(id);
            if (!found) return NotFound();

            return Page();
        }

        // HANDLER LƯU ĐÁNH GIÁ (Đã gộp chung tính năng bảo vệ vào đây)
        public async Task<IActionResult> OnPostAsync(string id, int rating, string comment)
{
            // 1. Chức năng bảo vệ: Nếu không có ID sản phẩm trên URL, đẩy về trang chủ để tránh lỗi
            if (string.IsNullOrEmpty(id)) 
            {
                return RedirectToPage("/Index"); 
            }

            // 2. Yêu cầu đăng nhập
            if (User.Identity == null || !User.Identity.IsAuthenticated) 
            {
                return Challenge();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity.Name ?? "Unknown"; 

            // 3. Đếm số đơn hàng và số đánh giá dựa trên ID
            var purchasedOrdersCount = await _context.Orders
                .Where(o => o.UserId == userId 
                         && o.Status == "Hoàn thành"
                         && o.OrderDetails.Any(od => od.ProductId == id))
                .CountAsync();

            var reviewCount = await _context.Reviews
                .Where(r => r.ProductId == id && r.UserName == userName)
                .CountAsync();

            // 4. Lưu đánh giá vào Database nếu hợp lệ
            if (purchasedOrdersCount > reviewCount)
            {
                var review = new Review
                {
                    ProductId = id, // Sử dụng thẳng tham số id từ URL
                    UserName = userName, 
                    Rating = rating,
                    Comment = comment,
                    CreatedAt = DateTime.Now
                };
                
                _context.Reviews.Add(review);
                await _context.SaveChangesAsync(); // Lưu thay đổi xuống SQL Server
            }

            // 5. Trả về đúng trang chi tiết của sản phẩm đó để load lại dữ liệu
            return RedirectToPage(new { id = id });
        }
    }
}