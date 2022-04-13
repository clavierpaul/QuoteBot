using BinaryTree;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using QuoteBot;
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

var services = new ServiceCollection()
    .AddSingleton<IMongoClient>(_ => new MongoClient(config.ConnectionString))
    .AddScoped<QuoteService>();

var slash = discordClient.UseSlashCommands(new SlashCommandsConfiguration
{
    Services = services.BuildServiceProvider()
});

#if DEBUG
slash.RegisterCommands<Commands>(config.TestServer!);
#else
slash.RegisterCommands<Commands>();
#endif

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

var tree = new BinaryTree<string>(new [] {"Bence", "bence", "beast", "bees", "adam"});

foreach (var author in tree.AllMatches("be"))
{
    Console.WriteLine(author);
}

await discordClient.ConnectAsync();
await Task.Delay(-1);