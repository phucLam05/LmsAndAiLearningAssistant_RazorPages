namespace Core.DTOs.Documents;

public class DocumentConflictComparisonDto
{
    public string NewFileName { get; set; } = string.Empty;
    public string OldFileName { get; set; } = string.Empty;
    public string Analysis { get; set; } = string.Empty;
    public bool Cached { get; set; }
}
