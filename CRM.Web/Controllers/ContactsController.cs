using CRM.Core.Models;
using CRM.Core.Storage;
using CRM.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Web.Controllers;

public sealed class ContactsController : Controller
{
    private readonly IContactStore _contactStore;
    private readonly IAccountStore _accountStore;

    public ContactsController(IContactStore contactStore, IAccountStore accountStore)
    {
        _contactStore = contactStore;
        _accountStore = accountStore;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var contacts = await _contactStore.GetAllAsync(cancellationToken);
        var accountIds = new Dictionary<int, IReadOnlyList<int>>();
        foreach (var contact in contacts)
        {
            accountIds[contact.Id] = await _contactStore.GetAccountIdsForContactAsync(contact.Id, cancellationToken);
        }

        var model = new ContactListViewModel
        {
            Contacts = contacts,
            ContactAccountIds = accountIds,
            Accounts = await _accountStore.GetAllAsync(cancellationToken)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(new ContactEditViewModel { Accounts = await _accountStore.GetAllAsync(cancellationToken) });
    }

    [HttpPost]
    public async Task<IActionResult> Create(ContactEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Accounts = await _accountStore.GetAllAsync(cancellationToken);
            return View(model);
        }

        await _contactStore.AddAsync(new Contact { Name = model.Name, EmailAddress = model.EmailAddress }, model.SelectedAccountIds, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var contact = await _contactStore.GetByIdAsync(id, cancellationToken);
        if (contact is null)
        {
            return NotFound();
        }

        return View(new ContactEditViewModel
        {
            Id = contact.Id,
            Name = contact.Name,
            EmailAddress = contact.EmailAddress,
            SelectedAccountIds = (await _contactStore.GetAccountIdsForContactAsync(id, cancellationToken)).ToList(),
            Accounts = await _accountStore.GetAllAsync(cancellationToken)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(ContactEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Accounts = await _accountStore.GetAllAsync(cancellationToken);
            return View(model);
        }

        await _contactStore.UpdateAsync(new Contact { Id = model.Id, Name = model.Name, EmailAddress = model.EmailAddress }, model.SelectedAccountIds, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _contactStore.DeleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
