using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace QuoteBot.Services;

public class NoQuotesFoundException : Exception
{
}

public class QuoteService
{
    private readonly IMongoCollection<Quote> _quoteCollection;
    private readonly ThreadLocal<Random> _random = new (() => new Random());

    private static bool _indexesCreated;

    public QuoteService(IMongoClient client)
    {
        var database = client.GetDatabase("quoteBot");
        _quoteCollection = database.GetCollection<Quote>("quotes");
        
        // Create indexes if they don't exist
        if (_indexesCreated) 
            return;

        var uniqueOption = new CreateIndexOptions { Unique = true };
        _quoteCollection.Indexes.CreateMany(new CreateIndexModel<Quote>[]
        {
            new (Builders<Quote>.IndexKeys.Ascending(q => q.QuoteId), uniqueOption),
            new (Builders<Quote>.IndexKeys.Ascending(q => q.Author)),
            new (Builders<Quote>.IndexKeys.Ascending(q => q.Name))
        });
        
        _indexesCreated = true;
    }

    private string CreateRandomAlphaId()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz";
        var result = new string(
            Enumerable.Repeat(chars, 4)
                      .Select(s => s[_random.Value!.Next(s.Length)])
                      .ToArray());
        
        return result;
    }
    
    /// <summary>
    /// Adds a quote to the database and returns the new quote object
    /// </summary>
    /// <returns>The created quote object</returns>
    private async Task<Quote> AddQuoteAsync(ulong serverId, QuoteType type, string body, string? author, string? name)
    {
        // Generate a new ID for the quote and check if it already exists
        string id;
        do
        {
            id = CreateRandomAlphaId();
        } 
        while (await _quoteCollection.AsQueryable().AnyAsync(q => q.ServerId == serverId && q.QuoteId == id));
        
        var quote = new Quote
        {
            ServerId = serverId,
            QuoteId = id,
            Type = type,
            Name = name,
            Author = author,
            Body = body
        };
        
        await _quoteCollection.InsertOneAsync(quote);
        
        if (author != null && !AuthorCache.Contains(serverId, author))
            AuthorCache.AddAuthor(serverId, author);

        return quote;
    }
    
    /// <summary>
    /// Adds a text quote to the database and returns the new quote object
    /// </summary>
    /// <param name="serverId">Discord server to associate with the quote</param>
    /// <param name="body">Quote text</param>
    /// <param name="author">Author of the quote, optional</param>
    /// <returns>The created quote object</returns>
    public async Task<Quote> AddTextQuoteAsync(ulong serverId, string body, string? author)
    {
        return await AddQuoteAsync(serverId, QuoteType.Text, body, author, null);
    }
    
    /// <summary>
    /// Adds a quote to the database and returns the new quote object
    /// </summary>
    /// <param name="serverId">Discord server to associate with the quote</param>
    /// <param name="url">Link to the image</param>
    /// <param name="author">Author of the quote, optional</param>
    /// <returns>The created quote object</returns>
    public async Task<Quote> AddImageQuoteAsync(ulong serverId, string url, string? author)
    {
        return await AddQuoteAsync(serverId, QuoteType.Image, url, author, null);
    }

    /// <summary>
    /// Gets a quote by its unique name
    /// </summary>
    /// <param name="serverId">Discord server to retrieve from</param>
    /// <param name="name">Name of the quote</param>
    /// <returns>The quote object, or null if not found</returns>
    public async Task<Quote?> GetQuoteByNameAsync(ulong serverId, string name)
    {
        var value = await _quoteCollection.AsQueryable().FirstOrDefaultAsync(q => q.ServerId == serverId && q.Name == name);
        return value;
    }

    /// <summary>
    /// Tries to add a named text quote
    /// </summary>
    /// <param name="serverId">Discord server to associate with the quote</param>
    /// <param name="body">Quote text</param>
    /// <param name="author">Author of the quote, optional</param>
    /// <param name="name">Name of the quote</param>
    /// <returns>The quote object and true if no quote with that name already existed, otherwise null and false</returns>
    public async Task<(Quote? quote, bool Success)> TryAddNamedTextQuoteAsync(ulong serverId, string body,
        string? author, string name)
    {
        return await GetQuoteByNameAsync(serverId, name) != null 
            ? (null, false) 
            : (await AddQuoteAsync(serverId, QuoteType.Text, body, author, name), true);
    }
    
    /// <summary>
    /// Tries to add a named image quote
    /// </summary>
    /// <param name="serverId">Discord server to associate with the quote</param>
    /// <param name="url">Link to the image</param>
    /// <param name="author">Author of the quote, optional</param>
    /// <param name="name">Name of the quote</param>
    /// <returns>The quote object and true if no quote with that name already existed, otherwise null and false</returns>
    public async Task<(Quote? quote, bool Success)> TryAddNamedImageQuoteAsync(ulong serverId, string url,
        string? author, string name)
    {
        return await GetQuoteByNameAsync(serverId, name) != null 
            ? (null, false) 
            : (await AddQuoteAsync(serverId, QuoteType.Image, url, author, name), true);
    }
    
    /// <summary>
    /// Gets a random server quote
    /// </summary>
    /// <param name="serverId">Discord server to take quotes from</param>
    /// <returns>A random quote</returns>
    /// <exception cref="NoQuotesFoundException">If no quotes were found for the server</exception>
    public async Task<Quote> GetRandomQuoteAsync(ulong serverId)
    {
        var quotes = await _quoteCollection.AsQueryable().Where(q => q.ServerId == serverId).ToListAsync();

        if (quotes.Count == 0)
            throw new NoQuotesFoundException();
        
        return quotes[_random.Value!.Next(quotes.Count)];
    }
    
    /// <summary>
    /// Gets all text quotes for a server
    /// </summary>
    /// <param name="serverId">Discord server to list quotes for</param>
    /// <returns>A list of all text quotes</returns>
    public async Task<IList<Quote>> GetTextQuotesAsync(ulong serverId)
    {
        return await _quoteCollection.AsQueryable().Where(q => q.ServerId == serverId && q.Type == QuoteType.Text).ToListAsync();
    }
    
    /// <summary>
    /// Gets all image quotes for a server
    /// </summary>
    /// <param name="serverId">Discord server to list quotes for</param>
    /// <returns>A list of all image quotes</returns>
    public async Task<IList<Quote>> GetImageQuotesAsync(ulong serverId)
    {
        return await _quoteCollection.AsQueryable().Where(q => q.ServerId == serverId && q.Type == QuoteType.Image).ToListAsync();
    }

    /// <summary>
    /// Gets a random server quote from an author
    /// </summary>
    /// <param name="serverId">Discord server to take quotes from</param>
    /// <param name="author">Author to search for</param>
    /// <returns>A random quote or null if the author was not found</returns>
    public async Task<Quote?> GetRandomQuoteByAuthorAsync(ulong serverId, string author)
    {
        var quotes = await GetQuotesByAuthorAsync(serverId, author);
        
        return quotes.Count == 0 
            ? null 
            : quotes[_random.Value!.Next(quotes.Count)];
    }
    
    /// <summary>
    /// Gets all quotes from an author
    /// </summary>
    /// <param name="serverId">Discord server to list quotes for</param>
    /// <param name="author">Author to find quotes from</param>
    /// <returns>All quotes found by the author, or an empty list if none were found</returns>
    public async Task<IList<Quote>> GetQuotesByAuthorAsync(ulong serverId, string author)
    {
        return await _quoteCollection.AsQueryable().Where(q => q.ServerId == serverId && q.Author == author).ToListAsync();
    }

    /// <summary>
    /// Attempts to the delete a quote by its ID
    /// </summary>
    /// <param name="serverId">Discord server the quote is from</param>
    /// <param name="quoteId">The ID of the quote</param>
    /// <returns>If the quote exists</returns>
    public async Task<bool> TryDeleteQuoteAsync(ulong serverId, string quoteId)
    {
        // Get quote for author lookup
        var quote = await _quoteCollection.AsQueryable().FirstOrDefaultAsync(q => q.ServerId == serverId && q.QuoteId == quoteId);
        
        var result = await _quoteCollection.DeleteOneAsync(q => q.ServerId == serverId && q.QuoteId == quoteId);
        if (result.DeletedCount == 0)
            return false;
        
        // If all quotes by the author are now gone, remove from cache
        if (quote.Author == "")
            return true;
        
        var author = await _quoteCollection.AsQueryable().Where(q => q.ServerId == serverId && q.Author == quote.Author).ToListAsync();
        if (author.Count == 0)
            AuthorCache.RemoveAuthor(serverId, quoteId);
        
        return true;
    }    
}