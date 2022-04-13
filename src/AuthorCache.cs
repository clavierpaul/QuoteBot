using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace QuoteBot;

public static class AuthorCache
{
    private static readonly ReaderWriterLockSlim Lock = new();
    private static readonly Dictionary<ulong, SortedSet<string>> Authors = new();

    private static bool _initialized;
    
    private static ILogger? _logger;

    /// <summary>
    /// Loads the authors from the database as well as setting the logger to use. Does nothing if the cache is already initialized.
    /// This method is not thread-safe.
    /// </summary>
    /// <param name="quoteCollection">The collection of quotes</param>
    /// <param name="logger">Logger to use</param>
    public static void Initialize(IMongoCollection<Quote> quoteCollection, ILogger logger)
    {
        if (_initialized)
            return;
        
        _logger = logger;
        
        var servers = quoteCollection
            .AsQueryable()
            .ToList()
            .GroupBy(x => x.ServerId);
        
        foreach (var server in servers)
        {
            var authors = server.AsEnumerable()
                .Where(q => q.Author != "")
                .GroupBy(q => q.Author).Select(x => x.Key!);
            
            Authors.Add(server.Key, new SortedSet<string>(authors));
        }
        
        _initialized = true;
    }

    /// <summary>
    /// Adds an author to the cache.
    /// </summary>
    /// <param name="serverId">Server to add the author to</param>
    /// <param name="author">Author to add</param>
    public static void AddAuthor(ulong serverId, string author)
    {
        Lock.EnterWriteLock();
        try
        {
            if (!Authors.ContainsKey(serverId))
                Authors.Add(serverId, new SortedSet<string>());

            Authors[serverId].Add(author);
        }
        catch (LockRecursionException e)
        {
            _logger.LogError("Error in author cache: {}", e);
        }
        finally
        {
            Lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Checks if an author is in the cache.
    /// </summary>
    /// <param name="serverId">Server the author belongs to</param>
    /// <param name="author">Author to check</param>
    /// <returns></returns>
    public static bool Contains(ulong serverId, string author)
    {
        Lock.EnterReadLock();
        try
        {
            return Authors.ContainsKey(serverId) && Authors[serverId].Contains(author);
        }
        catch (LockRecursionException e)
        {
            _logger.LogError("Error in author cache: {}", e);
        }
        finally
        {
            Lock.ExitReadLock();
        }
        
        return false;
    }

    /// <summary>
    /// Removes an author from the cache.
    /// </summary>
    /// <param name="serverId">Server to remove the author from</param>
    /// <param name="author">Author to remove</param>
    public static void RemoveAuthor(ulong serverId, string author)
    {
        Lock.EnterWriteLock();
        try
        {
            if (!Authors.ContainsKey(serverId))
                return;

            Authors[serverId].Remove(author);
        }
        catch (LockRecursionException e)
        {
            _logger.LogError("Error in author cache: {}", e);
        }
        finally
        {
            Lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Get all authors whose names begin with the given string.
    /// </summary>
    /// <param name="serverId">Server to search through</param>
    /// <param name="value">String to search for</param>
    /// <param name="limit">Number of results to return, default 8</param>
    /// <returns>Results of the search</returns>
    public static IEnumerable<string> GetPrefixMatches(ulong serverId, string value, int limit = 8)
    {
        Lock.EnterReadLock();
        try
        {
            var results = new List<string>();
            if (!Authors.ContainsKey(serverId))
                return results;

            var prefixFound = false;
            foreach (var author in Authors[serverId].TakeWhile(author => results.Count != limit))
            {
                if (author.StartsWith(value, StringComparison.OrdinalIgnoreCase))
                {
                    prefixFound = true;
                    results.Add(author);
                }
                else if (prefixFound)
                {
                    break;
                }
            }

            return results;
        }
        catch (LockRecursionException e)
        {
            _logger.LogError("Error in author cache: {}", e);
            return new List<string>();
        }
        finally
        {
            Lock.ExitReadLock();
        }
    }
}