using CafeDash.Data;
using CafeDash.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CafeDash.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Shows the Welcome Landing Page
        [HttpGet]
        public IActionResult Welcome()
        {
            return View();
        }

        // 1. Shows the Login Page
        [HttpGet]
        public IActionResult Login()
        {
            ViewBag.Title = "Login - Cafe Dash";
            return View();
        }

        // 2. Handles the Login Submission using Entity Framework Core
        [HttpPost]
        public async Task<IActionResult> Login(string User_name, string password)
        {
            // ==========================================
            // 1. CAPTCHA SECURITY CHECK
            // ==========================================
            string captchaResponse = Request.Form["g-recaptcha-response"];
            bool isCaptchaValid = await VerifyCaptcha(captchaResponse);

            if (!isCaptchaValid)
            {
                ViewBag.Error = "Please complete the CAPTCHA to prove you are human.";
                return View();
            }

            // ==========================================
            // 2. NORMAL LOGIN LOGIC (EF CORE)
            // ==========================================
            if (string.IsNullOrEmpty(User_name) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please enter both username and password.";
                return View();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.User_name == User_name || u.Email == User_name);

            if (user != null)
            {
                if (user.Suspend == 1)
                {
                    ViewBag.Error = "Your account has been suspended. Please contact the administrator.";
                    return View();
                }

                bool isValidPassword = false;

                if (password == user.Password)
                {
                    isValidPassword = true;
                }
                else
                {
                    try { isValidPassword = BCrypt.Net.BCrypt.Verify(password, user.Password); }
                    catch { isValidPassword = false; }
                }

                if (isValidPassword)
                {
                    HttpContext.Session.SetInt32("user_id", user.User_ID);
                    HttpContext.Session.SetString("User_name", user.User_name ?? "");

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ViewBag.Error = "Incorrect Password!";
                }
            }
            else
            {
                ViewBag.Error = "User not found!";
            }

            return View();
        }

        // 3. Handles Logging Out
        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        // 4. Shows the Registration Page
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // 5. Handles Registration Form Submission using EF Core
        [HttpPost]
        public async Task<IActionResult> Register(string user_name, string email, string phone, string address, string password, string confirm_password)
        {
            if (string.IsNullOrWhiteSpace(user_name) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(address) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "All fields are required.";
                return View();
            }

            if (password.Length < 8)
            {
                ViewBag.Error = "Password must be at least 8 characters.";
                return View();
            }

            if (password != confirm_password)
            {
                ViewBag.Error = "Password and Confirm Password do not match.";
                return View();
            }

            // Check if user already exists via EF Core
            bool userExists = await _context.Users
                .AnyAsync(u => u.User_name == user_name || u.Email == email);

            if (userExists)
            {
                ViewBag.Error = "Username or email already exists. Please use a different one.";
                return View();
            }

            // Create and insert the new user object
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var newUser = new User
            {
                User_name = user_name,
                Email = email,
                Contain_number = phone,
                Address = address,
                Password = hashedPassword,
                Suspend = 0
            };

            _context.Users.Add(newUser);
            int result = await _context.SaveChangesAsync();

            if (result > 0)
            {
                ViewBag.Success = "Registration successful. You can now login with your account.";
            }
            else
            {
                ViewBag.Error = "System error: unable to complete registration right now.";
            }

            return View();
        }

        // Helper to hash the token with SHA256
        private string HashToken(string token)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        // ==========================================
        // FORGOT PASSWORD
        // ==========================================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email_or_username)
        {
            if (string.IsNullOrWhiteSpace(email_or_username))
            {
                ViewBag.Error = "Please enter your registered email or username.";
                return View();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.User_name == email_or_username.Trim() || u.Email == email_or_username.Trim());

            if (user != null)
            {
                string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();
                string tokenHash = HashToken(token);
                DateTime now = DateTime.UtcNow;

                // A reset link is only useful if its hashed token is persisted.  Keep
                // one active token per user so a newer request invalidates older links.
                var existingTokens = await _context.PasswordResets
                    .Where(reset => reset.user_id == user.User_ID)
                    .ToListAsync();
                _context.PasswordResets.RemoveRange(existingTokens);
                _context.PasswordResets.Add(new PasswordReset
                {
                    user_id = user.User_ID,
                    token_hash = tokenHash,
                    expires_at = now.AddHours(1),
                    created_at = now
                });
                await _context.SaveChangesAsync();

                string resetLink = Url.Action("ResetPassword", "Account", new { token = token }, Request.Scheme)!;

                try
                {
                    var _config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();

                    using (var smtp = new SmtpClient(_config["Smtp:Host"]!, int.Parse(_config["Smtp:Port"]!)))
                    {
                        smtp.Credentials = new System.Net.NetworkCredential(_config["Smtp:Username"], _config["Smtp:Password"]);
                        smtp.EnableSsl = true;
                        var mailMessage = new MailMessage
                        {
                            From = new MailAddress(_config["Smtp:FromEmail"]!, _config["Smtp:FromName"]),
                            Subject = "CafeDash Password Reset",
                            Body = $"<p>You requested a password reset. Click the link below to reset your password:</p><p><a href='{resetLink}'>{resetLink}</a></p><p>This link expires in 1 hour.</p>",
                            IsBodyHtml = true
                        };
                        mailMessage.To.Add(user.Email!);

                        smtp.Send(mailMessage);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Email failed to send: " + ex.Message);
                }
            }

            ViewBag.Success = "If the account exists, a reset link has been sent to the registered email.";
            return View();
        }

        // ==========================================
        // RESET PASSWORD
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || !await HasValidResetToken(token))
            {
                ViewBag.Error = "Reset token is missing or invalid.";
            }
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string token, string password, string confirm_password)
        {
            ViewBag.Token = token;

            if (string.IsNullOrWhiteSpace(token))
            {
                ViewBag.Error = "Reset token is missing or invalid.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                ViewBag.Error = "Password must be at least 8 characters.";
                return View();
            }

            if (password != confirm_password)
            {
                ViewBag.Error = "Password and Confirm Password do not match.";
                return View();
            }

            string tokenHash = HashToken(token);
            var passwordReset = await _context.PasswordResets
                .FirstOrDefaultAsync(reset => reset.token_hash == tokenHash && reset.expires_at > DateTime.UtcNow);

            if (passwordReset == null)
            {
                ViewBag.Error = "This reset link is invalid or has expired. Please request a new one.";
                return View();
            }

            var user = await _context.Users.FindAsync(passwordReset.user_id);
            if (user == null)
            {
                _context.PasswordResets.Remove(passwordReset);
                await _context.SaveChangesAsync();
                ViewBag.Error = "This reset link is invalid. Please request a new one.";
                return View();
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(password);
            _context.PasswordResets.Remove(passwordReset);
            await _context.SaveChangesAsync();

            ViewBag.Token = null;
            ViewBag.Success = "Your password has been updated. You can now login.";
            return View();
        }

        private Task<bool> HasValidResetToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Task.FromResult(false);
            }

            string tokenHash = HashToken(token);
            return _context.PasswordResets
                .AnyAsync(reset => reset.token_hash == tokenHash && reset.expires_at > DateTime.UtcNow);
        }

        // Helper method to verify the CAPTCHA with Google
        private async Task<bool> VerifyCaptcha(string captchaResponse)
        {
            if (string.IsNullOrEmpty(captchaResponse)) return false;

            string secretKey = "6LeIxAcTAAAAAGG-vFI1TnRWxMZNFuojJ4WifJWe";
            string apiUrl = $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={captchaResponse}";

            using (var client = new HttpClient())
            {
                var response = await client.PostAsync(apiUrl, null);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    using (JsonDocument document = JsonDocument.Parse(jsonString))
                    {
                        return document.RootElement.GetProperty("success").GetBoolean();
                    }
                }
            }
            return false;
        }
    }
}
