using System.Threading;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    /// <summary>
    /// Provides access to the Gemini API for text generation and chat features.
    /// </summary>
    public interface IGeminiChatProvider
    {
        /// <summary>
        /// Generates text completion using the Gemini API based on a prompt and model.
        /// </summary>
        /// <param name="prompt">The prompt content.</param>
        /// <param name="model">The Gemini model name to use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A tuple containing the generated answer, prompt tokens used, and completion tokens used.</returns>
        Task<(string Answer, int PromptTokens, int CompletionTokens)> GenerateTextAsync(
            string prompt, string model, CancellationToken cancellationToken = default);
    }
}
