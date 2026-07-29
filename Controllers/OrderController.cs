using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanVLXD.Models;
using System.Linq;
using System.Security.Claims;
using System.Collections.Generic;
using System.Threading.Tasks; 
using Microsoft.AspNetCore.Authorization; 
using System; 

namespace WebBanVLXD.Controllers
{
    public class OrderController : Controller
    {
        private readonly AppDbContext _context;

        public OrderController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Order/Track
        [HttpGet]
        public IActionResult Track(string id)
        {
            // Lấy dữ liệu cơ bản từ database, bao gồm chi tiết sản phẩm
            var query = _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .AsQueryable();

            // 1. KIỂM TRA QUYỀN VÀ LỌC THEO USER
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userName = User.Identity.Name;

                // ÉP BUỘC: Chỉ lấy những đơn hàng thuộc về User này
                query = query.Where(o => o.UserId == userId);

                // --- MỚI: LẤY DANH SÁCH ID SẢN PHẨM MÀ USER NÀY ĐÃ ĐÁNH GIÁ ---
                var reviewedProductIds = _context.Reviews
                    .Where(r => r.UserName == userName)
                    .Select(r => r.ProductId)
                    .ToList();

                // Gửi danh sách này sang View để kiểm tra ẩn/hiện nút Đánh giá
                ViewBag.ReviewedProducts = reviewedProductIds;
            }
            else
            {
                // Nếu khách chưa đăng nhập và không nhập mã đơn hàng -> Trả về danh sách rỗng
                if (string.IsNullOrEmpty(id))
                {
                    return View(new List<Order>());
                }
            }

            // 2. LỌC THEO MÃ ĐƠN HÀNG (Nếu người dùng tìm kiếm theo mã cụ thể)
            if (!string.IsNullOrEmpty(id))
            {
                query = query.Where(o => o.Id == id);
                if (!query.Any())
                {
                    ViewBag.Error = "Không tìm thấy đơn hàng nào hợp lệ với mã: " + id;
                }
            }

            // 3. TRẢ VỀ KẾT QUẢ VÀ SẮP XẾP MỚI NHẤT LÊN ĐẦU
            var orders = query.OrderByDescending(o => o.OrderDate).ToList();

            return View(orders);
        }

        // POST: /Order/SubmitReview
        [HttpPost]
        [Authorize] 
        public async Task<IActionResult> SubmitReview(string productId, int rating, string comment)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name ?? "Khách hàng";

            // 1. Kiểm tra xem đơn hàng đã "Hoàn thành" và khách thực sự đã mua sản phẩm này chưa
            var hasPurchased = await _context.Orders
                .AnyAsync(o => o.UserId == userId
                          && o.Status == "Hoàn thành"
                          && o.OrderDetails.Any(od => od.ProductId == productId));

            if (!hasPurchased)
            {
                return BadRequest("Bạn chỉ có thể đánh giá sản phẩm trong đơn hàng đã hoàn thành.");
            }

            // 2. --- MỚI: KIỂM TRA XEM NGƯỜI DÙNG ĐÃ ĐÁNH GIÁ SẢN PHẨM NÀY CHƯA (CHẶN TRÙNG LẶP) ---
            var isAlreadyReviewed = await _context.Reviews
                .AnyAsync(r => r.ProductId == productId && r.UserName == userName);

            if (isAlreadyReviewed)
            {
                // Nếu đã đánh giá rồi thì không lưu nữa, đẩy về trang Track luôn
                return RedirectToAction("Track");
            }

            // 3. Lưu đánh giá mới vào Database
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

            // 4. Quay lại trang tra cứu đơn hàng
            return RedirectToAction("Track");
        }
    }
}