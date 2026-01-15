using TelegramBotCarInsurance.Core.Enums;

namespace TelegramBotCarInsurance.Core.Models
{
    /// <summary>
    /// Represents a single user's session within the Telegram bot.
    /// Stores the current workflow state and all data extracted
    /// from submitted documents.
    /// 
    /// This class acts as the data container for the bot's state machine.
    /// </summary>
    public class UserSession
    {
        /// <summary>
        /// Telegram chat identifier.
        /// Used as a unique key to track the user's session.
        /// </summary>
        public long ChatId { get; set; }

        /// <summary>
        /// Current state of the user within the bot workflow.
        /// Controls which step the bot should process next.
        /// </summary>
        public UserState State { get; set; } = UserState.Start;

        // =========================
        // Passport fields (Mindee passport model)
        // =========================

        /// <summary>
        /// Given name(s) extracted from the passport document.
        /// </summary>
        public string? GivenNames { get; set; }

        /// <summary>
        /// Surname(s) extracted from the passport document.
        /// </summary>
        public string? Surnames { get; set; }

        /// <summary>
        /// Passport or identity document number.
        /// </summary>
        public string? DocumentNumber { get; set; }

        // =========================
        // Vehicle registration fields (Mindee vehicle document model)
        // =========================

        /// <summary>
        /// Vehicle model or description extracted from the vehicle document.
        /// </summary>
        public string? CarModel { get; set; }

        /// <summary>
        /// Vehicle license plate number.
        /// </summary>
        public string? LicensePlate { get; set; }
    }
}
