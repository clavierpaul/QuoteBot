using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace QuoteBot;

public class AuthorCompletionStrict : IAutocompleteProvider
{
    public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
    {
        var matches = AuthorCache.GetPrefixMatches(ctx.Guild.Id, ctx.OptionValue.ToString()!);
        
        return Task.FromResult(matches.Select(m => new DiscordAutoCompleteChoice(m, m)));
    }
}

public class AuthorCompletion : IAutocompleteProvider
{
    public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
    {
        var results = new List<DiscordAutoCompleteChoice>();
        
        var search = ctx.OptionValue.ToString()!;

        results.Add(search != ""
            ? new DiscordAutoCompleteChoice(search, ctx.OptionValue)
            : new DiscordAutoCompleteChoice("<none>", ""));

        results.AddRange(AuthorCache.GetPrefixMatches(ctx.Guild.Id, search).Select(result => new DiscordAutoCompleteChoice(result, result)));

        return Task.FromResult(results.AsEnumerable());
    }
}

public abstract class QuoteCommandGroup : ApplicationCommandModule
{
    protected readonly QuoteService _quoteService;

    protected static DiscordEmbed CreateQuoteEmbed(Quote quote)
    {
        var embedBuilder = new DiscordEmbedBuilder();

        switch (quote.Type)
        {
            case QuoteType.Text:
                embedBuilder.WithDescription(quote.Body);
                embedBuilder.WithAuthor(quote.Author != "" ? $"Quote by {quote.Author}" : "Quote");
                break;
            case QuoteType.Image:
                embedBuilder.WithImageUrl(quote.Body);
                if (quote.Author != "")
                    embedBuilder.WithAuthor($"Quote by {quote.Author}");
                
                break;
            default:
                throw new ArgumentOutOfRangeException($"Invalid quote type {quote.Type}");
        }
        
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
            [Option("author", "Author of the quote")] 
            [Autocomplete(typeof(AuthorCompletion))]
            string author = "",
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
        
        [SlashCommand("url", "Add an image quote from a link")]
        public async Task AddImageQuoteFromLink(InteractionContext ctx, 
            [Option("url", "Link to the image")] string link,
            [Option("author", "Author of the quote")]
            [Autocomplete(typeof(AuthorCompletion))]
            string author = "",
            [Option("name", "Quote name")] string name = "")
        {
            var quote = await _quoteService.AddImageQuoteAsync(ctx.Guild.Id, link, author, name);
            await ctx.CreateResponseAsync("Quote added!", CreateQuoteEmbed(quote));
        }
        
        [SlashCommand("file", "Add an image quote from a file")]
        public async Task AddImageQuoteFromFile(InteractionContext ctx, 
            [Option("file", "File to the image")] DiscordAttachment file,
            [Option("author", "Author of the quote")]
            [Autocomplete(typeof(AuthorCompletion))]
            string author = "",
            [Option("name", "Quote name")] string name = "")
        {
            await ctx.CreateResponseAsync("Not implemented.");
        }
    }

    [SlashCommand("deletequote", "Remove a quote from the database")]
    public async Task DeleteQuote(InteractionContext ctx, [Option("id", "ID of the quote")] string quoteId)
    {
        if (await _quoteService.TryDeleteQuoteAsync(ctx.Guild.Id, quoteId))
        {
            await ctx.CreateResponseAsync(":wastebasket:  Quote deleted!");
        }
        else
        {
            await ctx.CreateResponseAsync($":x:  No quote by `{quoteId}` found.", true);
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
            await ctx.CreateResponseAsync(":x:  No quotes found!");
        }
    }
}