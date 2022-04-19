using System.Text;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.EventHandling;
using DSharpPlus.Interactivity.Extensions;
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

    [SlashCommandGroup("listquotes", "List quotes")]
    public class ListQuoteCommands : QuoteCommandGroup
    {
        public ListQuoteCommands(QuoteService quoteService) : base(quoteService)
        {
        }

        public string CreateInlineQuote(Quote quote)
        {
            var sb = new StringBuilder();
            sb.Append($"\"{quote.Body}\"");
            if (quote.Author != "")
            {
                sb.Append($" - {quote.Author}");
            }
            
            if (quote.Name != "")
            {
                sb.Append($" ({quote.Name})");
            }

            sb.Append($" `(id: {quote.QuoteId})`");

            return sb.ToString();
        }

        [SlashCommand("text", "List all text quotes in the server")]
        public async Task GetAllTextQuotes(InteractionContext ctx)
        {
            var quotes = await QuoteService.GetTextQuotesAsync(ctx.Guild.Id);
            
            if (!quotes.Any())
            {
                await ctx.CreateResponseAsync(":x:  No quotes found.");
                return;
            }
            
            var pages = new List<Page>();
            var totalPages = (int) Math.Ceiling((double) quotes.Count / 15);
            
            for (var i = 0; i < quotes.Count; i += 15)
            {
                var quotePage = quotes
                    .Skip(i)
                    .Take(15)
                    .Select(CreateInlineQuote)
                    .ToList();
                
                var quoteCount = (i + 1) * 15 > quotes.Count ? quotes.Count : (i + 1) * 15;

                var embed = new DiscordEmbedBuilder()
                    .WithTitle($"{ctx.Guild.Name} Quotes")
                    .WithThumbnail(ctx.Guild.IconUrl)
                    .WithDescription(string.Join("\n", quotePage))
                    .WithFooter($"Page {pages.Count + 1}/{totalPages} • {quoteCount}/{quotes.Count} quotes")
                    .WithColor(new DiscordColor("#3199d8"));
                
                pages.Add(new Page { Embed = embed });
            }

            await ctx.Interaction.SendPaginatedResponseAsync(false, ctx.User, pages);
        }

        [SlashCommand("image", "List all image quotes in the server")]
        public async Task GetAllImageQuotes(InteractionContext ctx)
        {
            var images = await QuoteService.GetImageQuotesAsync(ctx.Guild.Id);
            
            if (!images.Any())
            {
                await ctx.CreateResponseAsync(":x:  No images found.");
                return;
            }

            var pages = new List<Page>();
            var i = 1;
            
            foreach (var image in images)
            {
                var embed = new DiscordEmbedBuilder()
                    .WithTitle($"{ctx.Guild.Name} Images")
                    .WithImageUrl(image.Body)
                    .WithFooter($"id: {image.QuoteId} • {i}/{images.Count} images")
                    .WithColor(new DiscordColor("#3199d8"));
                
                if (image.Author != "")
                {
                    embed.WithAuthor($"By {image.Author}");
                }
                
                pages.Add(new Page { Embed = embed });
                
                i += 1;
            }
            
            await ctx.Interaction.SendPaginatedResponseAsync(false, ctx.User, pages);
        }

        [SlashCommand("author", "List all quotes by a specific author")]
        public async Task GetAllAuthorQuotes(InteractionContext ctx,
            [Option("author", "Author to get quotes by")]
            [Autocomplete(typeof(AuthorCompletionStrict))] 
            string author)
        {
            if (author == "")
            {
                await ctx.CreateResponseAsync(":x:  No author specified!");
                return;
            }
            
            var quotes = await QuoteService.GetQuotesByAuthorAsync(ctx.Guild.Id, author);
            
            if (!quotes.Any())
            {
                await ctx.CreateResponseAsync($":x:  No quotes by {author} found.");
                return;
            }
            
            var pages = new List<Page>();
            var i = 1;

            foreach (var quote in quotes)
            {
                var embed = new DiscordEmbedBuilder()
                    .WithTitle($"Quotes by {author}")
                    .WithFooter($"id: {quote.QuoteId} • {i}/{quotes.Count} quotes")
                    .WithColor(new DiscordColor("#3199d8"));
                
                switch (quote.Type)
                {
                    case QuoteType.Text:
                        embed.WithDescription(quote.Body);
                        break;
                    case QuoteType.Image:
                        embed.WithImageUrl(quote.Body);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Invalid quote type {quote.Type}");
                }
                
                pages.Add(new Page { Embed = embed });

                i += 1;
            }
            
            await ctx.Interaction.SendPaginatedResponseAsync(false, ctx.User, pages);
        }
    }
}