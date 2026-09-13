using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CafeDash.Controllers
{
    public class CartController : Controller
    {
        // Handle GET requests (e.g. ?action=get)
        [HttpGet]
        public IActionResult Api(string action)
        {
            return GetCartItems();
        }

        // Handle POST requests (e.g. adding items)
        [HttpPost]
        public IActionResult Api([FromForm] string action, [FromForm] CartItemDto model)
        {
            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0)
            {
                return Json(new { success = false, message = "Please log in first." });
            }

            var cartJson = HttpContext.Session.GetString("CartItems");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new List<CartItemDto>()
                : JsonSerializer.Deserialize<List<CartItemDto>>(cartJson) ?? new List<CartItemDto>();

            if (action == "add")
            {
                cart.Add(model);
                HttpContext.Session.SetString("CartItems", JsonSerializer.Serialize(cart));
                return Json(new { success = true, items = cart });
            }

            return GetCartItems();
        }

        private IActionResult GetCartItems()
        {
            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0)
            {
                return Json(new { success = false, message = "Please log in first." });
            }

            var cartJson = HttpContext.Session.GetString("CartItems");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new List<CartItemDto>()
                : JsonSerializer.Deserialize<List<CartItemDto>>(cartJson) ?? new List<CartItemDto>();

            return Json(new { success = true, items = cart });
        }
    }

    public class CartItemDto
    {
        public int food_id { get; set; }
        public int restaurant_id { get; set; }
        public string? restaurant_name { get; set; }
        public string? item_name { get; set; }
        public string? item_type { get; set; }
        public int quantity { get; set; }
        public decimal unit_amount { get; set; }
        public string? sugar { get; set; }
        public string? ice { get; set; }
        public string? remark { get; set; }
    }
}