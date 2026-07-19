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
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
            {
                user.ResetPasswordToken = Guid.NewGuid().ToString();
                user.ResetPasswordTokenExpiry = DateTime.UtcNow.AddMinutes(15);
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                var resetLink = Url.Action("ResetPassword", "Account", new { email = user.Email, token = user.ResetPasswordToken }, Request.Scheme);
                await SendEmailAsync(user.Email, "Khôi phục mật khẩu - BuildMat", $"Click vào link để đặt lại mật khẩu: {resetLink}");
            }
            ViewBag.Message = "Nếu email chính xác, link khôi phục đã được gửi.";
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
    }
}