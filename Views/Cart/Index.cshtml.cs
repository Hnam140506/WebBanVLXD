using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using WebBanVLXD.Models; // Khai báo để dùng CartItem và Product

namespace WebBanVLXD.Pages.Cart
{
    public class IndexModel : PageModel
    {
        // Bạn nên sử dụng Session hoặc Cookie để lưu giỏ hàng tạm thời
        public List<CartItem> Items { get; set; } = new List<CartItem>();
        public decimal TotalAmount => Items.Sum(i => i.Product.Price * i.Quantity);

        public void OnGet()
        {
            // Load danh sách sản phẩm từ Session vào biến Items
        }
    }
}