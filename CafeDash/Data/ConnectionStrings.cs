namespace CafeDash.Data
{
    public static class ConnectionStrings
    {
        public const string LocalDbDefault =
            "Server=(localdb)\\mssqllocaldb;Database=cafedash_db;Trusted_Connection=True;TrustServerCertificate=True;";

        /// <summary>
        /// Resolves the configured connection string, falling back to LocalDB so the
        /// app still starts when appsettings.json is missing (e.g. a fresh clone).
        /// Without this, GetConnectionString returning null reaches SqlConnection.
        /// </summary>
        public static string Resolve(IConfiguration configuration)
        {
            var configured = configuration.GetConnectionString("DefaultConnection");
            return string.IsNullOrWhiteSpace(configured) ? LocalDbDefault : configured;
        }
    }
}
