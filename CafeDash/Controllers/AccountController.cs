using BCrypt.Net;
using CafeDash.Models;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace CafeDash.Controllers
{
    public class AccountController : Controller
    {
        private readonly string _connectionString;

        public AccountController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
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

        // 2. Handles the Login Submission
        [HttpPost]
        public IActionResult Login(string User_name, string password)
        {
            if (string.IsNullOrEmpty(User_name) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please enter both username and password.";
                return View();
            }

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                // Check against Username OR Email, just like the PHP version
                var cmd = new MySqlCommand("SELECT User_ID, User_name, Password, Suspend FROM User WHERE User_name = @user OR Email = @user LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@user", User_name);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        // Check for suspension
                        if (Convert.ToInt32(reader["Suspend"]) == 1)
                        {
                            ViewBag.Error = "Your account has been suspended. Please contact the administrator.";
                            return View();
                        }

                        string dbPassword = reader["Password"].ToString()!;
                        bool isValidPassword = false;

                        // Check legacy plain text OR the BCrypt hash
                        if (password == dbPassword)
                        {
                            isValidPassword = true;
                        }
                        else
                        {
                            try { isValidPassword = BCrypt.Net.BCrypt.Verify(password, dbPassword); }
                            catch { isValidPassword = false; }
                        }

                        if (isValidPassword)
                        {
                            // Login successful! Set the user sessions.
                            HttpContext.Session.SetInt32("user_id", Convert.ToInt32(reader["User_ID"]));
                            HttpContext.Session.SetString("User_name", reader["User_name"].ToString()!);

                            // Send them to the Customer Homepage
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
                }
            }
            return View();
        }

        // 3. Handles Logging Out
        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // Clears all session data
            return RedirectToAction("Login", "Account"); // Sends back to login page
        }

        // 4. Shows the Registration Page
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // 5. Handles Registration Form Submission
        [HttpPost]
        public IActionResult Register(string user_name, string email, string phone, string address, string password, string confirm_password)
        {
            // Mirroring your PHP validation exactly
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

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                // Check if user already exists
                var checkCmd = new MySqlCommand("SELECT User_ID FROM User WHERE User_Name = @u OR Email = @e LIMIT 1", conn);
                checkCmd.Parameters.AddWithValue("@u", user_name);
                checkCmd.Parameters.AddWithValue("@e", email);

                using (var reader = checkCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        ViewBag.Error = "Username or email already exists. Please use a different one.";
                        return View();
                    }
                }

                // Insert the new user with BCrypt hashing
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
                var insertCmd = new MySqlCommand("INSERT INTO User (User_Name, Password, Address, Contain_number, Email, Suspend) VALUES (@u, @p, @a, @c, @e, 0)", conn);
                insertCmd.Parameters.AddWithValue("@u", user_name);
                insertCmd.Parameters.AddWithValue("@p", hashedPassword);
                insertCmd.Parameters.AddWithValue("@a", address);
                insertCmd.Parameters.AddWithValue("@c", phone);
                insertCmd.Parameters.AddWithValue("@e", email);

                if (insertCmd.ExecuteNonQuery() > 0)
                {
                    ViewBag.Success = "Registration successful. You can now login with your account.";
                }
                else
                {
                    ViewBag.Error = "System error: unable to complete registration right now.";
                }
            }

            return View();
        }

        // Helper to ensure the reset table exists (matches your PHP ensure_reset_table)
        private void EnsureResetTableExists(MySqlConnection conn)
        {
            var cmd = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS password_resets (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    user_id INT NOT NULL,
                    token_hash VARCHAR(64) NOT NULL,
                    expires_at DATETIME NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )", conn);
            cmd.ExecuteNonQuery();
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
        public IActionResult ForgotPassword(string email_or_username)
        {
            if (string.IsNullOrWhiteSpace(email_or_username))
            {
                ViewBag.Error = "Please enter your registered email or username.";
                return View();
            }

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                EnsureResetTableExists(conn);

                // Find the user
                var cmd = new MySqlCommand("SELECT User_ID, Email FROM User WHERE User_Name = @input OR Email = @input LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@input", email_or_username.Trim());

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int userId = Convert.ToInt32(reader["User_ID"]);
                        string email = reader["Email"].ToString()!;
                        reader.Close(); // Close reader before doing new DB commands

                        // Generate a secure 32-byte token
                        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();
                        string tokenHash = HashToken(token);
                        DateTime expiresAt = DateTime.Now.AddHours(1);

                        // Delete old tokens for this user
                        var delCmd = new MySqlCommand("DELETE FROM password_resets WHERE user_id = @uid", conn);
                        delCmd.Parameters.AddWithValue("@uid", userId);
                        delCmd.ExecuteNonQuery();

                        // Insert new token
                        var insertCmd = new MySqlCommand("INSERT INTO password_resets (user_id, token_hash, expires_at) VALUES (@uid, @hash, @exp)", conn);
                        insertCmd.Parameters.AddWithValue("@uid", userId);
                        insertCmd.Parameters.AddWithValue("@hash", tokenHash);
                        insertCmd.Parameters.AddWithValue("@exp", expiresAt);
                        insertCmd.ExecuteNonQuery();

                        // Create the reset link
                        string resetLink = Url.Action("ResetPassword", "Account", new { token = token }, Request.Scheme)!;

                        // Send the Email (Update these SMTP details later!)
                        // Send the Email using your real appsettings.json configuration!
                        try
                        {
                            var _config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();

                            using (var smtp = new SmtpClient(_config["Smtp:Host"], int.Parse(_config["Smtp:Port"]!)))
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
                                mailMessage.To.Add(email);

                                smtp.Send(mailMessage); // It is now uncommented and will actually send!
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Email failed to send: " + ex.Message);
                        }
                    }
                }
            }

            ViewBag.Success = "If the account exists, a reset link has been sent to the registered email.";
            return View();
        }

        // ==========================================
        // RESET PASSWORD
        // ==========================================
        [HttpGet]
        public IActionResult ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                ViewBag.Error = "Reset token is missing or invalid.";
            }
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public IActionResult ResetPassword(string token, string password, string confirm_password)
        {
            ViewBag.Token = token;

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

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string tokenHash = HashToken(token);

                var checkCmd = new MySqlCommand("SELECT user_id, expires_at FROM password_resets WHERE token_hash = @hash LIMIT 1", conn);
                checkCmd.Parameters.AddWithValue("@hash", tokenHash);

                int? userId = null;
                using (var reader = checkCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        DateTime expiresAt = Convert.ToDateTime(reader["expires_at"]);
                        if (expiresAt >= DateTime.Now)
                        {
                            userId = Convert.ToInt32(reader["user_id"]);
                        }
                    }
                }

                if (userId == null)
                {
                    // Clean up expired token
                    var delExpCmd = new MySqlCommand("DELETE FROM password_resets WHERE token_hash = @hash", conn);
                    delExpCmd.Parameters.AddWithValue("@hash", tokenHash);
                    delExpCmd.ExecuteNonQuery();

                    ViewBag.Error = "Reset token is invalid or has expired.";
                    return View();
                }

                // Update the password
                string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
                var updateCmd = new MySqlCommand("UPDATE User SET Password = @pwd WHERE User_ID = @uid", conn);
                updateCmd.Parameters.AddWithValue("@pwd", newHashedPassword);
                updateCmd.Parameters.AddWithValue("@uid", userId);
                updateCmd.ExecuteNonQuery();

                // Delete the used token
                var delCmd = new MySqlCommand("DELETE FROM password_resets WHERE user_id = @uid", conn);
                delCmd.Parameters.AddWithValue("@uid", userId);
                delCmd.ExecuteNonQuery();

                ViewBag.Success = "Your password has been updated. You can now login.";
            }

            return View();
        }
    }
}