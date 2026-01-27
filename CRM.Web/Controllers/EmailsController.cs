using CRM.Core.Models;
using CRM.Core.Storage;
using CRM.Email.Abstractions;
using CRM.Email.Services;
using CRM.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Web.Controllers;

public sealed class EmailsController : Controller
{
    private readonly IAccountStore _accountStore;
    private readonly IEmailThreadQuery _threadQuery;
    private readonly IEmailThreadStore _threadStore;
    private readonly EmailSyncService _syncService;
    private readonly IEmailProvider _provider;
    private readonly IEmailService _emailService;
    private readonly EmailProviderOptions _providerOptions;

    public EmailsController(
        IAccountStore accountStore,
        IEmailThreadQuery threadQuery,
        IEmailThreadStore threadStore,
        EmailSyncService syncService,
        IEmailProvider provider,
        IEmailService emailService,
        EmailProviderOptions providerOptions)
    {
        _accountStore = accountStore;
        _threadQuery = threadQuery;
        _threadStore = threadStore;
        _syncService = syncService;
        _provider = provider;
        _emailService = emailService;
        _providerOptions = providerOptions;
    }

    [HttpPost]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var mailbox = _providerOptions.ServiceMailboxAddress;
        if (string.IsNullOrWhiteSpace(mailbox))
        {
            return BadRequest("Service mailbox address not configured.");
        }

        await _syncService.SyncMailboxAsync(
            new EmailSyncRequest { MailboxAddress = mailbox, Since = DateTimeOffset.UtcNow.AddDays(-7) },
            cancellationToken);

        return RedirectToAction("Orphans");
    }

    public async Task<IActionResult> Orphans(CancellationToken cancellationToken)
    {
        var model = new OrphanEmailsViewModel
        {
            OrphanMessages = await _threadQuery.GetOrphanMessagesAsync(cancellationToken)
        };

        ViewBag.Accounts = await _accountStore.GetAllAsync(cancellationToken);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> MapOrphan(string messageId, int accountId, CancellationToken cancellationToken)
    {
        await _threadQuery.SetAccountForMessageAsync(messageId, accountId, cancellationToken);
        return RedirectToAction("Orphans");
    }

    [HttpPost]
    public async Task<IActionResult> MoveThread(string conversationId, int accountId, int currentAccountId, CancellationToken cancellationToken)
    {
        await _threadQuery.ReassignThreadAsync(conversationId, accountId, cancellationToken);
        return RedirectToAction("Details", "Accounts", new { id = currentAccountId });
    }

    public async Task<IActionResult> Content(string messageId, CancellationToken cancellationToken)
    {
        var content = await _threadStore.GetContentAsync(messageId, cancellationToken);
        if (content is null)
        {
            content = await _provider.FetchContentAsync(messageId, cancellationToken);
            if (content is not null)
            {
                await _threadStore.SaveContentAsync(content, cancellationToken);
            }
        }

        if (content is null)
        {
            return NotFound();
        }

        return View(content);
    }

    [HttpGet]
    public async Task<IActionResult> Compose(int accountId, CancellationToken cancellationToken)
    {
        var account = await _accountStore.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        var model = new ComposeEmailViewModel
        {
            From = _providerOptions.ServiceMailboxAddress,
            AccountGuid = account.AccountGuid
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Compose(ComposeEmailViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var message = new EmailMessage
        {
            From = model.From,
            To = model.To.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Cc = model.Cc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Subject = model.Subject,
            TextBody = model.Body,
            AccountGuidStamp = model.AccountGuid
        };

        await _emailService.SendAsync(message, cancellationToken);
        return RedirectToAction("Index", "Accounts");
    }
}
