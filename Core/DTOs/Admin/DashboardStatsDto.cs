namespace Core.DTOs.Admin
{
    public class DashboardStatsDto
    {
        public int TotalUsers { get; set; }
        public int TotalDocuments { get; set; }
        public long TotalStorageUsedBytes { get; set; }
        public int TotalDocumentChunks { get; set; }
    }
}
