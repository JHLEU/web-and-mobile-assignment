using System.IO;

namespace CafeDash.Helpers
{
    public static class AvatarCsvHelper
    {
        // This sets the path to /Data/avatar_mapping.csv
        private static readonly string CsvFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "avatar_mapping.csv");

        // Translates: getAvatarFromCSV()
        public static string? GetAvatarFromCSV(int userId)
        {
            if (!File.Exists(CsvFilePath)) return null;

            var lines = File.ReadAllLines(CsvFilePath);
            foreach (var line in lines.Skip(1)) // Skip the header row
            {
                var parts = line.Split(',');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int id) && id == userId)
                {
                    return parts[1];
                }
            }
            return null;
        }

        // Translates: saveAvatarMap()
        public static void SaveAvatarToCSV(int userId, string avatarPath)
        {
            var directory = Path.GetDirectoryName(CsvFilePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory!);

            var avatars = new Dictionary<int, string>();

            // Load existing data
            if (File.Exists(CsvFilePath))
            {
                var lines = File.ReadAllLines(CsvFilePath);
                foreach (var line in lines.Skip(1))
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int id))
                    {
                        avatars[id] = parts[1];
                    }
                }
            }

            // Add or update the user's avatar
            avatars[userId] = avatarPath;

            // Save back to CSV
            using (var writer = new StreamWriter(CsvFilePath))
            {
                writer.WriteLine("User_ID,Avatar_Path"); // Write Header
                foreach (var kvp in avatars)
                {
                    writer.WriteLine($"{kvp.Key},{kvp.Value}");
                }
            }
        }
    }
}