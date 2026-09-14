using Microsoft.Data.SqlClient;
using System.Text;

namespace CafeDash.Data
{
    /// <summary>
    /// Brings a freshly created database up to a usable state: applies structural
    /// fixes that the EF migration does not cover, then loads demo data if empty.
    /// </summary>
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(string? connectionString, string contentRootPath)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("[WARN] No database connection string configured; skipping seeding.");
                return;
            }

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            // Structural fixes are idempotent, so they run on every startup.
            await RunScriptAsync(conn, Path.Combine(contentRootPath, "Data", "seed_schema.sql"));

            if (await HasDataAsync(conn))
            {
                return;
            }

            await RunScriptAsync(conn, Path.Combine(contentRootPath, "Data", "seed_data.sql"));
            Console.WriteLine("[INFO] Database was empty - demo data seeded.");
        }

        private static async Task<bool> HasDataAsync(SqlConnection conn)
        {
            await using var cmd = new SqlCommand("SELECT COUNT(*) FROM Restaurants", conn);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        private static async Task RunScriptAsync(SqlConnection conn, string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"[WARN] SQL seed script not found: {path}");
                return;
            }

            var script = await File.ReadAllTextAsync(path);

            foreach (var batch in SplitOnGo(script))
            {
                await using var cmd = new SqlCommand(batch, conn);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>sqlcmd convention: a line containing only GO ends a batch.</summary>
        private static IEnumerable<string> SplitOnGo(string script)
        {
            var batch = new StringBuilder();

            foreach (var line in script.Split('\n'))
            {
                if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
                {
                    if (batch.ToString().Trim().Length > 0)
                    {
                        yield return batch.ToString();
                    }
                    batch.Clear();
                }
                else
                {
                    batch.AppendLine(line);
                }
            }

            if (batch.ToString().Trim().Length > 0)
            {
                yield return batch.ToString();
            }
        }
    }
}
