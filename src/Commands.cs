using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace QuoteBot.Services;

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
        var matches = AuthorCache.GetPrefixMatches(ctx.Guild.Id, ctx.OptionValue.ToString()!).ToList();
        
        var search = ctx.OptionValue.ToString()!;

        if (!matches.Contains(search))
        {
            results.Add(search != ""
                ? new DiscordAutoCompleteChoice(search, ctx.OptionValue)
                : new DiscordAutoCompleteChoice("<none>", ""));
        }

        results.AddRange(matches.Select(result => new DiscordAutoCompleteChoice(result, result)));

        return Task.FromResult(results.AsEnumerable());
    }
}

public abstract class QuoteCommandGroup : ApplicationCommandModule
{
    protected readonly QuoteService QuoteService;

    protected static DiscordEmbed CreateQuoteEmbed(Quote quote)
    {
        var embedBuilder = new DiscordEmbedBuilder();

        switch (quote.Type)
        {
            case QuoteType.Text:
                embedBuilder.WithDescription(quote.Body);
                embedBuilder.WithAuthor(quote.Author != "" ? quote.Author : "Quote", iconUrl: "https://i.imgur.com/mSTMLkl.png");
                break;
            case QuoteType.Image:
                embedBuilder.WithImageUrl(quote.Body);
                embedBuilder.WithAuthor(quote.Author != "" ? quote.Author : "Image", iconUrl: "https://i.imgur.com/mSTMLkl.png");
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
        QuoteService = quoteService;
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
        private UploadService _uploadService;
        
        public AddQuoteCommands(QuoteService quoteService, UploadService uploadService) : base(quoteService)
        {
            _uploadService = uploadService;
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

            if (name == "")
            {
                var quote = await QuoteService.AddTextQuoteAsync(ctx.Guild.Id, body, author);
                await ctx.CreateResponseAsync("Quote added!", CreateQuoteEmbed(quote));
            }
            else
            {
                var (quote, success) = await QuoteService.TryAddNamedTextQuoteAsync(ctx.Guild.Id, body, author, name);
                if (success)
                {
                    await ctx.CreateResponseAsync("Quote added!", CreateQuoteEmbed(quote!));
                }
                else
                {
                    await ctx.CreateResponseAsync(":x:  A quote with that name already exists!");
                }
            }
        }
        
        [SlashCommand("url", "Add an image quote from a link")]
        public async Task AddImageQuoteFromLink(InteractionContext ctx, 
            [Option("url", "Link to the image")] string link,
            [Option("author", "Author of the quote")]
            [Autocomplete(typeof(AuthorCompletion))]
            string author = "",
            [Option("name", "Quote name")] string name = "")
        {
            if (name == "")
            {
                var quote = await QuoteService.AddImageQuoteAsync(ctx.Guild.Id, link, author);
                await ctx.CreateResponseAsync("Quote added!", CreateQuoteEmbed(quote));
            }
            else
            {
                var (quote, success) = await QuoteService.TryAddNamedImageQuoteAsync(ctx.Guild.Id, link, author, name);
                if (success)
                {
                    await ctx.CreateResponseAsync("Quote added!", CreateQuoteEmbed(quote!));
                }
                else
                {
                    await ctx.CreateResponseAsync(":x:  A quote with that name already exists!");
                }
            }
        }
        
        [SlashCommand("file", "Add an image quote from a file")]
        public async Task AddImageQuoteFromFile(InteractionContext ctx, 
            [Option("file", "File to the image")] DiscordAttachment file,
            [Option("author", "Author of the quote")]
            [Autocomplete(typeof(AuthorCompletion))]
            string author = "",
            [Option("name", "Quote name")] string name = "")
        {
            await ctx.DeferAsync();
            
            // Upload the image
            var image = await _uploadService.UploadImageAsync(ctx.Client, file.Url);

            if (name == "")
            {
                var quote = await QuoteService.AddImageQuoteAsync(ctx.Guild.Id, image, author);
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Quote added!").AddEmbed(CreateQuoteEmbed(quote)));
            }
            else
            {
                var (quote, success) = await QuoteService.TryAddNamedImageQuoteAsync(ctx.Guild.Id, image, author, name);
                if (success)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Quote added!").AddEmbed(CreateQuoteEmbed(quote!)));
                }
                else
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(":x:  A quote with that name already exists!"));
                }
            }
        }
    }

    [SlashCommand("deletequote", "Remove a quote from the database")]
    public async Task DeleteQuote(InteractionContext ctx, [Option("id", "ID of the quote")] string quoteId)
    {
        if (await QuoteService.TryDeleteQuoteAsync(ctx.Guild.Id, quoteId))
        {
            await ctx.CreateResponseAsync(":wastebasket:  Quote deleted!");
        }
        else
        {
            await ctx.CreateResponseAsync($":x:  No quote by `{quoteId}` found.");
        }
    }
    
    [SlashCommand("quote", "Get a random quote")]
    public async Task GetRandomQuote(InteractionContext ctx)
    {
        try
        {
            var quote = await QuoteService.GetRandomQuoteAsync(ctx.Guild.Id);
            await ctx.CreateResponseAsync(CreateQuoteEmbed(quote));
        } 
        catch (NoQuotesFoundException)
        {
            await ctx.CreateResponseAsync(":x:  No quotes found!");
        }
    }

    [SlashCommandGroup("quoteby", "Get a quote by a query")]
    public class QuoteByCommands : QuoteCommandGroup
    {
        public QuoteByCommands(QuoteService quoteService) : base(quoteService)
        {
        }
        
        [SlashCommand("author", "Get a random quote by an author")]
        public async Task GetRandomQuoteByAuthor(InteractionContext ctx, 
            [Option("author", "Author of the quote")]
            [Autocomplete(typeof(AuthorCompletionStrict))]
            string author)
        {
            if (author == "")
            {
                await ctx.CreateResponseAsync(":x:  No author specified!");
                return;
            }
            
            var quote = await QuoteService.GetRandomQuoteByAuthorAsync(ctx.Guild.Id, author);
            if (quote == null)
            {
                await ctx.CreateResponseAsync($":x:  No quotes by {author} found.");
            }
            else
            {
                await ctx.CreateResponseAsync(CreateQuoteEmbed(quote));
            }
        }
        
        [SlashCommand("name", "Get a random quote by a name")]
        public async Task GetQuoteByName(InteractionContext ctx, [Option("name", "Name of the quote")] string name)
        {
            var quote = await QuoteService.GetQuoteByNameAsync(ctx.Guild.Id, name);
            if (quote == null)
            {
                await ctx.CreateResponseAsync($":x:  No quote associated with {name}");
            }
            else
            {
                await ctx.CreateResponseAsync(CreateQuoteEmbed(quote));
            }
        }
    }
}