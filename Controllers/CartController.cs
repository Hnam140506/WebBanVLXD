using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebBanVLXD.Models;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Net.payOS;        // Dùng đúng thư viện gốc
using Net.payOS.Types;  // Dùng đúng thư viện gốc

namespace WebBanVLXD.Controllers
{
    public class CartController : Controller
    {
        private readonly AppDbContext _context;
        private readonly Net.payOS.PayOS _payOS; // Tường minh kiểu dữ liệu

        public CartController(AppDbContext context, Net.payOS.PayOS payOS)
        {
            _context = context;
            _payOS = payOS;
        }

        // ==========================================
        // CÁC HÀM XỬ LÝ GIỎ HÀNG
        // ==========================================
        private List<CartItem> GetCartItems()
        {
            var sessionCart = HttpContext.Session.GetString("Cart");
            if (sessionCart != null) return JsonConvert.DeserializeObject<List<CartItem>>(sessionCart)!;
            return new List<CartItem>();
        }

        private void SaveCartItems(List<CartItem> cart) =>
            HttpContext.Session.SetString("Cart", JsonConvert.SerializeObject(cart));

        public IActionResult Index() => View(GetCartItems());

        [HttpPost]
        public IActionResult AddToCart(string productId, int quantity = 1, string submitAction = "add")
        {
            var product = _context.Products.Find(productId);
            if (product == null) return NotFound();

            var cart = GetCartItems();
            var item = cart.FirstOrDefault(i => i.Product.Id == productId);

            if (item != null) item.Quantity += quantity;
            else cart.Add(new CartItem { Product = product, Quantity = quantity });

            SaveCartItems(cart);

            // KIỂM TRA HÀNH ĐỘNG CỦA NÚT SUBMIT
            if (submitAction == "buy")
            {
                // Nhấn "Mua ngay" -> Tới trang Checkout
                return RedirectToAction("Checkout");
            }

            // Nhấn "Thêm vào giỏ" -> Điều hướng khách ở lại đúng trang hiện tại
            string refererUrl = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(refererUrl))
            {
                return Redirect(refererUrl);
            }

            // Nếu không lấy được URL trang trước đó, đưa về trang Giỏ hàng mặc định
            return RedirectToAction("Index");
        }
        public IActionResult Remove(string productId)
        {
            var cart = GetCartItems();
            cart.RemoveAll(i => i.Product.Id == productId);
            SaveCartItems(cart);
            return RedirectToAction("Index");
        }

        // ==========================================
        // MÀN HÌNH ĐIỀN THÔNG TIN THANH TOÁN
        // ==========================================
        [HttpGet]
        public IActionResult Checkout()
        {
            var cart = GetCartItems();
            if (cart.Count == 0) return RedirectToAction("Index");
            return View(cart);
        }

