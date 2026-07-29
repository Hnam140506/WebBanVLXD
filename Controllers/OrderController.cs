using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanVLXD.Models;
using System.Linq;
using System.Security.Claims; // BẮT BUỘC THÊM: Để lấy ID tài khoản
using System.Collections.Generic; // BẮT BUỘC THÊM: Để dùng List<Order>

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
                // Lấy ID của tài khoản đang đăng nhập
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                // ÉP BUỘC: Chỉ lấy những đơn hàng thuộc về User này
                query = query.Where(o => o.UserId == userId);
            }
            else
            {
                // Nếu khách CHƯA đăng nhập mà cũng KHÔNG nhập mã đơn hàng
                // -> Trả về danh sách rỗng (Không hiển thị gì cả)
                if (string.IsNullOrEmpty(id))
                {
                    return View(new List<Order>());
                }
            }

            // 2. LỌC THEO MÃ ĐƠN HÀNG (Nếu người dùng có gõ vào ô tìm kiếm)
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
    }
}