using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebBanVLXD.Models;
using System.Linq;
using System;

namespace WebBanVLXD.Controllers
{
    [Authorize(Roles = "Admin")] // Chỉ Admin mới vào được
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // Đây là hàm xử lý cho địa chỉ /Admin
        public IActionResult Index()
        {
            var orders = _context.Orders.ToList();
            
            // Tính toán các con số thống kê
            ViewBag.TotalRevenue = orders.Where(o => o.Status == "Hoàn thành").Sum(o => o.TotalAmount);
            ViewBag.OrderCount = orders.Count;
            ViewBag.ProductCount = _context.Products.Count();
            ViewBag.UserCount = _context.Users.Count();

            // Dữ liệu cho biểu đồ 7 ngày
            var reportData = _context.Orders
                .Where(o => o.OrderDate >= DateTime.Now.AddDays(-7))
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key.ToString("dd/MM"), Total = g.Sum(o => o.TotalAmount) })
                .ToList();

            ViewBag.ChartLabels = reportData.Select(d => d.Date).ToList();
            ViewBag.ChartValues = reportData.Select(d => d.Total).ToList();

            return View();
        }

        // Hàm xử lý cho địa chỉ /Admin/Orders
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
    }
}