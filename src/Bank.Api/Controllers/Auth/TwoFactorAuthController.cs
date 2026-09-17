using System.Security.Claims;
using Bank.Application.DTOs;
using Bank.Application.Interfaces;
using Bank.Application.Interfaces.Security;
using Bank.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Controllers.Auth;

[ApiController]
[Route("api/auth/2fa")]
public class TwoFactorAuthController : ControllerBase
{
    private readonly ITwoFactorAuthService _twoFactorService;
    private readonly ICurrentUser _currentUser;

    public TwoFactorAuthController(ITwoFactorAuthService twoFactorService, ICurrentUser currentUser)
    {
        _twoFactorService = twoFactorService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get 2FA status for the current user
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetStatus()
    {
        if (_currentUser.UserId == null) return Unauthorized();

        var status = await _twoFactorService.GetTwoFactorStatusAsync(_currentUser.UserId.Value);
        return Ok(status);
    }

    /// <summary>
    /// Setup authenticator app for 2FA
    /// </summary>
    [HttpPost("setup/authenticator")]
    [Authorize]
    public async Task<IActionResult> SetupAuthenticator()
    {
        if (_currentUser.UserId == null) return Unauthorized();

        var result = await _twoFactorService.SetupAuthenticatorAsync(_currentUser.UserId.Value);
        
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new
        {
            secretKey = result.SecretKey,
            qrCodeUrl = result.QrCodeUrl,
            message = result.Message
        });
    }

    /// <summary>
    /// Complete 2FA setup by verifying authenticator token
    /// </summary>
    [HttpPost("setup/complete")]
    [Authorize]
    public async Task<IActionResult> CompleteSetup([FromBody] CompleteSetupRequest request)
    {
        if (_currentUser.UserId == null) return Unauthorized();

        var result = await _twoFactorService.CompleteSetupAsync(_currentUser.UserId.Value, request.VerificationToken);
        
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new
        {
            message = result.Message,
            backupCodes = result.BackupCodes
        });
    }

    /// <summary>
    /// Generate and send 2FA token
    /// </summary>
    [HttpPost("generate")]
    [Authorize]
    public async Task<IActionResult> GenerateToken([FromBody] GenerateTokenRequest request)
    {
        if (_currentUser.UserId == null) return Unauthorized();

        var result = await _twoFactorService.GenerateTokenAsync(_currentUser.UserId.Value, request.Method, request.Destination);
        
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new
        {
            message = result.Message,
            expiresAt = result.ExpiresAt
        });
    }

    /// <summary>
    /// Verify 2FA token
    /// </summary>
    [HttpPost("verify")]
    [Authorize]
    public async Task<IActionResult> VerifyToken([FromBody] VerifyTokenRequest request, [FromHeader(Name = "User-Agent")] string? userAgent)
    {
        if (_currentUser.UserId == null) return Unauthorized();

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _twoFactorService.VerifyTokenAsync(_currentUser.UserId.Value, request.Token, ipAddress, userAgent);
        
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        // Add 2FA verified claim to the current session
        var identity = (ClaimsIdentity)HttpContext.User.Identity!;
        identity.AddClaim(new Claim("2fa_verified", "true"));
        identity.AddClaim(new Claim("2fa_verified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));

        return Ok(new { message = result.Message });
    }

    /// <summary>
    /// Verify backup code
    /// </summary>
    [HttpPost("verify-backup")]
    [Authorize]
    public async Task<IActionResult> VerifyBackupCode([FromBody] VerifyBackupCodeRequest request)
    {
        if (_currentUser.UserId == null) return Unauthorized();

        var isValid = await _twoFactorService.VerifyBackupCodeAsync(_currentUser.UserId.Value, request.BackupCode);
        
        if (!isValid)
        {
            return BadRequest(new { message = "Invalid backup code" });
        }

        // Add 2FA verified claim to the current session
        var identity = (ClaimsIdentity)HttpContext.User.Identity!;
        identity.AddClaim(new Claim("2fa_verified", "true"));
        identity.AddClaim(new Claim("2fa_verified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));

        return Ok(new { message = "Backup code verified successfully" });
    }

    /// <summary>
    /// Generate new backup codes
    /// </summary>
    [HttpPost("backup-codes/regenerate")]
    [Authorize]
    public async Task<IActionResult> RegenerateBackupCodes()
    {
        if (_currentUser.UserId == null) return Unauthorized();

        // Verify 2FA is enabled
        var isEnabled = await _twoFactorService.IsTwoFactorEnabledAsync(_currentUser.UserId.Value);
        if (!isEnabled)
        {
            return BadRequest(new { message = "Two-factor authentication is not enabled" });
        }

        var backupCodes = await _twoFactorService.GenerateBackupCodesAsync(_currentUser.UserId.Value);
        
        return Ok(new { backupCodes });
    }

    /// <summary>
    /// Disable 2FA
    /// </summary>
    [HttpPost("disable")]
    [Authorize]
    public async Task<IActionResult> DisableTwoFactor()
    {
        if (_currentUser.UserId == null) return Unauthorized();

        var success = await _twoFactorService.DisableTwoFactorAsync(_currentUser.UserId.Value);
        
        if (!success)
        {
            return BadRequest(new { message = "Failed to disable two-factor authentication" });
        }

        return Ok(new { message = "Two-factor authentication disabled successfully" });
    }
}
