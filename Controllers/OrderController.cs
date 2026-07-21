using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanVLXD.Models;
using System.Linq;

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
            // Lấy tất cả đơn hàng từ database, bao gồm chi tiết sản phẩm
            var query = _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .AsQueryable();

            // Nếu người dùng có nhập mã vào ô tìm kiếm thì mới lọc
            if (!string.IsNullOrEmpty(id))
            {
                query = query.Where(o => o.Id == id); // Lọc chính xác mã đơn
                if (!query.Any())
                {
                    ViewBag.Error = "Không tìm thấy đơn hàng nào với mã: " + id;
                }
            }

            // Trả về danh sách (nếu không nhập 'id' thì sẽ là toàn bộ đơn hàng)
            // Sắp xếp đơn hàng mới nhất lên đầu (nếu Model của bạn có trường OrderDate thì đổi lại)
            var orders = query.ToList();

            return View(orders);
        }
    }
}