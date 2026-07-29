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

                // --- CẬP NHẬT LOGIC KIỂM TRA ĐÁNH GIÁ THEO TỪNG ĐƠN HÀNG ---
                // Lấy danh sách các cặp chuỗi kết hợp "MãĐơnHàng_MãSảnPhẩm" đã được đánh giá
                var reviewedKeys = _context.Reviews
                    .Where(r => r.UserName == userName)
                    .Select(r => r.OrderId + "_" + r.ProductId) 
                    .ToList();

                // Gửi danh sách "Khóa" này sang View
                ViewBag.ReviewedKeys = reviewedKeys;
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
        public async Task<IActionResult> SubmitReview(string productId, string orderId, int rating, string comment)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name ?? "Khách hàng";

            // 1. Kiểm tra xem đơn hàng cụ thể này có thuộc về User và đã "Hoàn thành" chưa
            var hasPurchased = await _context.Orders
                .AnyAsync(o => o.UserId == userId
                          && o.Id == orderId
                          && o.Status == "Hoàn thành"
                          && o.OrderDetails.Any(od => od.ProductId == productId));

            if (!hasPurchased)
            {
                return BadRequest("Bạn chỉ có thể đánh giá sản phẩm trong đơn hàng của chính mình đã hoàn thành.");
            }

            // 2. KIỂM TRA XEM SẢN PHẨM TRONG ĐƠN HÀNG NÀY ĐÃ ĐƯỢC ĐÁNH GIÁ CHƯA
            var isAlreadyReviewed = await _context.Reviews
                .AnyAsync(r => r.OrderId == orderId && r.ProductId == productId && r.UserName == userName);

            if (isAlreadyReviewed)
            {
                // Nếu đã đánh giá cho đơn này rồi thì quay về trang tra cứu luôn, không lưu đè
                return RedirectToAction("Track", new { id = orderId });
            }

            // 3. Lưu đánh giá mới vào Database (Có lưu kèm OrderId)
            var review = new Review
            {
                ProductId = productId,
                OrderId = orderId, // QUAN TRỌNG: Lưu mã đơn hàng vào bảng Review
                UserName = userName,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.Now
            };

           _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // SỬA DÒNG NÀY: Bỏ phần 'new { id = orderId }' đi
            // Thay vì quay về 1 đơn, ta quay về danh sách tổng quát
            return RedirectToAction("Track");
        }
    }
}