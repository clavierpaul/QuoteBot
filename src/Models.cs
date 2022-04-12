using JetBrains.Annotations;
using MongoDB.Bson.Serialization.Attributes;

namespace QuoteBot;

public class Config
{
    public string Token { get; [UsedImplicitly] init; } = null!;
    public ulong? TestServer { get; [UsedImplicitly] init; }
    public string ConnectionString { get; [UsedImplicitly] init; } = null!;
}

public enum QuoteType
{
    Text,
    Image
}

[BsonIgnoreExtraElements]
public class Quote
{
    public ulong ServerId { get; set; }
    public string QuoteId { get; set; } = null!;
    public QuoteType Type { get; set; }
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string Body { get; set; } = null!;
}