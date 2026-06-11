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

        Task<Result<int>> ImportStudentsFromExcelAsync(Stream excelStream);
    }
}
