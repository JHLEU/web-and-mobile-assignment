using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient; // Swapped to Microsoft SQL Server!
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using CafeDash.Data;
using CafeDash.Models;
using System;
using System.Collections.Generic;

namespace CafeDash.Controllers;

public class AdminController : Controller
{
    private readonly string _connectionString;
    private readonly IMemoryCache _cache;
    private const int MaxFailedAttempts = 3;
    private const int BlockDurationMinutes = 2;

    public AdminController(IConfiguration configuration, IMemoryCache cache)
    {
        _connectionString = ConnectionStrings.Resolve(configuration);
        _cache = cache;
    }

    private (bool isBlocked, int remainingSeconds) CheckLoginBlocked(string key)
    {
        if (_cache.TryGetValue($"admin_login_blocked_{key}", out DateTime blockUntil))
        {
            if (blockUntil > DateTime.UtcNow)
            {
                int remaining = (int)Math.Ceiling((blockUntil - DateTime.UtcNow).TotalSeconds);
                return (true, remaining);
            }
            _cache.Remove($"admin_login_blocked_{key}");
            _cache.Remove($"admin_login_attempts_{key}");
        }
        return (false, 0);
    }

    private void IncrementFailedAttempts(string key)
    {
        int attempts = _cache.GetOrCreate($"admin_login_attempts_{key}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(BlockDurationMinutes);
            return 0;
        }) + 1;

        _cache.Set($"admin_login_attempts_{key}", attempts, TimeSpan.FromMinutes(BlockDurationMinutes));

