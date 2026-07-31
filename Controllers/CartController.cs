using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebBanVLXD.Models;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Net.payOS;
using Net.payOS.Types;  
using Microsoft.EntityFrameworkCore;

namespace WebBanVLXD.Controllers
{
    public class CartController : Controller
    {
        private readonly AppDbContext _context;
        private readonly Net.payOS.PayOS _payOS;

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
        // ĐÃ SỬA: Thêm tham số string? variantId
        public IActionResult AddToCart(string productId, int quantity = 1, string? variantId = null, string submitAction = "add")
        {
            var product = _context.Products.Find(productId);
            if (product == null) return NotFound();

            var cart = GetCartItems();
            
            // ĐÃ SỬA: Tìm kiếm dựa trên CẢ productId và variantId để không bị gộp chung
            var item = cart.FirstOrDefault(i => i.Product.Id == productId && i.VariantId == variantId);

            if (item != null) 
            {
                item.Quantity += quantity;
            }
            else 
            {
                // ĐÃ SỬA: Xử lý lấy Tên, Hình ảnh và Giá của Phân loại (Variant)
                string finalVariantName = "";
                string finalImageUrl = product.ImageUrl ?? "";
                decimal finalPrice = product.Price;

                if (!string.IsNullOrEmpty(variantId))
                {
                    // LƯU Ý: Nếu bảng phân loại trong AppDbContext của bạn tên khác, hãy đổi "ProductVariants" thành tên đúng
                    var variant = _context.ProductVariants.Find(variantId); 
                    if (variant != null)
                    {
                        finalVariantName = variant.Name; // Lấy tên phân loại
                        finalPrice = variant.Price > 0 ? variant.Price : product.Price;
                        
                        // Nếu phân loại có ảnh riêng thì lấy, không thì dùng ảnh gốc
                        if (!string.IsNullOrEmpty(variant.ImageUrl)) 
                        {
                            finalImageUrl = variant.ImageUrl;
                        }
                    }
                }

                cart.Add(new CartItem { 
                    Product = product, 
                    Quantity = quantity,
                    VariantId = variantId,
                    VariantName = finalVariantName, // Lưu tên phân loại
                    ImageUrl = finalImageUrl,       // Lưu ảnh phân loại
                    Price = finalPrice              // Lưu giá chuẩn
                });
            }

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
        
        // ĐÃ SỬA: Cập nhật hàm Remove để xóa đúng phân loại
        public IActionResult Remove(string productId, string? variantId)
        {
            var cart = GetCartItems();
            cart.RemoveAll(i => i.Product.Id == productId && i.VariantId == variantId);
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

            var validOrderDetails = new List<OrderDetail>();
            decimal totalAmount = 0;

            foreach (var item in cart)
            {
                var dbProduct = _context.Products.Find(item.Product.Id);
                if (dbProduct != null)
                {
                    //Lấy giá từ item.Price (đã gán lúc AddToCart) thay vì mặc định lấy giá gốc của Product
                    decimal finalPrice = item.Price > 0 ? item.Price : dbProduct.Price;

                    validOrderDetails.Add(new OrderDetail
                    {
                        ProductId = dbProduct.Id,
                        Quantity = item.Quantity,
                        Price = finalPrice // Đảm bảo lưu đúng giá
                    });

                    // Tính tổng tiền dựa trên giá chính xác
                    totalAmount += finalPrice * item.Quantity;

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
        // ĐÃ SỬA: Bổ sung tham số variantId
        public IActionResult AddToCartAjax(string productId, int quantity = 1, string? variantId = null)
        {
            var product = _context.Products.Find(productId);
            if (product == null) return Json(new { success = false, message = "Không tìm thấy sản phẩm!" });

            var cart = GetCartItems();
            
            // ĐÃ SỬA: Chặn việc cộng dồn nếu khác phân loại
            var item = cart.FirstOrDefault(i => i.Product.Id == productId && i.VariantId == variantId);

            if (item != null) 
            {
                item.Quantity += quantity;
            }
            else 
            {
                //Xử lý lấy Tên, Hình ảnh và Giá của Phân loại (Variant)
                string finalVariantName = "";
                string? finalImageUrl = product.ImageUrl ?? "";
                decimal finalPrice = product.Price;

                if (!string.IsNullOrEmpty(variantId))
                {
                    var variant = _context.ProductVariants.Find(variantId);
                    if (variant != null)
                    {
                        finalVariantName = variant.Name;
                        finalPrice = variant.Price > 0 ? variant.Price : product.Price;
                        
                        if (!string.IsNullOrEmpty(variant.ImageUrl)) 
                        {
                            finalImageUrl = variant.ImageUrl;
                        }
                    }
                }

                // ĐÃ SỬA: Gán Price, Name, ImageUrl chuẩn để giỏ hàng hiển thị đúng
                cart.Add(new CartItem { 
                    Product = product, 
                    Quantity = quantity,
                    VariantId = variantId,
                    VariantName = finalVariantName,
                    ImageUrl = finalImageUrl,
                    Price = finalPrice
                });
            }

            SaveCartItems(cart);

            return Json(new { success = true, message = $"Đã thêm {product.Name} vào giỏ hàng!", cartCount = cart.Sum(i => i.Quantity) });
        }

        [HttpGet]
        public IActionResult GetMiniCart()
        {
            var cart = GetCartItems();
            return PartialView("_MiniCart", cart);
        }
        
        [HttpPost]
        public IActionResult UpdateQuantity(string productId, string? variantId, int quantity)
        {
            var cart = GetCartItems();
            var item = cart.FirstOrDefault(i => i.Product.Id == productId && i.VariantId == variantId);
            if (item != null)
            {
                if (quantity > 0) item.Quantity = quantity;
                else cart.Remove(item);
            }
            SaveCartItems(cart);
            return RedirectToAction("Index");
        }
    }
}