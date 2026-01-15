using TelegramBotCarInsurance.Application.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;

namespace TelegramBotCarInsurance
{
    /// <summary>
    /// Background worker responsible for running Telegram long polling.
    /// Listens for incoming updates and forwards them to the UpdateHandler.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<Worker> _logger;

        /// <summary>
        /// Worker constructor.
        /// Dependencies are injected via DI container.
        /// </summary>
        public Worker(
            ITelegramBotClient botClient,
            IServiceProvider serviceProvider,
            ILogger<Worker> logger)
        {
            _botClient = botClient;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Entry point for the background service.
        /// Starts Telegram long polling and listens for updates
        /// until the application is stopped.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Configure which update types the bot should receive.
            // Empty array means "receive all update types".
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = []
            };

            // Start receiving updates from Telegram
            await _botClient.ReceiveAsync(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken
            );

            // This line will be reached only when polling stops
            _logger.LogInformation("Bot stopped.");
        }

        /// <summary>
        /// Handles incoming Telegram updates.
        /// Creates a new DI scope for each update to ensure
        /// scoped services (e.g., UpdateHandler) are resolved correctly.
        /// </summary>
        private async Task HandleUpdateAsync(
            ITelegramBotClient botClient,
            Telegram.Bot.Types.Update update,
            CancellationToken cancellationToken)
        {
            // Create a new dependency injection scope per update
            using (var scope = _serviceProvider.CreateScope())
            {
                // Resolve UpdateHandler from DI container
                var handler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();

                try
                {
                    // Delegate update processing to application layer
                    await handler.HandleUpdateAsync(update);
                }
                catch (Exception ex)
                {
                    // Log any unhandled errors during update processing
                    _logger.LogError($"Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handles errors that occur during Telegram polling
        /// (e.g., network issues, API errors).
        /// </summary>
        private Task HandlePollingErrorAsync(
            ITelegramBotClient botClient,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError($"Telegram Error: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
