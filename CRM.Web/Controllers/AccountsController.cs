using CRM.Core.Storage;
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

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var account = await _accountStore.GetByIdAsync(id, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        var messages = await _threadQuery.GetMessagesByAccountAsync(id, cancellationToken);
        var threads = await _threadQuery.GetThreadsByAccountAsync(id, cancellationToken);

        var model = new AccountEmailsViewModel
        {
            Account = account,
            Messages = messages,
            Threads = threads
        };

        ViewBag.Accounts = await _accountStore.GetAllAsync(cancellationToken);
        return View(model);
    }
}
