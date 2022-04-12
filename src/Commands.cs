using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace QuoteBot;

public abstract class QuoteCommandGroup : ApplicationCommandModule
{
    protected readonly QuoteService _quoteService;

    protected static DiscordEmbed CreateQuoteEmbed(Quote quote)
    {
        var embedBuilder = new DiscordEmbedBuilder();
        embedBuilder.WithAuthor(quote.Author != "" ? $"Quote by {quote.Author}" : "Quote");
        

        embedBuilder.WithDescription(quote.Body);
        embedBuilder.WithColor(new DiscordColor("#3199d8"));
        embedBuilder.WithFooter($"id: {quote.QuoteId}");
        
        return embedBuilder.Build();
    }
    
    protected QuoteCommandGroup(QuoteService quoteService)
    {
        _quoteService = quoteService;
    }
}

public class Commands : QuoteCommandGroup
{
    public Commands(QuoteService quoteService) : base(quoteService)
    {
    }

    [SlashCommandGroup("addquote", "Add a quote to the database")]
    public class AddQuoteCommands : QuoteCommandGroup
    {
        public AddQuoteCommands(QuoteService quoteService) : base(quoteService)
        {
        }
        
        [SlashCommand("text", "Add a text quote")]
        public async Task AddTextQuote(InteractionContext ctx, 
            [Option("quote", "Body of the quote")] string body,
            [Option("author", "Author of the quote")] string author = "",
            [Option("name", "Quote name")] string name = "")
        {
            // If the body has quotes at the start and end because someone didn't understand how the bot worked, remove them
            if (body.StartsWith("\"") && body.EndsWith("\""))
            {
                body = body.Substring(1, body.Length - 2);
            }
            
            var quote = await _quoteService.AddTextQuoteAsync(ctx.Guild.Id, body, author, name);
            await ctx.CreateResponseAsync("Quote added!", CreateQuoteEmbed(quote));
        }
    }
    
    [SlashCommand("quote", "Get a random quote")]
    public async Task GetRandomQuote(InteractionContext ctx)
    {
        try
        {
            var quote = await _quoteService.GetRandomQuoteAsync(ctx.Guild.Id);
            await ctx.CreateResponseAsync(CreateQuoteEmbed(quote));
        } 
        catch (NoQuotesFoundException)
        {
            await ctx.CreateResponseAsync("No quotes found!");
        }
    }
}