using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebBanVLXD.Models;
using System.Security.Claims;
using Newtonsoft.Json;
using System.Linq;

namespace WebBanVLXD.Controllers {
    public class CartController : Controller {
        private readonly AppDbContext _context;
        public CartController(AppDbContext context) => _context = context;

        // Lấy giỏ hàng từ Session
        private List<CartItem> GetCartItems() {
            var sessionCart = HttpContext.Session.GetString("Cart");
            if (sessionCart != null) return JsonConvert.DeserializeObject<List<CartItem>>(sessionCart)!;
            return new List<CartItem>();
        }

        // Lưu giỏ hàng vào Session
        private void SaveCartItems(List<CartItem> cart) => 
            HttpContext.Session.SetString("Cart", JsonConvert.SerializeObject(cart));

        public IActionResult Index() => View(GetCartItems());

        [HttpPost]
        public IActionResult AddToCart(string productId, int quantity = 1) {
            var product = _context.Products.Find(productId);
            if (product == null) return NotFound();

            var cart = GetCartItems();
            var item = cart.FirstOrDefault(i => i.Product.Id == productId);

            if (item != null) item.Quantity += quantity;
            else cart.Add(new CartItem { Product = product, Quantity = quantity });

            SaveCartItems(cart);
            return RedirectToAction("Index");
        }

        public IActionResult Remove(string productId) {
            var cart = GetCartItems();
            cart.RemoveAll(i => i.Product.Id == productId);
            SaveCartItems(cart);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(string customerName, string phone, string address) {
            var cart = GetCartItems();
            if (cart.Count == 0) return RedirectToAction("Index");

            var order = new Order {
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                CustomerName = customerName,
                Phone = phone,
                Address = address,
                TotalAmount = cart.Sum(i => i.Product.Price * i.Quantity),
                OrderDetails = cart.Select(i => new OrderDetail {
                    ProductId = i.Product.Id,
                    Quantity = i.Quantity,
                    Price = i.Product.Price
                }).ToList()
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            
            HttpContext.Session.Remove("Cart"); // Xóa giỏ sau khi đặt
            return View("OrderSuccess", order);
        }
    }
}