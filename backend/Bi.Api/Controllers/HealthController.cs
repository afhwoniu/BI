using Bi.Api.Models;
using Bi.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bi.Api.Controllers;

/// <summary>
/// 健康检查控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly BiDbContext _db;

    public HealthController(BiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 健康检查
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<HealthInfo>>> Check()
    {
        var dbConnected = false;
        try
        {
            dbConnected = await _db.Database.CanConnectAsync();
        }
        catch { }

        return Ok(ApiResponse<HealthInfo>.Success(new HealthInfo
        {
            Status = dbConnected ? "Healthy" : "Degraded",
            Database = dbConnected ? "Connected" : "Disconnected",
            Version = "1.0.0",
            Timestamp = DateTime.UtcNow
        }));
    }
}

public class HealthInfo
{
    public string Status { get; set; } = "Healthy";
    public string Database { get; set; } = "Unknown";
    public string Version { get; set; } = "1.0.0";
    public DateTime Timestamp { get; set; }
}

