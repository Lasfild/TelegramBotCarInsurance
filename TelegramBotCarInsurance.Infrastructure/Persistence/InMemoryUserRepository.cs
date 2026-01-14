using System.Collections.Concurrent;
using TelegramBotCarInsurance.Core.Interfaces;
using TelegramBotCarInsurance.Core.Models;

namespace TelegramBotCarInsurance.Infrastructure.Persistence
{
    public class InMemoryUserRepository : IUserRepository
    {
        private readonly ConcurrentDictionary<long, UserSession> _store = new();

        public UserSession GetOrCreate(long chatId)
        {
            return _store.GetOrAdd(chatId, _ => new UserSession { ChatId = chatId });
        }

        public void Update(UserSession session)
        {
            _store[session.ChatId] = session;
        }
    }
}
