using System.Threading.Tasks;

namespace BLL.Interfaces
{
    /// <summary>
    /// Service to send email notifications.
    /// </summary>
    public interface IEmailService
    {
        Task SendFirstTimeLoginEmailAsync(string email, string fullName, string userCode, string temporaryPassword);
    }
}
