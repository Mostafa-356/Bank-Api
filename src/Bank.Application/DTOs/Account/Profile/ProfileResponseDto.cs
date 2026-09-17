namespace Bank.Application.DTOs.Account.Profile;

public record ProfileResponse(Guid Id, string UserName, string Email, string FirstName, string LastName);

