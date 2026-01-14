using TelegramBotCarInsurance.Core.Enums;

namespace TelegramBotCarInsurance.Core.Models
{
    public class UserSession
    {
        public long ChatId { get; set; }
        public UserState State { get; set; } = UserState.Start;

        // Passport fields (Mindee model #1)
        public string? GivenNames { get; set; }
        public string? Surnames { get; set; }
        public string? DocumentNumber { get; set; }

        // Vehicle doc fields (Mindee model #2)
        public string? CarModel { get; set; }
        public string? LicensePlate { get; set; }
    }
}
