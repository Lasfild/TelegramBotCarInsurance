using TelegramBotCarInsurance.Application.Services;
using TelegramBotCarInsurance.Core.Interfaces;
using TelegramBotCarInsurance.Infrastructure.ExternalServices;
using TelegramBotCarInsurance.Infrastructure.Persistence;
using Telegram.Bot;
using TelegramBotCarInsurance;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var token = context.Configuration["Telegram:Token"];
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentNullException("Telegram Token not found");

        services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(token));
        services.AddSingleton<IUserRepository, InMemoryUserRepository>();

        // Groq
        services.Configure<GroqOptions>(context.Configuration.GetSection("Groq"));
        services.AddHttpClient<IAiAssistant, GroqAiService>();

        // Mindee options: two different sections
        services.Configure<MindeePassportOptions>(context.Configuration.GetSection("MindeePassport"));
        services.Configure<MindeeVehicleOptions>(context.Configuration.GetSection("MindeeVehicle"));

        // Mindee REST clients (disable redirects)
        services.AddHttpClient<MindeePassportParser>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        services.AddHttpClient<MindeeVehicleDocParser>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        services.AddScoped<UpdateHandler>();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
