using Core.DTOs.Admin;
using Core.DTOs.Common;
using Core.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    /// <summary>
    /// Service interface for Admin-focused User CRUD operations and bulk Excel import.
    /// </summary>
    public interface IUserService
    {
        Task<Result<User>> CreateUserAsync(UserCreateDto dto);

        Task<Result> UpdateUserAsync(UserEditDto dto);

        Task<Result> DeleteUserAsync(Guid id);

        Task<Result<string>> ResetPasswordAsync(Guid id);

        Task<Result<int>> ImportStudentsFromExcelAsync(Stream excelStream);

        /// <summary>Returns the UserCode for a given user ID, used by the profile page.</summary>
        Task<Result<string>> GetUserCodeAsync(Guid userId);

        /// <summary>Verifies the current password and replaces it with a new one (logged-in user self-service).</summary>
        Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);

        /// <summary>Activates an Inactive account and replaces the temporary password (first-time login flow).</summary>
        Task<Result> ActivateAndChangePasswordAsync(Guid userId, string? currentPassword, string newPassword);

        /// <summary>Returns all active users with the Lecturer role, used for assignment dropdowns.</summary>
        Task<IReadOnlyList<User>> GetLecturersAsync();
    }
}
