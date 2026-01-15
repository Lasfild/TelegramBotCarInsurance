using TelegramBotCarInsurance.Core.Models;

namespace TelegramBotCarInsurance.Core.Interfaces
{
    /// <summary>
    /// Repository abstraction for managing user sessions.
    /// Responsible for storing and retrieving the current state
    /// of a user's interaction with the Telegram bot.
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Retrieves an existing user session by chat ID,
        /// or creates a new one if it does not exist.
        /// </summary>
        /// <param name="chatId">
        /// Unique Telegram chat identifier used as a session key.
        /// </param>
        /// <returns>
        /// Existing or newly created <see cref="UserSession"/> instance.
        /// </returns>
        UserSession GetOrCreate(long chatId);

        /// <summary>
        /// Persists changes to the user session.
        /// </summary>
        /// <param name="session">
        /// Updated user session containing the latest state and data.
        /// </param>
        void Update(UserSession session);
    }
}
