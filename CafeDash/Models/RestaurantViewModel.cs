namespace CafeDash.Models
{
    public class Restaurant
    {
        public int Restaurant_ID { get; set; }
        public string? Name { get; set; }
        public string? Restaurant_type { get; set; }
        public string? Email { get; set; }
        public string? Contain_number { get; set; }
        public string? Address { get; set; }

        // Added for Customer Homepage
        public decimal Rating { get; set; }
    }

    public class Food
    {
        public int Food_ID { get; set; }
        public string? Name { get; set; }
        public string? Food_type { get; set; }
        public string? Detail { get; set; }
        public decimal? Amount { get; set; }
    }

    public class Feedback
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Message { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class User
    {
        public int User_ID { get; set; }
        public string? User_name { get; set; }
        public string? Email { get; set; }
        public string? Contain_number { get; set; } // Add this
        public string? Address { get; set; } // Add this
        public int Suspend { get; set; }
    }

    public class RestaurantViewModel
    {
        public List<Restaurant> AllRestaurants { get; set; } = new List<Restaurant>();
        public Restaurant? SelectedRestaurant { get; set; }
        public List<Food> SelectedRestaurantFoods { get; set; } = new List<Food>();
    }

    public class CustomerHomeViewModel
    {
        public List<Restaurant> TopRestaurants { get; set; } = new List<Restaurant>();
        public List<Restaurant> AllRestaurants { get; set; } = new List<Restaurant>();
    }

    public class MenuItem
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Detail { get; set; }
        public string? Type { get; set; }
        public decimal Amount { get; set; }
        public bool IsDrink { get; set; }
        public string? Image { get; set; }
    }

    public class CafeViewModel
    {
        public Restaurant? Restaurant { get; set; }
        public Dictionary<string, List<MenuItem>> GroupedMenuItems { get; set; } = new();
    }

    public class BillItem
    {
        public string? Item_name { get; set; }
        public int Quantity { get; set; }
        public decimal Unit_amount { get; set; }
        public decimal Line_total { get; set; }
        public string? Sugar_level { get; set; }
        public string? Ice_level { get; set; }
        public string? Remark { get; set; }
    }

    public class Bill
    {
        public int Payment_ID { get; set; }
        public decimal Payment_amount { get; set; }
        public decimal Subtotal_amount { get; set; }
        public decimal SST_amount { get; set; }
        public string? Payment_status { get; set; }
        public DateTime Created_at { get; set; }
        public DateTime Paid_at { get; set; }
        public string? Display_restaurant_name { get; set; }
        public List<BillItem> Items { get; set; } = new List<BillItem>();
    }
}