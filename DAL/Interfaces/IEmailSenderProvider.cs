using System.Threading.Tasks;

namespace DAL.Interfaces
{
    /// <summary>
    /// Provides service for sending physical emails or handling fallback logging.
    /// </summary>
    public interface IEmailSenderProvider
    {
        /// <summary>
        /// Sends an email to the specified recipient.
        /// </summary>
        /// <param name="to">The recipient's email address.</param>
        /// <param name="subject">The email subject.</param>
        /// <param name="body">The email body.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        Task SendEmailAsync(string to, string subject, string body);
    }
}
