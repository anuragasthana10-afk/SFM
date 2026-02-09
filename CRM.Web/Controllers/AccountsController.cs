using CRM.Models;
using CRM.Storage;
using CRM.Email.Abstractions;
using CRM.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Web.Controllers;

public sealed class AccountsController : Controller
{
    private readonly IAccountStore _accountStore;
    private readonly IEmailThreadQuery _threadQuery;

    public AccountsController(IAccountStore accountStore, IEmailThreadQuery threadQuery)
    {
        _accountStore = accountStore;
        _threadQuery = threadQuery;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new AccountListViewModel
        {
            Accounts = await _accountStore.GetAllAsync(cancellationToken)
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Create() => View(new AccountEditViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(AccountEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        await _accountStore.AddAsync(new Account
        {
            Name = model.Name,
            AccountGuid = model.AccountGuid == Guid.Empty ? Guid.NewGuid() : model.AccountGuid
        }, cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var account = await _accountStore.GetByIdAsync(id, cancellationToken);
        if (account is null) return NotFound();

        return View(new AccountEditViewModel { Id = account.Id, Name = account.Name, AccountGuid = account.AccountGuid });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(AccountEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        await _accountStore.UpdateAsync(new Account { Id = model.Id, Name = model.Name, AccountGuid = model.AccountGuid }, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _accountStore.DeleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, string view = "thread", string? conversationId = null, CancellationToken cancellationToken = default)
    {
        var account = await _accountStore.GetByIdAsync(id, cancellationToken);
        if (account is null) return NotFound();

        var normalizedView = view.Equals("flat", StringComparison.OrdinalIgnoreCase) ? "flat" : "thread";
        var allMessages = await _threadQuery.GetMessagesByAccountAsync(id, cancellationToken);
        var messages = normalizedView == "thread"
            ? (!string.IsNullOrWhiteSpace(conversationId)
                ? allMessages.Where(m => m.ConversationId == conversationId).OrderByDescending(m => m.ReceivedAt).ToList()
                : new List<EmailMessageMetadata>())
            : allMessages;

        var threads = await _threadQuery.GetThreadsByAccountAsync(id, cancellationToken);
        var username = User?.Identity?.Name ?? "demo.user@crm.local";
        var userId = await _threadQuery.EnsureUserAsync(username, "Demo", "User", cancellationToken);
        var readMessages = await _threadQuery.GetReadMessageIdsAsync(id, userId, cancellationToken);
        var readThreads = await _threadQuery.GetReadConversationIdsAsync(id, userId, cancellationToken);

        var model = new AccountEmailsViewModel
        {
            Account = account,
            Messages = messages,
            Threads = threads,
            ViewMode = normalizedView,
            SelectedConversationId = conversationId,
            ReadMessageIds = readMessages,
            ReadConversationIds = readThreads
        };

        ViewBag.Accounts = await _accountStore.GetAllAsync(cancellationToken);
        return View(model);
    }
}
