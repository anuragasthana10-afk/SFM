using System.Text.Json;

namespace ZohoBooksSync.Persistence;

public sealed class ReferenceStore
{
    private readonly string _filePath;
    private ReferenceData _data;

    public ReferenceStore(string filePath)
    {
        _filePath = filePath;
        _data = new ReferenceData();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            _data = new ReferenceData();
            return;
        }

        await using var stream = File.OpenRead(_filePath);
        var data = await JsonSerializer.DeserializeAsync<ReferenceData>(stream, cancellationToken: cancellationToken);
        _data = data ?? new ReferenceData();
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        var options = new JsonSerializerOptions { WriteIndented = true };
        await JsonSerializer.SerializeAsync(stream, _data, options, cancellationToken);
    }

    public string? GetItemId(string localId) => _data.Items.GetValueOrDefault(localId);
    public string? GetContactId(string localId) => _data.Contacts.GetValueOrDefault(localId);
    public string? GetInvoiceId(string localId) => _data.Invoices.GetValueOrDefault(localId);

    public void SetItemId(string localId, string remoteId) => _data.Items[localId] = remoteId;
    public void SetContactId(string localId, string remoteId) => _data.Contacts[localId] = remoteId;
    public void SetInvoiceId(string localId, string remoteId) => _data.Invoices[localId] = remoteId;

    private sealed class ReferenceData
    {
        public Dictionary<string, string> Items { get; init; } = new();
        public Dictionary<string, string> Contacts { get; init; } = new();
        public Dictionary<string, string> Invoices { get; init; } = new();
    }
}
