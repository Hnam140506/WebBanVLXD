using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebBanVLXD.Models;
using System.Linq;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace WebBanVLXD.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AdminController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private List<string> CategoryList = new List<string> {
            "Sắt thép xây dựng", "Xi măng, Cát, Đá", "Gạch xây dựng", "Sơn & Chống thấm", "Gạch ốp lát", "Thiết bị điện nước", "Vật liệu khác"
        };

        // ==========================================
        // 1. DASHBOARD
        // ==========================================
        public IActionResult Index()
        {
            var orders = _context.Orders.ToList();
            ViewBag.TotalRevenue = orders.Where(o => o.Status == "Hoàn thành").Sum(o => o.TotalAmount);
            ViewBag.OrderCount = orders.Count;
            ViewBag.ProductCount = _context.Products.Count();
            ViewBag.UserCount = _context.Users.Count();

            var reportData = _context.Orders
                .Where(o => o.OrderDate >= DateTime.Now.AddDays(-7))
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key.ToString("dd/MM"), Total = g.Sum(o => o.TotalAmount) })
                .ToList();

            ViewBag.ChartLabels = reportData.Select(d => d.Date).ToList();
            ViewBag.ChartValues = reportData.Select(d => d.Total).ToList();

            return View();
        }

        // ==========================================
        // 2. QUẢN LÝ ĐƠN HÀNG
        // ==========================================
        public IActionResult Orders()
        {
            var orders = _context.Orders.OrderByDescending(o => o.OrderDate).ToList();
            return View(orders);
        }

        [HttpPost]
        public IActionResult UpdateOrderStatus(string orderId, string status)
        {
            var order = _context.Orders.Find(orderId);
            if (order != null)
            {
                order.Status = status;
                _context.SaveChanges();
            }
            return RedirectToAction("Orders");
        }

        // ==========================================
        // 3. QUẢN LÝ SẢN PHẨM (Nâng cấp Nhiều ảnh & Phiên bản)
        // ==========================================
        public IActionResult Products()
        {
            // Load sản phẩm kèm theo các phiên bản để hiển thị tổng tồn kho
            var products = _context.Products.Include(p => p.Variants).OrderByDescending(p => p.CreatedAt).ToList();
            return View(products);
        }

        public IActionResult CreateProduct()
        {
            ViewBag.Categories = CategoryList;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateProduct(Product product, List<IFormFile> moreImages,
    string[] vNames, decimal[] vPrices, int[] vStocks, List<IFormFile> vImages) // <-- Thêm vImages
        {
            // 1. Xử lý lưu nhiều ảnh
            if (moreImages != null && moreImages.Count > 0)
            {
                foreach (var file in moreImages)
                {
                    string url = await SaveImage(file);
                    product.Images.Add(new ProductImage { Url = url });
                }
                product.ImageUrl = product.Images[0].Url; // Lấy ảnh đầu làm ảnh đại diện
            }

            // 2. Xử lý lưu các phiên bản (Variants)
            if (vNames != null && vNames.Length > 0)
            {
                for (int i = 0; i < vNames.Length; i++)
                {
                    var variant = new ProductVariant
                    {
                        Name = vNames[i],
                        Price = vPrices[i],
                        StockQuantity = vStocks[i]
                    };

                    // XỬ LÝ LƯU ẢNH CHO TỪNG PHIÊN BẢN
                    if (vImages != null && vImages.Count > i && vImages[i] != null && vImages[i].Length > 0)
                    {
                        variant.ImageUrl = await SaveImage(vImages[i]);
                    }

                    product.Variants.Add(variant);
                }
                // Đồng bộ giá và tồn kho chính
                product.Price = vPrices[0];
                product.StockQuantity = vStocks.Sum();
            }

            if (string.IsNullOrEmpty(product.Description)) product.Description = "Đang cập nhật nội dung...";
            if (string.IsNullOrEmpty(product.Brand)) product.Brand = "Chính hãng";

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction("Products");
        }

        public IActionResult EditProduct(string id)
        {
            var product = _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .FirstOrDefault(p => p.Id == id);

            if (product == null) return NotFound();
            ViewBag.Categories = CategoryList;
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> EditProduct(Product product, IFormFile? imageFile, List<IFormFile>? moreImages,
            string[]? vNames, decimal[]? vPrices, int[]? vStocks, List<IFormFile>? vImages)
        {
            // 1. Lấy sản phẩm cũ kèm theo Images và Variants từ Database
            var existing = _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .FirstOrDefault(p => p.Id == product.Id);

            if (existing == null) return NotFound();

            // 2. Cập nhật các thông tin văn bản/số cơ bản
            existing.Name = product.Name;
            existing.Category = product.Category;
            existing.Price = product.Price;
            existing.OldPrice = product.OldPrice;
            existing.Unit = product.Unit;
            existing.Brand = product.Brand;
            existing.Description = product.Description;

            // 3. Xử lý Ảnh đại diện chính (ImageUrl) nếu có chọn file mới
            if (imageFile != null && imageFile.Length > 0)
            {
                existing.ImageUrl = await SaveImage(imageFile);
            }

            // 4. Xử lý lưu thêm các ảnh Slider (moreImages)
            if (moreImages != null && moreImages.Count > 0)
            {
                foreach (var file in moreImages)
                {
                    if (file.Length > 0)
                    {
                        string url = await SaveImage(file);
                        existing.Images.Add(new ProductImage { Url = url });
                    }
                }
            }

            // 5. Xử lý thêm các phiên bản (Variants) mới bổ sung (nếu admin có điền thêm)
            if (vNames != null && vNames.Length > 0)
            {
                for (int i = 0; i < vNames.Length; i++)
                {
                    // Chỉ thêm nếu người dùng có nhập tên phiên bản
                    if (!string.IsNullOrEmpty(vNames[i]))
                    {
                        var variant = new ProductVariant
                        {
                            Name = vNames[i],
                            Price = vPrices != null && vPrices.Length > i ? vPrices[i] : existing.Price,
                            StockQuantity = vStocks != null && vStocks.Length > i ? vStocks[i] : 0
                        };

                        // Lưu ảnh riêng cho phiên bản nếu có chọn
                        if (vImages != null && vImages.Count > i && vImages[i] != null && vImages[i].Length > 0)
                        {
                            variant.ImageUrl = await SaveImage(vImages[i]);
                        }

                        existing.Variants.Add(variant);
                    }
                }

                // Cập nhật lại tổng tồn kho chính nếu cần
                existing.StockQuantity = existing.Variants.Sum(v => v.StockQuantity);
            }

            // 6. Lưu thay đổi vào Database
            await _context.SaveChangesAsync();
            return RedirectToAction("Products");
        }

        [HttpPost]
        public IActionResult DeleteProduct(string id)
        {
            var product = _context.Products.Find(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                _context.SaveChanges();
            }
            return RedirectToAction("Products");
        }

        // ==========================================
        // 4. QUẢN LÝ NGƯỜI DÙNG
        // ==========================================
        public IActionResult Users()
        {
            return View(_context.Users.OrderByDescending(u => u.CreatedAt).ToList());
        }

        [HttpPost]
        public IActionResult UpdateUserRole(string userId, string newRole)
        {
            var user = _context.Users.Find(userId);
            if (user != null) { user.Role = newRole; _context.SaveChanges(); }
            return RedirectToAction("Users");
        }

        [HttpPost]
        public IActionResult DeleteUser(string id)
        {
            var user = _context.Users.Find(id);
            if (user != null) { _context.Users.Remove(user); _context.SaveChanges(); }
            return RedirectToAction("Users");
        }

        // ==========================================
        // 5. QUẢN LÝ MÃ GIẢM GIÁ
        // ==========================================
        public IActionResult Coupons()
        {
            return View(_context.Coupons.OrderByDescending(c => c.ExpiryDate).ToList());
        }

        public IActionResult CreateCoupon() => View();

        [HttpPost]
        public IActionResult CreateCoupon(Coupon coupon)
        {
            _context.Coupons.Add(coupon);
            _context.SaveChanges();
            return RedirectToAction("Coupons");
        }

        [HttpPost]
        public IActionResult DeleteCoupon(string id)
        {
            var coupon = _context.Coupons.Find(id);
            if (coupon != null) { _context.Coupons.Remove(coupon); _context.SaveChanges(); }
            return RedirectToAction("Coupons");
        }

        // ==========================================
        // 6. QUẢN LÝ ĐÁNH GIÁ (Reviews)
        // ==========================================
        public IActionResult Reviews()
{
    // Lấy danh sách đánh giá
    var reviews = _context.Reviews.OrderByDescending(r => r.CreatedAt).ToList();
    
    // Tạo từ điển tra cứu tên sản phẩm để hiển thị thay vì chỉ hiện ID
    ViewBag.ProductNames = _context.Products.ToDictionary(p => p.Id, p => p.Name);
    
    return View(reviews);
}

// Thêm hàm xử lý trả lời đánh giá
[HttpPost]
public IActionResult ReplyReview(string id, string adminReply)
{
    var review = _context.Reviews.Find(id);
    if (review != null)
    {
        review.AdminReply = adminReply;
        review.ReplyDate = DateTime.Now;
        _context.SaveChanges();
    }
    return RedirectToAction("Reviews");
}

[HttpPost]
public IActionResult DeleteReview(string id)
{
    var review = _context.Reviews.Find(id);
    if (review != null) 
    { 
        _context.Reviews.Remove(review); 
        _context.SaveChanges(); 
    }
    return RedirectToAction("Reviews");
}
        

        // ==========================================
        // HÀM HỖ TRỢ (HELPER)
        // ==========================================
        private async Task<string> SaveImage(IFormFile file)
        {
            string folder = Path.Combine(_env.WebRootPath, "images", "products");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string fileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(folder, fileName);

            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }
            return "/images/products/" + fileName;
        }

        // 1. Xem danh sách tin nhắn
public IActionResult Messages()
{
    var msgs = _context.ContactMessages.OrderByDescending(m => m.CreatedAt).ToList();
    return View(msgs);
}

// 2. Đánh dấu đã đọc
[HttpPost]
public IActionResult MarkAsRead(string id)
{
    var msg = _context.ContactMessages.Find(id);
    if (msg != null) { msg.IsRead = true; _context.SaveChanges(); }
    return RedirectToAction("Messages");
}

// 3. Xóa tin nhắn
[HttpPost]
public IActionResult DeleteMessage(string id)
{
    var msg = _context.ContactMessages.Find(id);
    if (msg != null) { _context.ContactMessages.Remove(msg); _context.SaveChanges(); }
    return RedirectToAction("Messages");
}
    }
}