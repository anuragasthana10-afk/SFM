namespace ZohoBooksSync.Services;

public sealed class ZohoApiException : Exception
{
    public ZohoApiException(string message, string? content = null) : base(message)
    {
        Content = content;
    }

    public string? Content { get; }
}
