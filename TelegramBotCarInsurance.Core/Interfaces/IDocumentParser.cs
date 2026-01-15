using TelegramBotCarInsurance.Core.Models;

namespace TelegramBotCarInsurance.Core.Interfaces
{
    /// <summary>
    /// Defines a contract for document parsing services.
    /// Implementations are responsible for extracting
    /// structured data from document images (e.g., passport,
    /// vehicle registration document).
    /// 
    /// Typically backed by OCR / document understanding APIs
    /// such as Mindee.
    /// </summary>
    public interface IDocumentParser
    {
        /// <summary>
        /// Extracts relevant data from a document image.
        /// </summary>
        /// <param name="imageStream">
        /// Stream containing the image data of the document
        /// (usually downloaded from Telegram servers).
        /// </param>
        /// <returns>
        /// A <see cref="UserSession"/> instance populated
        /// with the extracted fields relevant for the current document type.
        /// Only the fields related to the parsed document are expected to be filled.
        /// </returns>
        Task<UserSession> ExtractDataAsync(Stream imageStream);
    }
}
