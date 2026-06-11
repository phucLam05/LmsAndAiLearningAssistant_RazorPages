namespace Core.Configurations
{
    /// <summary>
    /// Holds Supabase Storage configuration loaded from appsettings or environment variables.
    /// </summary>
    public class SupabaseOptions
    {
        public string Url { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public string ServiceRoleKey { get; set; } = string.Empty;

        public string Bucket { get; set; } = "documents";
    }
}
