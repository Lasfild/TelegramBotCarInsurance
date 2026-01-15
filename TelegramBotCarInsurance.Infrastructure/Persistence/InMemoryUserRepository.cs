using System.Collections.Concurrent;
using TelegramBotCarInsurance.Core.Interfaces;
using TelegramBotCarInsurance.Core.Models;

namespace TelegramBotCarInsurance.Infrastructure.Persistence
{
    /// <summary>
    /// In-memory implementation of <see cref="IUserRepository"/>.
    /// 
    /// Stores user sessions in a thread-safe dictionary using Telegram chat ID
    /// as the key. This implementation is suitable for demos and test tasks,
    /// but not intended for production use.
    /// </summary>
    public class InMemoryUserRepository : IUserRepository
    {
        /// <summary>
        /// Thread-safe storage for user sessions.
        /// Key: Telegram chat ID
        /// Value: UserSession instance
        /// </summary>
        private readonly ConcurrentDictionary<long, UserSession> _store = new();

        /// <summary>
        /// Retrieves an existing user session for the given chat ID,
        /// or creates a new one if it does not exist.
        /// </summary>
        /// <param name="chatId">
        /// Unique Telegram chat identifier.
        /// </param>
        /// <returns>
        /// Existing or newly created <see cref="UserSession"/> instance.
        /// </returns>
        public UserSession GetOrCreate(long chatId)
        {
            // Atomically get or create a new session
            return _store.GetOrAdd(chatId, _ => new UserSession { ChatId = chatId });
        }

        /// <summary>
        /// Updates (or inserts) the given user session in the storage.
        /// </summary>
        /// <param name="session">
        /// User session containing the latest state and extracted data.
        /// </param>
        public void Update(UserSession session)
        {
            // Overwrite existing session for the given chat ID
            _store[session.ChatId] = session;
        }
    }
}
