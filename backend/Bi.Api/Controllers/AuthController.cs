using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bi.Api.Models;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Bi.Api.Controllers;

/// <summary>
/// 认证控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly BiDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(BiDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        var user = await _db.SysUsers.FirstOrDefaultAsync(u => u.Username == request.Username && u.IsEnabled);
        if (user == null)
            return Ok(ApiResponse<LoginResponse>.Fail("用户名或密码错误"));

        if (!VerifyPassword(request.Password, user.PasswordHash))
            return Ok(ApiResponse<LoginResponse>.Fail("用户名或密码错误"));

        // 更新最后登录时间
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = GenerateJwtToken(user);
        return Ok(ApiResponse<LoginResponse>.Success(new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            RealName = user.RealName,
            Avatar = user.Avatar
        }));
    }

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    [HttpGet("userinfo")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserInfoResponse>>> GetUserInfo()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Ok(ApiResponse<UserInfoResponse>.Fail("无效的用户信息", 401));

        var user = await _db.SysUsers.FindAsync(userId);
        if (user == null)
            return Ok(ApiResponse<UserInfoResponse>.Fail("用户不存在", 404));

        return Ok(ApiResponse<UserInfoResponse>.Success(new UserInfoResponse
        {
            UserId = user.Id,
            Username = user.Username,
            RealName = user.RealName,
            Email = user.Email,
            Phone = user.Phone,
            Avatar = user.Avatar
        }));
    }

    private string GenerateJwtToken(SysUser user)
    {
        var jwtSection = _config.GetSection("Jwt");
        var secret = jwtSection["Secret"] ?? "DefaultSecretKey123456789012345678901234";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(double.Parse(jwtSection["ExpireMinutes"] ?? "1440"));
        var issuer = jwtSection["Issuer"] ?? "BiPlatform";
        var audience = jwtSection["Audience"] ?? "BiPlatformClient";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("realName", user.RealName ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? RealName { get; set; }
    public string? Avatar { get; set; }
}

public class UserInfoResponse
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? RealName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Avatar { get; set; }
}
