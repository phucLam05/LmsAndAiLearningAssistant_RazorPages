using BLL.Interfaces;
using Core.DTOs.Admin;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class ChunkingConfigService : IChunkingConfigService
    {
        private readonly string _filePath;

        public ChunkingConfigService()
        {
            _filePath = Path.Combine(Directory.GetCurrentDirectory(), "chunking_settings.json");
        }

        public async Task<ChunkingConfigDto> GetConfigAsync()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = await File.ReadAllTextAsync(_filePath);
                    var config = JsonSerializer.Deserialize<ChunkingConfigDto>(json);
                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch
            {
                // Fallback to defaults on any reading/parsing issues
            }

            return new ChunkingConfigDto
            {
                Method = "Paragraph",
                ChunkSize = 500,
                OverlapSize = 50
            };
        }

        public async Task SaveConfigAsync(ChunkingConfigDto config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(config, options);
                await File.WriteAllTextAsync(_filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not save chunking settings: {ex.Message}", ex);
            }
        }
    }
}
