using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient; // Swapped to Microsoft SQL Server!
using Microsoft.AspNetCore.Http;
using CafeDash.Models;
using System;
using System.Collections.Generic;

namespace CafeDash.Controllers;

public class AdminController : Controller
{
    private readonly string _connectionString;

    public AdminController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
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
            // Changed from [User] to Users
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

        // Grab the logged-in admin's ID from the session
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
    [HttpGet] public IActionResult Login() { return View(); }

    [HttpGet]
    public IActionResult Restaurants(int? edit_id)
    {
        ViewBag.Title = "Manage Restaurants - Admin";
        var viewModel = new RestaurantViewModel();

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();

            if (edit_id.HasValue && edit_id.Value > 0)
            {
                // Changed Restaurant -> Restaurants
                var cmdRes = new SqlCommand("SELECT * FROM Restaurants WHERE Restaurant_ID = @id", conn);
                cmdRes.Parameters.AddWithValue("@id", edit_id.Value);
                using (var reader = cmdRes.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        viewModel.SelectedRestaurant = new Restaurant
                        {
                            Restaurant_ID = Convert.ToInt32(reader["Restaurant_ID"]),
                            Name = reader["Name"].ToString()!,
                            Restaurant_type = reader["Restaurant_type"].ToString()!,
                            Email = reader["Email"].ToString()!,
                            Contain_number = reader["Contain_number"].ToString()!,
                            Address = reader["Address"].ToString()!
                        };
                    }
                }

                // Changed Food -> Foods
                var cmdFood = new SqlCommand("SELECT * FROM Foods WHERE Restaurant_ID = @id", conn);
                cmdFood.Parameters.AddWithValue("@id", edit_id.Value);
                using (var reader = cmdFood.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        viewModel.SelectedRestaurantFoods.Add(new Food
                        {
                            Food_ID = Convert.ToInt32(reader["Food_ID"]),
                            Name = reader["Name"].ToString()!,
                            Food_type = reader["Food_type"].ToString()!,
                            Detail = reader["detail"].ToString()!,
                            Amount = reader["amount"] != DBNull.Value ? Convert.ToDecimal(reader["amount"]) : null
                        });
                    }
                }
            }
            else
            {
                // Changed Restaurant -> Restaurants
                var cmdAll = new SqlCommand("SELECT * FROM Restaurants ORDER BY Restaurant_ID DESC", conn);
                using (var reader = cmdAll.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        viewModel.AllRestaurants.Add(new Restaurant
                        {
                            Restaurant_ID = Convert.ToInt32(reader["Restaurant_ID"]),
                            Name = reader["Name"].ToString()!,
                            Restaurant_type = reader["Restaurant_type"].ToString()!,
                            Address = reader["Address"].ToString()!
                        });
                    }
                }
            }
        }
        return View(viewModel);
    }

    // =========================================================
    // 2. ADMIN LOGIN & LOGOUT 
    // =========================================================
    [HttpPost]
    public IActionResult Admin_login_btn(string admin_name, string admin_password)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            // Fixed table name to Admins (with an 's')
            var cmd = new SqlCommand("SELECT Admin_ID, Name, Password FROM Admins WHERE Name = @name", conn);
            cmd.Parameters.AddWithValue("@name", admin_name);

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    if (admin_password == reader["Password"].ToString())
                    {
                        HttpContext.Session.SetInt32("admin_id", Convert.ToInt32(reader["Admin_ID"]));
                        HttpContext.Session.SetString("admin_name", reader["Name"].ToString()!);
                        return RedirectToAction("Index");
                    }
                    else
                    {
                        TempData["error_message"] = "Incorrect Admin Password!";
                    }
                }
                else
                {
                    TempData["error_message"] = "Admin account not found!";
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
    public IActionResult AddRestaurant(string res_name, string res_address, string res_type, string res_email, string res_phone)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            // Added Rating and gave it a default value of 0
            var cmd = new SqlCommand("INSERT INTO Restaurants (Name, Address, Restaurant_type, Email, Contain_number, Rating) VALUES (@name, @address, @type, @email, @phone, 0)", conn);
            cmd.Parameters.AddWithValue("@name", res_name);
            cmd.Parameters.AddWithValue("@address", res_address ?? "");
            cmd.Parameters.AddWithValue("@type", res_type ?? "");
            cmd.Parameters.AddWithValue("@email", res_email ?? "");
            cmd.Parameters.AddWithValue("@phone", res_phone ?? "");

            if (cmd.ExecuteNonQuery() > 0) TempData["msg"] = "Restaurant added successfully!";
        }
        return RedirectToAction("Restaurants");
    }

    [HttpPost]
    public IActionResult UpdateRestaurant(int restaurant_id, string res_name, string res_type, string res_email, string res_phone, string res_address)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            // Changed Restaurant -> Restaurants
            var cmd = new SqlCommand("UPDATE Restaurants SET Name = @name, Restaurant_type = @type, Email = @email, Contain_number = @phone, Address = @address WHERE Restaurant_ID = @id", conn);
            cmd.Parameters.AddWithValue("@name", res_name);
            cmd.Parameters.AddWithValue("@type", res_type);
            cmd.Parameters.AddWithValue("@email", res_email ?? "");
            cmd.Parameters.AddWithValue("@phone", res_phone ?? "");
            cmd.Parameters.AddWithValue("@address", res_address ?? "");
            cmd.Parameters.AddWithValue("@id", restaurant_id);

            if (cmd.ExecuteNonQuery() > 0) TempData["msg"] = "Restaurant updated successfully!";
        }
        return RedirectToAction("Restaurants");
    }

    [HttpPost]
    public IActionResult DeleteRestaurant(int restaurant_id)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            // Changed Restaurant -> Restaurants
            var cmd = new SqlCommand("DELETE FROM Restaurants WHERE Restaurant_ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", restaurant_id);
            cmd.ExecuteNonQuery();
            TempData["msg"] = "Restaurant removed successfully.";
        }
        return RedirectToAction("Restaurants");
    }

    // =========================================================
    // 4. SETTINGS & PASSWORD
    // =========================================================
    [HttpPost]
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
            // Changed Admin -> Admins
            var selectCmd = new SqlCommand("SELECT Password FROM Admins WHERE Admin_ID = @id", conn);
            selectCmd.Parameters.AddWithValue("@id", adminId);
            string currentDbPassword = selectCmd.ExecuteScalar()?.ToString() ?? "";

            if (current_password == currentDbPassword)
            {
                // Changed Admin -> Admins
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
        // Ensure we check the exact session key set during Admin Login
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
}