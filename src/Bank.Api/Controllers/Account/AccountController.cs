using Bank.Api.Constants;
using Bank.Api.Helpers;
using Bank.Application.Commands.Account;
using Bank.Application.Queries.Account;
using Bank.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Controllers.Account;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountController(IMediator mediator) => _mediator = mediator;

    /// <summary>GET /api/account — List all accounts for the logged-in user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyAccounts()
    {
        var userId = this.GetCurrentUserIdRequired();
        var accounts = await _mediator.Send(new GetUserAccountsQuery(userId));
        return this.CreateSuccessResponse("Accounts retrieved successfully", accounts);
    }

    /// <summary>GET /api/account/{id} — Get a specific account by ID.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAccountById(Guid id)
    {
        var account = await _mediator.Send(new GetAccountByIdQuery(id));
        if (account is null) return this.CreateNotFoundResponse(ErrorMessages.AccountNotFound);
        return this.CreateSuccessResponse("Account retrieved successfully", account);
    }

    /// <summary>POST /api/account — Create a new account for the logged-in user.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
    {
        var userId = this.GetCurrentUserIdRequired();
        var account = await _mediator.Send(new CreateAccountCommand(userId, request.AccountNickname));
        return CreatedAtAction(nameof(GetAccountById), new { id = account.Id },
            new { Success = true, Message = "Account created successfully", Data = account });
    }

    /// <summary>PUT /api/account/{id} — Update an account's holder name.</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] UpdateAccountRequest request)
    {
        var userId = this.GetCurrentUserIdRequired();
        // Handler throws KeyNotFoundException (→ 404) or UnauthorizedAccessException (→ 401)
        var account = await _mediator.Send(
            new UpdateAccountCommand(id, request.AccountHolderName ?? string.Empty, userId));
        return this.CreateSuccessResponse("Account updated successfully", account);
    }

    /// <summary>DELETE /api/account/{id} — Soft-delete an account.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAccount(Guid id)
    {
        var userId = this.GetCurrentUserIdRequired();
        // Handler throws KeyNotFoundException (→ 404) or UnauthorizedAccessException (→ 401)
        await _mediator.Send(new DeleteAccountCommand(id, userId));
        return NoContent();
    }
}
