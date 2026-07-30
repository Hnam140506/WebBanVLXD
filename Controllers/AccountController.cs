using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebBanVLXD.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;


namespace WebBanVLXD.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;

        public AccountController(AppDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }
        [HttpGet]
        public IActionResult InitAdmin()
        {
            string adminEmail = "admin@gmail.com"; // Thay bằng email bạn muốn
            string adminPass = "123456";          // Thay bằng mật khẩu bạn muốn

            var existingUser = _context.Users.FirstOrDefault(u => u.Email == adminEmail);
            if (existingUser == null)
            {
                var adminUser = new User
                {
                    UserName = "SuperAdmin",
                    Email = adminEmail,
                    Role = "Admin", // Quan trọng: Gán quyền Admin ở đây
                    CreatedAt = DateTime.UtcNow
                };

                adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, adminPass);

                _context.Users.Add(adminUser);
                _context.SaveChanges();

                return Content($"Đã tạo tài khoản Admin thành công! Email: {adminEmail} | Pass: {adminPass}");
            }

            return Content("Tài khoản này đã tồn tại hoặc đã là Admin.");
        }

        // ==========================================
        // --- ĐĂNG KÝ (Hàm xử lý duy nhất) ---
        // ==========================================
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public IActionResult Register(string username, string email, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận không trùng khớp!");
                return View();
            }

            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                ModelState.AddModelError("", "Mật khẩu quá yếu! Vui lòng nhập ít nhất 6 ký tự.");
                return View();
            }

            if (_context.Users.Any(u => u.Email == email || u.UserName == username))
            {
                ModelState.AddModelError("", "Email hoặc Tên tài khoản đã tồn tại!");
                return View();
            }

            var newUser = new User
            {
                UserName = username,
                Email = email,
                ThemePreference = "system",
                CreatedAt = DateTime.UtcNow
            };

            newUser.PasswordHash = _passwordHasher.HashPassword(newUser, password);
            _context.Users.Add(newUser);
            _context.SaveChanges();

            return RedirectToAction("Login");
        }

        // ==========================================
        // --- ĐĂNG NHẬP ---
        // ==========================================
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không chính xác!");
                return View();
            }

            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                ModelState.AddModelError("", "Tài khoản này dùng MXH. Vui lòng đăng nhập qua Google/FB.");
                return View();
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không chính xác!");
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "Customer")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

        public IActionResult Logout()
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // --- ĐĂNG NHẬP MẠNG XÃ HỘI ---
        // ==========================================
        [HttpPost]
        public IActionResult ExternalLogin(string provider)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account");
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, provider);
        }

        public async Task<IActionResult> ExternalLoginCallback()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded) return RedirectToAction("Login");

            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            var name = result.Principal.Identity?.Name;
            var provider = result.Principal.Identity?.AuthenticationType;

            if (string.IsNullOrEmpty(email)) return RedirectToAction("Login");

            var providerTarget = $"{provider}:{email}";
            var user = _context.Users.FirstOrDefault(u => u.AuthProvider != null && u.AuthProvider.Contains(providerTarget));

            if (user == null)
            {
                user = _context.Users.FirstOrDefault(u => u.Email == email);
                if (user == null)
                {
                    user = new User
                    {
                        Email = email,
                        UserName = name ?? email.Split('@')[0],
                        CreatedAt = DateTime.UtcNow,
                        PasswordHash = "",
                        AuthProvider = providerTarget
                    };
                    _context.Users.Add(user);
                }
                else
                {
                    user.AuthProvider = (user.AuthProvider ?? "") + "," + providerTarget;
                    _context.Users.Update(user);
                }
                _context.SaveChanges();
            }

            var claims = new List<Claim> {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "Customer")
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // --- QUÊN MẬT KHẨU & EMAIL ---
        // ==========================================
        // ==========================================
        // --- QUÊN MẬT KHẨU & GỬI MÃ OTP QUA EMAIL ---
        // ==========================================
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
            {
                // 1. Tạo mã OTP ngẫu nhiên 6 chữ số
                string otpCode = new Random().Next(100000, 999999).ToString();

                // 2. Lưu OTP và thời gian hết hạn (15 phút) vào Database
                user.ResetPasswordToken = otpCode;
                user.ResetPasswordTokenExpiry = DateTime.UtcNow.AddMinutes(15);
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                // 3. Gửi email chứa mã OTP
                string emailSubject = "Mã xác nhận khôi phục mật khẩu - BuildSmart";
                string emailBody = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                        <h2 style='color: #0b224c;'>Khôi phục mật khẩu tài khoản BuildSmart</h2>
                        <p>Xin chào <b>{user.UserName}</b>,</p>
                        <p>Bạn nhận được email này vì đã yêu cầu đặt lại mật khẩu cho tài khoản của mình.</p>
                        <p>Mã xác nhận (OTP) của bạn là:</p>
                        <h1 style='color: #f57224; letter-spacing: 5px; background: #f4f7f6; padding: 10px; text-align: center; width: 200px; border-radius: 5px;'>{otpCode}</h1>
                        <p>Mã này có hiệu lực trong vòng <b>15 phút</b>. Vui lòng không chia sẻ mã này cho bất kỳ ai.</p>
                        <hr style='border: none; border-top: 1px solid #eee;' />
                        <p style='font-size: 12px; color: #777;'>Trân trọng,<br>Đội ngũ BuildSmart</p>
                    </div>";

                await SendEmailAsync(user.Email, emailSubject, emailBody);
            }

            // Chuyển hướng sang trang nhập OTP, truyền kèm email để người dùng không phải gõ lại
            TempData["Email"] = email;
            return RedirectToAction("VerifyOtp");
        }
