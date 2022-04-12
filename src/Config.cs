using JetBrains.Annotations;

namespace QuoteBot;

public class Config
{
    public string Token { get; [UsedImplicitly] init; } = null!;
    public ulong? TestServer { get; [UsedImplicitly] init; }
    public string ConnectionString { get; [UsedImplicitly] init; } = null!;
}