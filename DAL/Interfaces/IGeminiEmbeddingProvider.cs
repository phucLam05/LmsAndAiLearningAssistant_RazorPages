using System.Threading;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    /// <summary>
    /// Provides access to the Gemini API for generating text embeddings.
    /// </summary>
    public interface IGeminiEmbeddingProvider
    {
        /// <summary>
        /// Generates an embedding vector for the provided text.
        /// </summary>
        /// <param name="text">The text content to embed.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A float array representing the embedding vector.</returns>
        Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    }
}
