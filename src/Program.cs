using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.EventHandling;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using QuoteBot.Services;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var configDeserializer = new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .Build();

var configPath = Path.Combine(AppContext.BaseDirectory, "config.yml");
var config = configDeserializer.Deserialize<Config>(File.ReadAllText(configPath));

var discordClient = new DiscordClient(new DiscordConfiguration
{
    Token = config.Token
});

var mongoClient = new MongoClient(config.ConnectionString);

var services = new ServiceCollection()
    .AddSingleton(config)
    .AddSingleton<IMongoClient>(_ => mongoClient)
    .AddScoped<UploadService>()
    .AddScoped<QuoteService>();

services.AddHttpClient<UploadService>();

var slash = discordClient.UseSlashCommands(new SlashCommandsConfiguration
{
    Services = services.BuildServiceProvider()
});

discordClient.UseInteractivity(new InteractivityConfiguration
{
    AckPaginationButtons = true,
    ButtonBehavior = ButtonPaginationBehavior.DeleteButtons,
    Timeout = TimeSpan.FromSeconds(30),
    ResponseBehavior = InteractionResponseBehavior.Ack,
    PaginationButtons = new PaginationButtons
    {
        Left = new DiscordButtonComponent(ButtonStyle.Secondary, "left", "Previous", emoji: new DiscordComponentEmoji("◀")),
        Right = new DiscordButtonComponent(ButtonStyle.Secondary, "right", "Next", emoji: new DiscordComponentEmoji("▶")),
        SkipLeft = new DiscordButtonComponent(ButtonStyle.Secondary, "first", "First", emoji: new DiscordComponentEmoji("⏮️")),
        SkipRight = new DiscordButtonComponent(ButtonStyle.Secondary, "last", "Last", emoji: new DiscordComponentEmoji("⏭️")),
        Stop = new DiscordButtonComponent(ButtonStyle.Secondary, "stop", "Cancel", emoji: new DiscordComponentEmoji("⏹️"))
    }
});

#if DEBUG
slash.RegisterCommands<Commands>(config.TestServer!);
#else
slash.RegisterCommands<Commands>();
#endif

AuthorCache.Initialize(mongoClient.GetDatabase("quoteBot").GetCollection<Quote>("quotes"), discordClient.Logger);

discordClient.Ready += (sender, eventArgs) =>
{
    sender.Logger.LogInformation("Connected as {}#{}!", discordClient.CurrentUser.Username, discordClient.CurrentUser.Discriminator);
    return Task.CompletedTask;
};

slash.SlashCommandErrored += (sender, eventArgs) =>
{
    sender.Client.Logger.LogError("Error while executing command: {}", eventArgs.Exception);

    return Task.CompletedTask;
};

slash.AutocompleteErrored += (sender, eventArgs) =>
{
    sender.Client.Logger.LogError("Error while autocompleting command: {}", eventArgs.Exception);

    return Task.CompletedTask;
};

await discordClient.ConnectAsync();
await Task.Delay(-1);