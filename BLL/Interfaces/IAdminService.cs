using Core.DTOs.Admin;
using Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IAdminService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync();
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<bool> ChangeUserRoleAsync(Guid userId, UserRole newRole);
    }
}