        // ==========================================
        // XỬ LÝ ĐẶT HÀNG & GỌI PAYOS
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> ProcessCheckout(string customerName, string phone, string address, string paymentMethod, string couponCode)
        {
            var cart = GetCartItems();
            if (cart.Count == 0) return RedirectToAction("Index");

            // BƯỚC CẢI TIẾN: Lọc các sản phẩm còn tồn tại trong DB, lấy giá thật từ DB để chống hack
            var validOrderDetails = new List<OrderDetail>();
            decimal totalAmount = 0;

            foreach (var item in cart)
            {
                var dbProduct = _context.Products.Find(item.Product.Id);
                if (dbProduct != null)
                {
                    validOrderDetails.Add(new OrderDetail
                    {
                        ProductId = dbProduct.Id,
                        Quantity = item.Quantity,
                        Price = dbProduct.Price // Lấy giá chuẩn từ Database
                    });

                    // Tính tổng tiền dựa trên sản phẩm thật
                    totalAmount += dbProduct.Price * item.Quantity;

                    // Trừ tồn kho luôn
                    dbProduct.StockQuantity -= item.Quantity;
                }
            }

            // Nếu sản phẩm trong giỏ đã bị admin xóa hết khỏi DB -> Xóa Session và báo lỗi
            if (validOrderDetails.Count == 0)
            {
                HttpContext.Session.Remove("Cart");
                return RedirectToAction("Index");
            }

            // XỬ LÝ MÃ GIẢM GIÁ
            decimal discountAmount = 0;

            if (!string.IsNullOrEmpty(couponCode))
            {
                var coupon = _context.Coupons.FirstOrDefault(c =>
                    c.Code == couponCode &&
                    c.IsActive &&
                    c.ExpiryDate >= DateTime.Now);

                if (coupon != null)
                {
                    discountAmount = coupon.DiscountAmount;

                    // Không cho giảm lớn hơn tổng tiền đơn hàng
                    if (discountAmount > totalAmount)
                    {
                        discountAmount = totalAmount;
                    }
                }
            }

            // TẠO ĐƠN HÀNG
            var order = new Order
            {
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                CustomerName = customerName,
                Phone = phone,
                Address = address,
                TotalAmount = totalAmount - discountAmount, // Trừ mã giảm giá sau khi cộng tổng
                PaymentMethod = paymentMethod,
                CouponCode = couponCode,
                DiscountAmount = discountAmount,
                Status = paymentMethod == "BankTransfer" ? "Chờ thanh toán" : "Chờ xử lý",
                OrderDetails = validOrderDetails // Chỉ dùng danh sách các sản phẩm hợp lệ
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            
            // Xóa session giỏ hàng sau khi đặt thành công
            HttpContext.Session.Remove("Cart"); 

            // XỬ LÝ PAYOS NẾU CHỌN CHUYỂN KHOẢN NGÂN HÀNG
            if (paymentMethod == "BankTransfer")
            {
                var domain = $"{Request.Scheme}://{Request.Host}";
                long orderCode = long.Parse(DateTimeOffset.Now.ToString("yyMMddHHmmss"));

                var items = new List<ItemData> {
                    new ItemData("Thanh toan BuildSmart", 1, (int)order.TotalAmount)
                };

                var paymentData = new PaymentData(
                    orderCode: orderCode,
                    amount: (int)order.TotalAmount,
                    description: "Thanh toan VLXD",
                    items: items,
                    cancelUrl: $"{domain}/Cart/PaymentCallback?orderId={order.Id}&success=false",
                    returnUrl: $"{domain}/Cart/PaymentCallback?orderId={order.Id}&success=true"
                );

                var createPayment = await _payOS.createPaymentLink(paymentData);
                return Redirect(createPayment.checkoutUrl);
            }

            return View("OrderSuccess", order);
        }

        // ==========================================
        // XỬ LÝ KẾT QUẢ TỪ PAYOS TRẢ VỀ
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> PaymentCallback(string orderId, bool success)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            if (success)
            {
                order.Status = "Đã thanh toán";
                await _context.SaveChangesAsync();
                return View("OrderSuccess", order);
            }
            else
            {
                order.Status = "Đã hủy";
                await _context.SaveChangesAsync();
                return RedirectToAction("Index");
            }
        }
        // ==========================================
        // TÍNH NĂNG AJAX & MINI CART
        // ==========================================
        [HttpPost]
        public IActionResult AddToCartAjax(string productId, int quantity = 1)
        {
            var product = _context.Products.Find(productId);
            if (product == null) return Json(new { success = false, message = "Không tìm thấy sản phẩm!" });

            var cart = GetCartItems();
            var item = cart.FirstOrDefault(i => i.Product.Id == productId);

            if (item != null) item.Quantity += quantity;
            else cart.Add(new CartItem { Product = product, Quantity = quantity });

            SaveCartItems(cart);

            return Json(new { success = true, message = $"Đã thêm {product.Name} vào giỏ hàng!", cartCount = cart.Sum(i => i.Quantity) });
        }

        [HttpGet]
        public IActionResult GetMiniCart()
        {
            var cart = GetCartItems();
            return PartialView("_MiniCart", cart);
        }
    }
}