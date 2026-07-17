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

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // Danh sách danh mục dùng chung cho Drop-down
        private List<string> CategoryList = new List<string> { 
            "Sắt thép xây dựng", "Xi măng", "Cát, Đá", "Gạch xây dựng", "Sơn & Chống thấm", "Gạch ốp lát", "Vật liệu khác" 
        };

        // --- CODE CŨ CỦA BẠN (GIỮ NGUYÊN) ---
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

        // --- PHẦN QUẢN LÝ SẢN PHẨM ---

        public IActionResult Products()
        {
            var products = _context.Products.OrderByDescending(p => p.CreatedAt).ToList();
            return View(products);
        }

        public IActionResult CreateProduct() 
        {
            ViewBag.Categories = CategoryList; // Truyền danh sách ra Drop-down
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateProduct(Product product, IFormFile? imageFile, [FromServices] IWebHostEnvironment env)
        {
            // Fix lỗi NULL Description nếu người dùng quên nhập
            if (string.IsNullOrEmpty(product.Description)) product.Description = "Đang cập nhật nội dung...";

            if (imageFile != null) product.ImageUrl = await SaveImage(imageFile, env);
            
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction("Products");
        }

        public IActionResult EditProduct(string id)
        {
            var product = _context.Products.Find(id);
            if (product == null) return NotFound();
            
            ViewBag.Categories = CategoryList; // Truyền danh sách ra Drop-down
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> EditProduct(Product product, IFormFile? imageFile, [FromServices] IWebHostEnvironment env)
        {
            var existing = _context.Products.AsNoTracking().FirstOrDefault(p => p.Id == product.Id);
            
            if (imageFile != null) product.ImageUrl = await SaveImage(imageFile, env);
            else product.ImageUrl = existing?.ImageUrl;

            if (string.IsNullOrEmpty(product.Description)) product.Description = existing?.Description ?? "Đang cập nhật...";

            _context.Update(product);
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

        private async Task<string> SaveImage(IFormFile file, IWebHostEnvironment env)
        {
            string folder = Path.Combine(env.WebRootPath, "images", "products");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            string fileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(folder, fileName);
            using (var fs = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(fs); }
            return "/images/products/" + fileName;
        }
    }
}