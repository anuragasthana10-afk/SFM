using System.Text.Json;

namespace ZohoBooksSync.Persistence;

internal sealed class FileReferenceStore : IReferenceStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileReferenceStore(string path)
    {
        _path = path;
    }

    public async Task<string?> GetItemReferenceAsync(string localItemId, CancellationToken cancellationToken = default)
        => (await LoadAsync(cancellationToken)).Items.TryGetValue(localItemId, out var id) ? id : null;

    public async Task<string?> GetInvoiceReferenceAsync(string localInvoiceId, CancellationToken cancellationToken = default)
        => (await LoadAsync(cancellationToken)).Invoices.TryGetValue(localInvoiceId, out var id) ? id : null;

    public async Task SaveItemReferenceAsync(string localItemId, string zohoItemId, CancellationToken cancellationToken = default)
    {
        var state = await LoadAsync(cancellationToken);
        state.Items[localItemId] = zohoItemId;
        await SaveAsync(state, cancellationToken);
    }

    public async Task SaveInvoiceReferenceAsync(string localInvoiceId, string zohoInvoiceId, CancellationToken cancellationToken = default)
    {
        var state = await LoadAsync(cancellationToken);
        state.Invoices[localInvoiceId] = zohoInvoiceId;
        await SaveAsync(state, cancellationToken);
    }

    private async Task<ReferenceState> LoadAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_path))
            {
                return new ReferenceState();
            }

            var json = await File.ReadAllTextAsync(_path, cancellationToken);
            return JsonSerializer.Deserialize<ReferenceState>(json) ?? new ReferenceState();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveAsync(ReferenceState state, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_path, json, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed class ReferenceState
    {
        public Dictionary<string, string> Items { get; init; } = new();
        public Dictionary<string, string> Invoices { get; init; } = new();
    }
}
