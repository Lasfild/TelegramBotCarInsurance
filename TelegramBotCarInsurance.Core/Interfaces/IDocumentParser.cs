using TelegramBotCarInsurance.Core.Models;

namespace TelegramBotCarInsurance.Core.Interfaces
{
    public interface IDocumentParser
    {
        Task<UserSession> ExtractDataAsync(Stream imageStream);
    }
}
