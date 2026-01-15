namespace TelegramBotCarInsurance.Core.Enums
{
    /// <summary>
    /// Represents the current step (state) of the user's interaction
    /// with the car insurance Telegram bot.
    /// Used as a state machine to control the bot workflow.
    /// </summary>
    public enum UserState
    {
        /// <summary>
        /// Initial state.
        /// The user has just started the bot or completed a previous flow.
        /// </summary>
        Start,

        /// <summary>
        /// The bot is waiting for the user to send a photo of their passport.
        /// Text messages are answered by AI, photos move the flow forward.
        /// </summary>
        WaitingForPassport,

        /// <summary>
        /// Passport data has been extracted and shown to the user.
        /// The bot is waiting for a Yes/No confirmation.
        /// </summary>
        ConfirmingPassport,

        /// <summary>
        /// The bot is waiting for a photo of the vehicle registration document.
        /// Text messages are answered by AI, photos move the flow forward.
        /// </summary>
        WaitingForVehicleDoc,

        /// <summary>
        /// Vehicle data has been extracted and shown to the user.
        /// The bot is waiting for a Yes/No confirmation.
        /// </summary>
        ConfirmingVehicleDoc,

        /// <summary>
        /// The bot is waiting for the user's agreement with the fixed insurance price.
        /// </summary>
        PriceAgreement,

        /// <summary>
        /// The insurance policy has been issued and sent to the user.
        /// The flow is completed.
        /// </summary>
        Completed
    }
}
