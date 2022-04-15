using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;

namespace QuoteBot.Services;

public class UploadService
{ 
    private readonly HttpClient _httpClient;
    private readonly Config _config;
    
    public UploadService(HttpClient httpClient, Config config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    /// <summary>
    /// Downloads an image from the url and uploads it to the configured discord server.
    /// </summary>
    /// <param name="discord">Current discord client</param>
    /// <param name="url">Url of the file</param>
    /// <returns>Url of the new file</returns>
    public async Task<string> UploadImageAsync(DiscordClient discord, string url)
    {
        // Download the image into a file stream
        var fileName = Path.GetFileName(url);
        var image = await _httpClient.GetStreamAsync(url);
        var channel = await discord.GetChannelAsync(_config.UploadChannel);

        var message = await discord.SendMessageAsync(channel, new DiscordMessageBuilder().WithFile(fileName, image));
        return message.Attachments[0].Url;
    }
}