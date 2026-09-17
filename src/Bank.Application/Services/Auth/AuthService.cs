using Bank.Application.Interfaces;
using Bank.Application.Interfaces.Security;
using Bank.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bank.Application.Services;

/// <summary>
/// Auth service using ASP.NET Core Identity.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ITokenService _tokenService;

    public AuthService(UserManager<User> userManager, SignInManager<User> signInManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email)
            ?? throw new Exception("Invalid credentials.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
                throw new Exception("Account is temporarily locked due to too many failed attempts. Try again later.");
            throw new Exception("Invalid credentials.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        return await _tokenService.GenerateAccessTokenAsync(user, roles);
    }

    public async Task<User> RegisterAsync(string username, string email, string password)
    {
        var user = new User
        {
            UserName = username,
            Email = email,
            FirstName = username,
            LastName = string.Empty
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, "User");
        return user;
    }

    public Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return Task.FromResult<IEnumerable<User>>(_userManager.Users.ToList());
    }
}