        if (attempts >= MaxFailedAttempts)
        {
            _cache.Set($"admin_login_blocked_{key}", DateTime.UtcNow.AddMinutes(BlockDurationMinutes),
                TimeSpan.FromMinutes(BlockDurationMinutes));
        }
    }

    private void ResetLoginAttempts(string key)
    {
        _cache.Remove($"admin_login_attempts_{key}");
        _cache.Remove($"admin_login_blocked_{key}");
    }

    // =========================================================
    // 1. PAGE VIEWS (GET METHODS)
    // =========================================================
    [HttpGet]
    public IActionResult Ranking()
    {
        // 🔒 Secure the actual page view so it kicks you out if you aren't logged in!
        int? adminId = HttpContext.Session.GetInt32("admin_id");
        if (adminId == null || adminId == 0)
        {
            return RedirectToAction("Login", "Admin");
        }

        ViewBag.Title = "Restaurant Performance Ranking";
        return View();
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.Title = "Admin Dashboard";
        var feedbackList = new List<Feedback>();

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("SELECT * FROM ContactUs ORDER BY Created_at DESC", conn);
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    feedbackList.Add(new Feedback
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Name = reader["Name"].ToString(),
                        Email = reader["Email"].ToString(),
                        Phone = reader["Phone"].ToString(),
                        Message = reader["Message"].ToString(),
                        CreatedAt = Convert.ToDateTime(reader["Created_at"])
                    });
                }
            }
        }
        return View(feedbackList);
    }

    [HttpGet]
    public IActionResult Members(string search)
    {
        ViewBag.Title = "Member Management";
        ViewBag.SearchTerm = search;
        var userList = new List<User>();

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            string query = "SELECT * FROM Users";
            if (!string.IsNullOrEmpty(search))
                query += " WHERE User_name LIKE @search OR Email LIKE @search";

            var cmd = new SqlCommand(query, conn);
            if (!string.IsNullOrEmpty(search))
                cmd.Parameters.AddWithValue("@search", "%" + search + "%");

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    userList.Add(new User
                    {
                        User_ID = Convert.ToInt32(reader["User_ID"]),
                        User_name = reader["User_name"].ToString(),
                        Email = reader["Email"].ToString(),
                        Suspend = Convert.ToInt32(reader["Suspend"])
                    });
                }
            }
        }
        return View(userList);
    }

    [HttpPost]
    public IActionResult ToggleSuspend(int? user_id)
    {
        if (user_id == null || user_id <= 0)
        {
            return RedirectToAction("Members");
        }

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("UPDATE Users SET Suspend = CASE WHEN Suspend = 1 THEN 0 ELSE 1 END WHERE User_ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", user_id.Value);
            cmd.ExecuteNonQuery();
            TempData["msg"] = "Member status updated successfully.";
        }
        return RedirectToAction("Members");
    }

    [HttpPost]
    public IActionResult DeleteUser(int? user_id)
    {
        if (user_id == null || user_id <= 0)
        {
            return RedirectToAction("Members");
        }

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("DELETE FROM Users WHERE User_ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", user_id.Value);
            cmd.ExecuteNonQuery();
            TempData["msg"] = "Member removed successfully.";
        }
        return RedirectToAction("Members");
    }

    [HttpGet]
    public IActionResult Settings()
    {
        ViewBag.Title = "Admin Settings";

        int? adminId = HttpContext.Session.GetInt32("admin_id");
        if (adminId == null || adminId == 0)
        {
            return RedirectToAction("Login", "Admin");
        }

        Admin admin = null!;

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("SELECT Admin_ID, Name, Password FROM Admins WHERE Admin_ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", adminId.Value);

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    admin = new Admin
                    {
                        Admin_ID = Convert.ToInt32(reader["Admin_ID"]),
                        Name = reader["Name"].ToString()!,
                        Password = reader["Password"].ToString()!
                    };
                }
            }
        }

        return View(admin);
    }

    [HttpGet]
    public IActionResult Login() { return View(); }

    // ==========================================
    // RESTAURANT & FOOD MANAGEMENT ACTIONS
    // ==========================================

    [HttpGet]
    public IActionResult Restaurants(int? edit_id)
    {
        int? adminId = HttpContext.Session.GetInt32("admin_id");
        if (adminId == null || adminId == 0) return RedirectToAction("Login", "Admin");

        if (edit_id.HasValue && edit_id.Value > 0)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM Restaurants WHERE Restaurant_ID = @id", conn);
                cmd.Parameters.AddWithValue("@id", edit_id.Value);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var restaurant = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            restaurant[reader.GetName(i)] = reader.GetValue(i);
                        }
                        ViewBag.SelectedRestaurant = restaurant;
                    }
                }
            }
        }
        else
        {
            var list = new List<Dictionary<string, object>>();
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM Restaurants", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var item = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            item[reader.GetName(i)] = reader.GetValue(i);
                        }
                        list.Add(item);
                    }
                }
            }
            ViewBag.Restaurants = list;
        }

        return View();
    }

    [HttpPost]
    public IActionResult AddRestaurant(string res_name, string res_address, string res_type, string res_email, string res_phone)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("INSERT INTO Restaurants (Name, Address, Restaurant_type, Email, Contain_number, Rating) VALUES (@name, @address, @type, @email, @phone, 0)", conn);
            cmd.Parameters.AddWithValue("@name", res_name ?? "");
            cmd.Parameters.AddWithValue("@address", res_address ?? "");
            cmd.Parameters.AddWithValue("@type", res_type ?? "");
            cmd.Parameters.AddWithValue("@email", res_email ?? "");
            cmd.Parameters.AddWithValue("@phone", res_phone ?? "");

            if (cmd.ExecuteNonQuery() > 0) TempData["msg"] = "Restaurant added successfully!";
        }
        return RedirectToAction("Restaurants");
    }

    [HttpPost]
    public async Task<IActionResult> UpdateRestaurant(int restaurant_id, string res_name, string res_type, string res_email, string res_phone, string res_address, IFormFile? res_image)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("UPDATE Restaurants SET Name = @name, Restaurant_type = @type, Email = @email, Contain_number = @phone, Address = @address WHERE Restaurant_ID = @id", conn);
            cmd.Parameters.AddWithValue("@name", res_name ?? "");
            cmd.Parameters.AddWithValue("@type", res_type ?? "");
            cmd.Parameters.AddWithValue("@email", res_email ?? "");
            cmd.Parameters.AddWithValue("@phone", res_phone ?? "");
            cmd.Parameters.AddWithValue("@address", res_address ?? "");
            cmd.Parameters.AddWithValue("@id", restaurant_id);
            cmd.ExecuteNonQuery();
        }

        if (res_image != null && res_image.Length > 0)
        {
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "material", res_name);
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            string filePath = Path.Combine(uploadsFolder, "shop.jpg");
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await res_image.CopyToAsync(stream);
            }
        }

        TempData["msg"] = "Restaurant updated successfully!";
        return RedirectToAction("Restaurants", new { edit_id = restaurant_id });
    }

    [HttpPost]
    public IActionResult DeleteRestaurant(int restaurant_id)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("DELETE FROM Restaurants WHERE Restaurant_ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", restaurant_id);
            cmd.ExecuteNonQuery();
            TempData["msg"] = "Restaurant removed successfully.";
        }
        return RedirectToAction("Restaurants");
    }

    [HttpPost]
    public async Task<IActionResult> AddFood(int restaurant_id, string food_name, decimal food_amount, string food_desc, string food_type, IFormFile? food_image)
    {
        string imageName = "default_food.jpg";
        if (food_image != null && food_image.Length > 0)
        {
            imageName = Guid.NewGuid().ToString() + Path.GetExtension(food_image.FileName);
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "material", "images");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            string filePath = Path.Combine(uploadsFolder, imageName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await food_image.CopyToAsync(stream);
            }
        }

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("INSERT INTO Foods (Restaurant_ID, Name, Food_type, detail, amount) VALUES (@rid, @name, @type, @detail, @amt)", conn);
            cmd.Parameters.AddWithValue("@rid", restaurant_id);
            cmd.Parameters.AddWithValue("@name", food_name ?? "");
            cmd.Parameters.AddWithValue("@type", food_type ?? "");
            cmd.Parameters.AddWithValue("@detail", food_desc ?? "");
            cmd.Parameters.AddWithValue("@amt", food_amount);
            cmd.ExecuteNonQuery();
        }

        TempData["msg"] = "Food item added successfully!";
        return RedirectToAction("Restaurants", new { edit_id = restaurant_id });
    }

    [HttpPost]
    public IActionResult DeleteFood(int food_id, int restaurant_id)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("DELETE FROM Foods WHERE Food_ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", food_id);
            cmd.ExecuteNonQuery();
        }

        TempData["msg"] = "Food item deleted successfully!";
        return RedirectToAction("Restaurants", new { edit_id = restaurant_id });
    }

    // =========================================================
    // 2. ADMIN LOGIN & LOGOUT 
    // =========================================================
    [HttpPost]
    public IActionResult Admin_login_btn(string admin_name, string admin_password)
    {
        string loginKey = (admin_name ?? "").Trim().ToLower();

        var (isBlocked, remainingSeconds) = CheckLoginBlocked(loginKey);
        if (isBlocked)
        {
            int mins = remainingSeconds / 60;
            int secs = remainingSeconds % 60;
            TempData["error_message"] = $"Too many failed login attempts. Please try again in {mins} minute(s) and {secs} second(s).";
            return RedirectToAction("Login");
        }

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("SELECT Admin_ID, Name, Password FROM Admins WHERE Name = @name", conn);
            cmd.Parameters.AddWithValue("@name", admin_name);

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    if (admin_password == reader["Password"].ToString())
                    {
                        ResetLoginAttempts(loginKey);
                        HttpContext.Session.SetInt32("admin_id", Convert.ToInt32(reader["Admin_ID"]));
                        HttpContext.Session.SetString("admin_name", reader["Name"].ToString()!);
                        return RedirectToAction("Index");
                    }
                    else
                    {
                        IncrementFailedAttempts(loginKey);
                        int remaining = MaxFailedAttempts - (_cache.Get<int>($"admin_login_attempts_{loginKey}"));
                        if (remaining > 0)
                            TempData["error_message"] = $"Incorrect Admin Password! {remaining} attempt(s) remaining before temporary lock.";
                        else
                            TempData["error_message"] = $"Incorrect Admin Password! Too many failed attempts. Login locked for {BlockDurationMinutes} minute(s).";
                    }
                }
                else
                {
                    IncrementFailedAttempts(loginKey);
                    int remaining = MaxFailedAttempts - (_cache.Get<int>($"admin_login_attempts_{loginKey}"));
                    if (remaining > 0)
                        TempData["error_message"] = $"Admin account not found! {remaining} attempt(s) remaining before temporary lock.";
                    else
                        TempData["error_message"] = $"Admin account not found! Too many failed attempts. Login locked for {BlockDurationMinutes} minute(s).";
                }
            }
        }
        return RedirectToAction("Login");
    }

    [HttpGet, HttpPost]
    public IActionResult Logout_btn()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login", "Admin");
    }

    // =========================================================
    // 4. SETTINGS & PASSWORD
    // =========================================================
    [HttpPost]
    public IActionResult UpdateAdminPassword(string current_password, string new_password, string confirm_password)
    {
        int? adminId = HttpContext.Session.GetInt32("admin_id");
        if (adminId == null) return RedirectToAction("Login", "Admin");

        if (new_password != confirm_password)
        {
            TempData["msg"] = "New passwords do not match!";
            return RedirectToAction("Settings");
        }

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var selectCmd = new SqlCommand("SELECT Password FROM Admins WHERE Admin_ID = @id", conn);
            selectCmd.Parameters.AddWithValue("@id", adminId);
            string currentDbPassword = selectCmd.ExecuteScalar()?.ToString() ?? "";

            if (current_password == currentDbPassword)
            {
                var updateCmd = new SqlCommand("UPDATE Admins SET Password = @newPass WHERE Admin_ID = @id", conn);
                updateCmd.Parameters.AddWithValue("@newPass", new_password);
                updateCmd.Parameters.AddWithValue("@id", adminId);
                if (updateCmd.ExecuteNonQuery() > 0) TempData["msg"] = "Password updated successfully!";
            }
            else
            {
                TempData["msg"] = "Current password is incorrect.";
            }
        }
        return RedirectToAction("Settings");
    }

    // =========================================================
    // 5. RESTAURANT RANKING JSON API
    // =========================================================
    [HttpGet]
    public IActionResult GetRanking(int limit = 10)
    {
        int? adminId = HttpContext.Session.GetInt32("admin_id");
        if (adminId == null || adminId == 0)
        {
            return Unauthorized(new { success = false, message = "Unauthorized." });
        }

        if (limit <= 0) limit = 10;
        if (limit > 100) limit = 100;

        var rows = new List<object>();

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();

            var sql = @"
            SELECT TOP (@limit)
                r.Restaurant_ID, 
                r.Name, 
                COUNT(p.Payment_ID) AS transaction_count, 
                COALESCE(SUM(p.Payment_amount), 0) AS total_sales
            FROM Restaurants r
            LEFT JOIN Payment p 
                ON p.Restaurant_ID = r.Restaurant_ID 
                AND p.Payment_status = 'SUCCEEDED'
            GROUP BY r.Restaurant_ID, r.Name
            ORDER BY transaction_count DESC, total_sales DESC, r.Name ASC";

            var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@limit", limit);

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add(new
                    {
                        restaurantId = Convert.ToInt32(reader["Restaurant_ID"]),
                        restaurantName = reader["Name"].ToString(),
                        transactionCount = Convert.ToInt32(reader["transaction_count"]),
                        totalSales = Math.Round(Convert.ToDecimal(reader["total_sales"]), 2)
                    });
                }
            }
        }

        return Json(new
        {
            success = true,
            limit = limit,
            ranking = rows
        });
    }

    // =========================================================
    // 6. BATCH DATA MANAGEMENT (Insert, Update, Delete)
    // =========================================================
    [HttpGet]
    public IActionResult BatchManager()
    {
        int? adminId = HttpContext.Session.GetInt32("admin_id");
        if (adminId == null || adminId == 0) return RedirectToAction("Login", "Admin");

        ViewBag.Title = "Batch Data Management";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ProcessBatch(IFormFile? batchFile, string batchText, string operationType)
    {
        int? adminId = HttpContext.Session.GetInt32("admin_id");
        if (adminId == null || adminId == 0) return Unauthorized(new { success = false, message = "Unauthorized." });

        var lines = new List<string>();

        if (batchFile != null && batchFile.Length > 0)
        {
            using (var reader = new StreamReader(batchFile.OpenReadStream()))
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(batchText))
        {
            using (var reader = new StringReader(batchText))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
                }
            }
        }

        if (!lines.Any())
        {
            TempData["msg"] = "Error: No data provided for batch processing.";
            return RedirectToAction("BatchManager");
        }

        int successCount = 0;

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            using (var transaction = conn.BeginTransaction())
            {
                try
                {
                    foreach (var line in lines)
                    {
                        var parts = line.Split(',');
                        if (parts.Length < 5) continue;

                        int restaurantId = int.Parse(parts[0].Trim());
                        string name = parts[1].Trim();
                        string type = parts[2].Trim();
                        string detail = parts[3].Trim();
                        decimal amount = decimal.Parse(parts[4].Trim());

                        SqlCommand cmd;

                        if (operationType == "insert")
                        {
                            cmd = new SqlCommand(@"
                                INSERT INTO Foods (Restaurant_ID, Name, Food_type, detail, amount) 
                                VALUES (@rid, @name, @type, @detail, @amt)", conn, transaction);
                        }
                        else if (operationType == "update")
                        {
                            cmd = new SqlCommand(@"
                                UPDATE Foods 
                                SET Food_type = @type, detail = @detail, amount = @amt 
                                WHERE Restaurant_ID = @rid AND Name = @name", conn, transaction);
                        }
                        else // delete
                        {
                            cmd = new SqlCommand(@"
                                DELETE FROM Foods 
                                WHERE Restaurant_ID = @rid AND Name = @name", conn, transaction);
                        }

                        cmd.Parameters.AddWithValue("@rid", restaurantId);
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@type", type);
                        cmd.Parameters.AddWithValue("@detail", detail);
                        cmd.Parameters.AddWithValue("@amt", amount);

                        successCount += cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    TempData["msg"] = $"Batch operation '{operationType}' completed successfully. Affected rows: {successCount}";
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["msg"] = "Batch processing failed: " + ex.Message;
                }
            }
        }

        return RedirectToAction("BatchManager");
    }
    [HttpGet]
    public IActionResult ChatManagement()
    {
        int? adminId = HttpContext.Session.GetInt32("admin_id");
        if (adminId == null || adminId == 0) return RedirectToAction("Login", "Admin");

        // Get list of users who have chat history
        var users = new List<Dictionary<string, object>>();
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand(@"
            SELECT DISTINCT u.User_ID, u.User_name, u.Email 
            FROM Users u 
            INNER JOIN Chats c ON u.User_ID = c.User_ID 
            ORDER BY u.User_name", conn);
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var item = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        item[reader.GetName(i)] = reader.GetValue(i);
                    }
                    users.Add(item);
                }
            }
        }
        ViewBag.ChatUsers = users;
        ViewBag.Title = "Live Chat Management";
        return View();
    }

    [HttpGet]
    public IActionResult GetAdminChatMessages(int userId)
    {
        var messages = new List<object>();
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("SELECT Sender_Type, Message_Text, Sent_at FROM Chats WHERE User_ID = @uid ORDER BY Sent_at ASC", conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    messages.Add(new
                    {
                        sender = reader["Sender_Type"].ToString(),
                        text = reader["Message_Text"].ToString(),
                        time = Convert.ToDateTime(reader["Sent_at"]).ToString("hh:mm tt")
                    });
                }
            }
        }
        return Json(new { success = true, messages });
    }

    [HttpPost]
    public IActionResult SendAdminMessage(int userId, string messageText)
    {
        int? adminId = HttpContext.Session.GetInt32("admin_id");
        if (adminId == null || string.IsNullOrWhiteSpace(messageText)) return Unauthorized(new { success = false });

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("INSERT INTO Chats (User_ID, Sender_Type, Message_Text) VALUES (@uid, 'admin', @text)", conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@text", messageText.Trim());
            cmd.ExecuteNonQuery();
        }
        return Json(new { success = true });
    }

    [HttpPost]
    public IActionResult DeleteFeedback(int? feedback_id)
    {
        if (feedback_id == null || feedback_id <= 0)
        {
            return RedirectToAction("Index");
        }

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("DELETE FROM ContactUs WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", feedback_id.Value);
            cmd.ExecuteNonQuery();
        }

        TempData["msg"] = "Feedback deleted successfully.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult LiveChat()
    {
        int? adminId = HttpContext.Session.GetInt32("admin_id");
        if (adminId == null || adminId == 0) return RedirectToAction("Login", "Admin");
        var users = new List<Dictionary<string, object>>();
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand(@"
            SELECT DISTINCT u.User_ID, u.User_name, u.Email 
            FROM Users u 
            INNER JOIN Chats c ON u.User_ID = c.User_ID 
            ORDER BY u.User_name", conn);

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var item = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        item[reader.GetName(i)] = reader.GetValue(i);
                    }
                    users.Add(item);
                }
            }
        }
        ViewBag.ChatUsers = users;
        ViewBag.Title = "Live Chat Hub";
        return View();
    }
}