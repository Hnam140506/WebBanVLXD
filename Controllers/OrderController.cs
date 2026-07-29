using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanVLXD.Models;
using System.Linq;
using System.Security.Claims;
using System.Collections.Generic;
using System.Threading.Tasks; 
using Microsoft.AspNetCore.Authorization; 
using System; 
using DinkToPdf;
using DinkToPdf.Contracts;
using System.Text;

namespace WebBanVLXD.Controllers
{
    public class OrderController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConverter _converter;

        // GỘP CHUNG VÀO 1 CONSTRUCTOR DUY NHẤT ĐỂ NHẬN CẢ DATABASE VÀ PDF CONVERTER
        public OrderController(AppDbContext context, IConverter converter)
        {
            _context = context;
            _converter = converter;
        }

        // ==========================================
        // 1. TRA CỨU ĐƠN HÀNG
        // ==========================================
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

        // ==========================================
        // 2. XỬ LÝ GỬI ĐÁNH GIÁ
        // ==========================================
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
                return RedirectToAction("Track");
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

            // Quay lại trang danh sách đơn hàng tổng quát
            return RedirectToAction("Track");
        }

        // ==========================================
        // 3. XUẤT HÓA ĐƠN PDF
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> DownloadInvoice(string id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            // 1. Tạo giao diện HTML cho hóa đơn
            var sb = new StringBuilder();
            sb.Append(@"
            <html>
            <head>
                <style>
                    body { font-family: 'Arial'; margin: 20px; }
                    .header { text-align: center; color: #0b224c; border-bottom: 2px solid #f57224; padding-bottom: 10px; }
                    .info { margin-bottom: 20px; line-height: 1.6; }
                    table { width: 100%; border-collapse: collapse; margin-top: 20px; }
                    th, td { border: 1px solid #ddd; padding: 10px; text-align: left; }
                    th { background-color: #f57224; color: white; }
                    .total { text-align: right; margin-top: 20px; font-size: 18px; font-weight: bold; }
                    .footer { margin-top: 50px; text-align: center; font-size: 12px; color: #777; }
                </style>
            </head>
            <body>
                <div class='header'>
                    <h1>HÓA ĐƠN BÁN HÀNG</h1>
                    <p>BuildSmart - Vật Liệu Xây Dựng Chất Lượng Cao</p>
                </div>
                <div class='info'>
                    <p><b>Mã đơn hàng:</b> #" + order.Id.Substring(0, 8).ToUpper() + @"</p>
                    <p><b>Khách hàng:</b> " + order.CustomerName + @"</p>
                    <p><b>Điện thoại:</b> " + order.Phone + @"</p>
                    <p><b>Địa chỉ:</b> " + order.Address + @"</p>
                    <p><b>Ngày đặt:</b> " + order.OrderDate.ToString("dd/MM/yyyy HH:mm") + @"</p>
                </div>
                <table>
                    <thead>
                        <tr>
                            <th>STT</th>
                            <th>Sản phẩm</th>
                            <th>Số lượng</th>
                            <th>Đơn giá</th>
                            <th>Thành tiền</th>
                        </tr>
                    </thead>
                    <tbody>");

            int i = 1;
            foreach (var item in order.OrderDetails)
            {
                sb.Append($@"
                        <tr>
                            <td>{i++}</td>
                            <td>{item.Product.Name}</td>
                            <td>{item.Quantity}</td>
                            <td>{item.Price.ToString("N0")} đ</td>
                            <td>{(item.Price * item.Quantity).ToString("N0")} đ</td>
                        </tr>");
            }

            sb.Append($@"
                    </tbody>
                </table>
                <div class='total'>
                    <p>Tổng cộng: {order.TotalAmount.ToString("N0")} đ</p>
                </div>
                <div class='footer'>
                    <p>Cảm ơn quý khách đã tin tưởng BuildSmart!</p>
                    <p>Website: buildsmart.vn | Hotline: 1900 123 456</p>
                </div>
            </body>
            </html>");

            // 2. Cấu hình PDF
            var globalSettings = new GlobalSettings
            {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Portrait,
                PaperSize = PaperKind.A4,
                Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 },
                DocumentTitle = "HoaDon_" + order.Id
            };

            var objectSettings = new ObjectSettings
            {
                PagesCount = true,
                HtmlContent = sb.ToString(),
                WebSettings = { DefaultEncoding = "utf-8" }
            };

            var pdf = new HtmlToPdfDocument()
            {
                GlobalSettings = globalSettings,
                Objects = { objectSettings }
            };

            // 3. Chuyển đổi và trả về file PDF
            var file = _converter.Convert(pdf);
            return File(file, "application/pdf", $"Invoice_{order.Id.Substring(0, 8)}.pdf");
        }
    }
}