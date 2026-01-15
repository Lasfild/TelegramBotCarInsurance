using TelegramBotCarInsurance.Application.Services;
using TelegramBotCarInsurance.Core.Interfaces;
using TelegramBotCarInsurance.Infrastructure.ExternalServices;
using TelegramBotCarInsurance.Infrastructure.Persistence;
using Telegram.Bot;
using TelegramBotCarInsurance;

// Create and configure the application host
IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // =========================
        // Telegram Bot configuration
        // =========================

        // Read Telegram bot token from configuration
        var token = context.Configuration["Telegram:Token"];
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentNullException("Telegram Token not found");

        // Register Telegram bot client as a singleton
        services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(token));

        // =========================
        // Application persistence
        // =========================

        // In-memory storage for user sessions (state machine)
        // Used to track user progress through the bot flow
        services.AddSingleton<IUserRepository, InMemoryUserRepository>();

        // =========================
        // AI integration (Groq)
        // =========================

        // Bind Groq configuration (API key, model, etc.)
        services.Configure<GroqOptions>(context.Configuration.GetSection("Groq"));

        // Register Groq AI service as implementation of IAiAssistant
        // Used for answering user questions and generating insurance policies
        services.AddHttpClient<IAiAssistant, GroqAiService>();

        // =========================
        // Mindee configuration
        // =========================

        // Bind Mindee options for passport recognition model
        services.Configure<MindeePassportOptions>(
            context.Configuration.GetSection("MindeePassport"));

        // Bind Mindee options for vehicle registration document model
        services.Configure<MindeeVehicleOptions>(
            context.Configuration.GetSection("MindeeVehicle"));

        // =========================
        // Mindee REST clients
        // =========================

        // HTTP client for passport document parsing
        // Redirects are disabled because Mindee returns polling URLs
        services.AddHttpClient<MindeePassportParser>()
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false });

        // HTTP client for vehicle document parsing
        // Redirects are disabled for the same reason as above
        services.AddHttpClient<MindeeVehicleDocParser>()
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false });

        // =========================
        // Bot runtime services
        // =========================

        // Main update handler containing bot business logic
        services.AddScoped<UpdateHandler>();

        // Background worker that starts Telegram long polling
        services.AddHostedService<Worker>();
    })
    .Build();

// Start the application and begin listening for Telegram updates
await host.RunAsync();
