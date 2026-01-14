namespace TelegramBotCarInsurance.Core.Enums
{
    public enum UserState
    {
        Start,

        WaitingForPassport,
        ConfirmingPassport,

        WaitingForVehicleDoc,
        ConfirmingVehicleDoc,

        PriceAgreement,
        Completed
    }
}
