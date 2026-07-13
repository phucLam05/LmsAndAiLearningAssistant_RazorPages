using System.Threading.Tasks;
using Core.DTOs.Admin;

namespace BLL.Interfaces
{
    public interface IChunkingConfigService
    {
        Task<ChunkingConfigDto> GetConfigAsync();
        Task SaveConfigAsync(ChunkingConfigDto config);
    }
}
