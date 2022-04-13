using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.Linq;

namespace QuoteBot;

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

        return quote;
    }
    
    /// <summary>
    /// Adds a text quote to the database and returns the new quote object
    /// </summary>
    /// <param name="serverId">Discord server to associate with the quote</param>
    /// <param name="body">Quote text</param>
    /// <param name="author">Author of the quote, optional</param>
    /// <param name="name">Lookup name for the quote, optional</param>
    /// <returns>The created quote object</returns>
    public async Task<Quote> AddTextQuoteAsync(ulong serverId, string body, string? author, string? name)
    {
        return await AddQuoteAsync(serverId, QuoteType.Text, body, author, name);
    }
    
    /// <summary>
    /// Adds a quote to the database and returns the new quote object
    /// </summary>
    /// <param name="serverId">Discord server to associate with the quote</param>
    /// <param name="url">Link to the image</param>
    /// <param name="author">Author of the quote, optional</param>
    /// <param name="name">Lookup name for the quote, optional</param>
    /// <param name="isFile">Whether the image is a file that needs to be hosted or not</param>
    /// <returns>The created quote object</returns>
    public async Task<Quote> AddImageQuoteAsync(ulong serverId, string url, string? author, string? name, bool isFile = false)
    {
        return await AddQuoteAsync(serverId, QuoteType.Image, url, author, name);
    }
    
    /// <summary>
    /// Returns a random server quote
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
    /// Attempts to the delete a quote by its ID
    /// </summary>
    /// <param name="serverId">Discord server the quote is from</param>
    /// <param name="quoteId">The ID of the quote</param>
    /// <returns>If the quote exists</returns>
    public async Task<bool> TryDeleteQuoteAsync(ulong serverId, string quoteId)
    {
        var result = await _quoteCollection.DeleteOneAsync(q => q.ServerId == serverId && q.QuoteId == quoteId);
        return result.DeletedCount > 0;
    }    
}