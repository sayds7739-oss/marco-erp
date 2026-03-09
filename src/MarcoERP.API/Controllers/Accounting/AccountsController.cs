using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Accounting
{
    /// <summary>
    /// API controller for Chart of Accounts management.
    /// </summary>
    public class AccountsController : ApiControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountsController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets all accounts (flat list).</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var result = await _accountService.GetAllAsync(ct);
            return FromResult(result);
        }

        /// <summary>Gets an account by ID.</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var result = await _accountService.GetByIdAsync(id, ct);
            return FromResult(result);
        }

        /// <summary>Gets an account by its code.</summary>
        [HttpGet("code/{code}")]
        public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
        {
            var result = await _accountService.GetByCodeAsync(code, ct);
            return FromResult(result);
        }

        /// <summary>Gets the Chart of Accounts as a hierarchical tree.</summary>
        [HttpGet("tree")]
        public async Task<IActionResult> GetTree(CancellationToken ct)
        {
            var result = await _accountService.GetAccountTreeAsync(ct);
            return FromResult(result);
        }

        /// <summary>Gets all accounts by type.</summary>
        [HttpGet("types/{type}")]
        public async Task<IActionResult> GetByType(AccountType type, CancellationToken ct)
        {
            var result = await _accountService.GetByTypeAsync(type, ct);
            return FromResult(result);
        }

        /// <summary>Gets all postable (leaf, active) accounts.</summary>
        [HttpGet("postable")]
        public async Task<IActionResult> GetPostable(CancellationToken ct)
        {
            var result = await _accountService.GetPostableAccountsAsync(ct);
            return FromResult(result);
        }

        /// <summary>Gets all children of a parent account.</summary>
        [HttpGet("{id:int}/children")]
        public async Task<IActionResult> GetChildren(int id, CancellationToken ct)
        {
            var result = await _accountService.GetChildrenAsync(id, ct);
            return FromResult(result);
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>Creates a new account.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAccountDto dto, CancellationToken ct)
        {
            var result = await _accountService.CreateAsync(dto, ct);
            return FromResult(result);
        }

        /// <summary>Updates an existing account.</summary>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateAccountDto dto, CancellationToken ct)
        {
            var result = await _accountService.UpdateAsync(dto, ct);
            return FromResult(result);
        }

        /// <summary>Activates a deactivated account.</summary>
        [HttpPatch("{id:int}/activate")]
        public async Task<IActionResult> Activate(int id, CancellationToken ct)
        {
            var result = await _accountService.ActivateAsync(id, ct);
            return FromResult(result);
        }

        /// <summary>Deactivates an account.</summary>
        [HttpPatch("{id:int}/deactivate")]
        public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
        {
            var result = await _accountService.DeactivateAsync(id, ct);
            return FromResult(result);
        }

        /// <summary>Soft-deletes an account.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var result = await _accountService.DeleteAsync(id, ct);
            return FromResult(result);
        }
    }
}
