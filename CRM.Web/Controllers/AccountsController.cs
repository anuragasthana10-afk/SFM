using CRM.Core.Models;
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

    [HttpGet]
    public IActionResult Create()
    {
        return View(new AccountEditViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(AccountEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

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
        if (account is null)
        {
            return NotFound();
        }

        return View(new AccountEditViewModel
        {
            Id = account.Id,
            Name = account.Name,
            AccountGuid = account.AccountGuid
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(AccountEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _accountStore.UpdateAsync(new Account
        {
            Id = model.Id,
            Name = model.Name,
            AccountGuid = model.AccountGuid
        }, cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _accountStore.DeleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
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
