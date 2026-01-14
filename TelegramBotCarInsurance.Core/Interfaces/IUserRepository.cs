using TelegramBotCarInsurance.Core.Models;

namespace TelegramBotCarInsurance.Core.Interfaces
{
    public interface IUserRepository
    {
        UserSession GetOrCreate(long chatId);
        void Update(UserSession session);
    }
}
