using BLL.Interfaces;
using Core.DTOs.Admin;
using Core.Entities;
using DAL.Interfaces;
using System;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class AdminService : IAdminService
    {
        private readonly IUserRepository _userRepository;
        private readonly IDocumentRepository _documentRepository;
        private readonly IDocumentChunkRepository _documentChunkRepository;

        public AdminService(
            IUserRepository userRepository,
            IDocumentRepository documentRepository,
            IDocumentChunkRepository documentChunkRepository)
        {
            _userRepository = userRepository;
            _documentRepository = documentRepository;
            _documentChunkRepository = documentChunkRepository;
        }

        public async Task<bool> ChangeUserRoleAsync(Guid userId, UserRole newRole)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            user.Role = newRole;
            await _userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<System.Collections.Generic.IEnumerable<User>> GetAllUsersAsync()
        {
            return await _userRepository.GetAllUsersAsync();
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            var allUsers = await _userRepository.GetAllUsersAsync();
            var allDocuments = await _documentRepository.GetAllWithDetailsAsync();
            var totalUsers = allUsers.Count;
            var totalDocuments = allDocuments.Count;
            var totalStorage = 0L;

            var totalChunks = await _documentChunkRepository.CountAllAsync();

            return new DashboardStatsDto
            {
                TotalUsers = totalUsers,
                TotalDocuments = totalDocuments,
                TotalStorageUsedBytes = totalStorage,
                TotalDocumentChunks = totalChunks
            };
        }
    }
}