[HttpGet]
        public IActionResult VerifyOtp()
        {
            ViewBag.Email = TempData["Email"] ?? "";
            TempData.Keep("Email"); // Giữ lại email nếu cần reload
            return View();
        }

        [HttpPost]
        public IActionResult VerifyOtp(string email, string otpCode)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email && u.ResetPasswordToken == otpCode);

            if (user != null && user.ResetPasswordTokenExpiry > DateTime.UtcNow)
            {
                // OTP đúng và còn hạn -> Chuyển sang trang đổi mật khẩu mới, truyền kèm token/otp xác thực
                return RedirectToAction("ResetPasswordWithOtp", new { email = email, token = otpCode });
            }

            ModelState.AddModelError("", "Mã OTP không chính xác hoặc đã hết hạn!");
            ViewBag.Email = email;
            return View();
        }

        [HttpGet]
        public IActionResult ResetPasswordWithOtp(string email, string token)
        {
            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPasswordWithOtp(string email, string token, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword) 
            { 
                ModelState.AddModelError("", "Mật khẩu xác nhận không khớp!"); 
                ViewBag.Email = email;
                ViewBag.Token = token;
                return View(); 
            }

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                ModelState.AddModelError("", "Mật khẩu phải có ít nhất 6 ký tự!");
                ViewBag.Email = email;
                ViewBag.Token = token;
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email && u.ResetPasswordToken == token);
            if (user != null && user.ResetPasswordTokenExpiry > DateTime.UtcNow)
            {
                // Mã hóa mật khẩu mới và lưu lại
                user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
                
                // Xóa token OTP để không bị tái sử dụng
                user.ResetPasswordToken = null;
                user.ResetPasswordTokenExpiry = null;
                
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đổi mật khẩu thành công! Vui lòng đăng nhập lại.";
                return RedirectToAction("Login");
            }

            ModelState.AddModelError("", "Phiên làm việc đã hết hạn hoặc không hợp lệ.");
            return View();
        }
        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string email, string token, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword) { ModelState.AddModelError("", "Mật khẩu không khớp"); return View(); }
            var user = _context.Users.FirstOrDefault(u => u.Email == email && u.ResetPasswordToken == token);
            if (user != null && user.ResetPasswordTokenExpiry > DateTime.UtcNow)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
                user.ResetPasswordToken = null;
                await _context.SaveChangesAsync();
                return RedirectToAction("Login");
            }
            ModelState.AddModelError("", "Link không hợp lệ hoặc hết hạn.");
            return View();
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            using var smtp = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("nam1452000@gmail.com", "sbbb xitb zgij cnog"),
                EnableSsl = true,
            };
            var mail = new MailMessage("nam1452000@gmail.com", toEmail, subject, body) { IsBodyHtml = true };
            await smtp.SendMailAsync(mail);
        }
        // ==========================================
        // 4. QUẢN LÝ NGƯỜI DÙNG
        // ==========================================
        public IActionResult Users()
        {
            var users = _context.Users.OrderByDescending(u => u.CreatedAt).ToList();
            return View(users);
        }

        [HttpPost]
        public IActionResult UpdateUserRole(string userId, string newRole)
        {
            var user = _context.Users.Find(userId);
            if (user != null)
            {
                user.Role = newRole;
                _context.SaveChanges();
            }
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
        // 5. QUẢN LÝ MÃ GIẢM GIÁ (COUPON)
        // ==========================================
        public IActionResult Coupons()
        {
            var coupons = _context.Coupons.OrderByDescending(c => c.ExpiryDate).ToList();
            return View(coupons);
        }

        public IActionResult CreateCoupon() => View();

        [HttpPost]
        public IActionResult CreateCoupon(Coupon coupon)
        {
            if (ModelState.IsValid)
            {
                _context.Coupons.Add(coupon);
                _context.SaveChanges();
                return RedirectToAction("Coupons");
            }
            return View(coupon);
        }

        [HttpPost]
        public IActionResult DeleteCoupon(string id)
        {
            var coupon = _context.Coupons.Find(id);
            if (coupon != null) { _context.Coupons.Remove(coupon); _context.SaveChanges(); }
            return RedirectToAction("Coupons");
        }

        // ==========================================
        // 6. QUẢN LÝ ĐÁNH GIÁ (REVIEW)
        // ==========================================
        public IActionResult Reviews()
        {
            // Lấy đánh giá kèm thông tin tên sản phẩm (nếu cần)
            var reviews = _context.Reviews.OrderByDescending(r => r.CreatedAt).ToList();
            return View(reviews);
        }

        [HttpPost]
        public IActionResult DeleteReview(string id)
        {
            var review = _context.Reviews.Find(id);
            if (review != null) { _context.Reviews.Remove(review); _context.SaveChanges(); }
            return RedirectToAction("Reviews");
        }
        [Authorize]
        public IActionResult Profile()
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            var user = _context.Users.Find(userId);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UpdateProfile(User model)
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            var user = _context.Users.Find(userId);

            if (user != null)
            {
                user.FullName = model.FullName;
                user.DateOfBirth = model.DateOfBirth;
                user.Gender = model.Gender;
                user.PhoneNumber = model.PhoneNumber;
                user.Address = model.Address;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                ViewBag.Message = "Cập nhật thông tin thành công!";
            }
            return View("Profile", user);
        }
        // ==========================================
        // --- ĐỔI MẬT KHẨU (KHI ĐÃ ĐĂNG NHẬP) ---
        // ==========================================
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            // 1. Kiểm tra xác nhận mật khẩu mới có khớp nhau không
            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận mới không khớp!");
                return View();
            }

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                ModelState.AddModelError("", "Mật khẩu mới phải có ít nhất 6 ký tự!");
                return View();
            }

            // 2. Lấy ID của user đang đăng nhập hiện tại
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // 3. Kiểm tra mật khẩu hiện tại có đúng không bằng PasswordHasher
            var passwordVerification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
            if (passwordVerification == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError("", "Mật khẩu hiện tại không chính xác!");
                return View();
            }

            // 4. Mã hóa và lưu mật khẩu mới
            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("Index", "Home"); // Hoặc điều hướng về trang cá nhân / thông báo thành công
        }
    }
}