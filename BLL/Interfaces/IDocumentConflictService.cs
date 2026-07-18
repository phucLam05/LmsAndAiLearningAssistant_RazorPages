using Core.DTOs.Common;
using Core.DTOs.Documents;

namespace BLL.Interfaces;

public interface IDocumentConflictService
{
    Task<Result<DocumentConflictComparisonDto>> CompareAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}
