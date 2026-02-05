using System.Text;
using System.Text.RegularExpressions;
using CRM.Core.Models;
using CRM.Core.Storage;
using CRM.Email.Abstractions;
using CRM.Email.Services;
using CRM.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

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
    private readonly IConfiguration _configuration;

    public EmailsController(
        IAccountStore accountStore,
        IEmailThreadQuery threadQuery,
        IEmailThreadStore threadStore,
        EmailSyncService syncService,
        IEmailProvider provider,
        IEmailService emailService,
        EmailProviderOptions providerOptions,
        IConfiguration configuration)
    {
        _accountStore = accountStore;
        _threadQuery = threadQuery;
        _threadStore = threadStore;
        _syncService = syncService;
        _provider = provider;
        _emailService = emailService;
        _providerOptions = providerOptions;
        _configuration = configuration;
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

    public async Task<IActionResult> Content(string messageId, int? accountId, CancellationToken cancellationToken)
    {
        var content = await _threadStore.GetContentAsync(messageId, cancellationToken);
        var source = "Database";
        if (content is null)
        {
            content = await _provider.FetchContentAsync(messageId, cancellationToken);
            if (content is not null)
            {
                await _threadStore.SaveContentAsync(content, cancellationToken);
            }

            source = "Provider";
        }

        if (content is null)
        {
            return NotFound();
        }

        var metadata = await _threadQuery.GetMessageByIdAsync(messageId, cancellationToken);
        ViewBag.ContentSource = source;
        ViewBag.MessageId = messageId;
        ViewBag.ConversationId = metadata?.ConversationId;
        ViewBag.AccountId = accountId ?? metadata?.AccountId;

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
            AccountGuid = account.AccountGuid,
            Mode = "Compose"
        };

        ViewBag.ShowBccField = IsBccVisible();
        return View(model);
    }

    [HttpGet("/Emails/Reply")]
    public async Task<IActionResult> Reply(string messageId, int accountId, CancellationToken cancellationToken)
    {
        var account = await _accountStore.GetByIdAsync(accountId, cancellationToken);
        var original = await _threadQuery.GetMessageByIdAsync(messageId, cancellationToken);
        if (account is null || original is null)
        {
            return NotFound();
        }

        var replyTo = original.Participants.Select(p => p.Address).FirstOrDefault() ?? string.Empty;
        var threadText = await BuildThreadTextAsync(original.ConversationId, cancellationToken);

        var model = new ComposeEmailViewModel
        {
            From = _providerOptions.ServiceMailboxAddress,
            To = replyTo,
            Subject = original.Subject.StartsWith("RE:", StringComparison.OrdinalIgnoreCase)
                ? original.Subject
                : $"RE: {original.Subject}",
            Body = $"\n\n--- Original Thread ---\n{threadText}",
            HtmlBody = ToHtmlWithLineBreaks($"\n\n--- Original Thread ---\n{threadText}"),
            AccountGuid = account.AccountGuid,
            ConversationId = original.ConversationId,
            InReplyToMessageId = original.MessageId,
            Mode = "Reply"
        };

        ViewBag.ShowBccField = IsBccVisible();
        return View("Compose", model);
    }

    [HttpGet("/Emails/Forward")]
    public async Task<IActionResult> Forward(string messageId, int accountId, CancellationToken cancellationToken)
    {
        var account = await _accountStore.GetByIdAsync(accountId, cancellationToken);
        var original = await _threadQuery.GetMessageByIdAsync(messageId, cancellationToken);
        if (account is null || original is null)
        {
            return NotFound();
        }

        var threadText = await BuildThreadTextAsync(original.ConversationId, cancellationToken);

        var model = new ComposeEmailViewModel
        {
            From = _providerOptions.ServiceMailboxAddress,
            Subject = original.Subject.StartsWith("FW:", StringComparison.OrdinalIgnoreCase)
                ? original.Subject
                : $"FW: {original.Subject}",
            Body = $"\n\n--- Forwarded Thread ---\n{threadText}",
            HtmlBody = ToHtmlWithLineBreaks($"\n\n--- Forwarded Thread ---\n{threadText}"),
            AccountGuid = account.AccountGuid,
            ConversationId = original.ConversationId,
            InReplyToMessageId = original.MessageId,
            Mode = "Forward"
        };

        ViewBag.ShowBccField = IsBccVisible();
        return View("Compose", model);
    }

    [HttpPost]
    public async Task<IActionResult> Compose(ComposeEmailViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ShowBccField = IsBccVisible();
            return View(model);
        }

        var message = new EmailMessage
        {
            From = model.From,
            To = model.To.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Cc = model.Cc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Bcc = model.Bcc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Subject = model.Subject,
            TextBody = model.Body,
            HtmlBody = string.IsNullOrWhiteSpace(model.HtmlBody) ? $"<pre>{System.Net.WebUtility.HtmlEncode(model.Body)}</pre>" : model.HtmlBody,
            AccountGuidStamp = model.AccountGuid,
            ConversationId = model.ConversationId,
            InReplyToMessageId = model.InReplyToMessageId
        };

        await _emailService.SendAsync(message, cancellationToken);
        return RedirectToAction("Index", "Accounts");
    }

    private bool IsBccVisible()
    {
        return bool.TryParse(_configuration["Email:UI:ShowBccField"], out var show) && show;
    }

    private async Task<string> BuildThreadTextAsync(string conversationId, CancellationToken cancellationToken)
    {
        var messages = await _threadQuery.GetMessagesByConversationAsync(conversationId, cancellationToken);
        var builder = new StringBuilder();

        foreach (var message in messages)
        {
            var content = await _threadStore.GetContentAsync(message.MessageId, cancellationToken)
                         ?? await _provider.FetchContentAsync(message.MessageId, cancellationToken);

            if (content is null)
            {
                continue;
            }

            var body = !string.IsNullOrWhiteSpace(content.TextBody)
                ? content.TextBody
                : StripHtml(content.HtmlBody);
            body = SanitizeQuotedBody(body);

            builder.AppendLine($"Subject: {message.Subject}");
            builder.AppendLine($"Received: {message.ReceivedAt:u}");
            builder.AppendLine($"From/To: {string.Join(", ", message.Participants.Select(p => p.Address))}");
            builder.AppendLine(body);
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }


    private static readonly Regex StampTokenRegex = new(@"\[\[CRM-ACCOUNT:[0-9a-fA-F-]{36}\]\]", RegexOptions.Compiled);

    private static string SanitizeQuotedBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        return StampTokenRegex.Replace(body, string.Empty).Trim();
    }

    private static string ToHtmlWithLineBreaks(string plainText)
    {
        var encoded = System.Net.WebUtility.HtmlEncode(plainText ?? string.Empty);
        return encoded.Replace("\r\n", "\n").Replace("\n", "<br />");
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
    }
}
