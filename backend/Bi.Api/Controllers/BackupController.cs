using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bi.Application.Services;

namespace Bi.Api.Controllers;

/// <summary>
/// 备份还原控制器
/// </summary>
[ApiController]
[Route("api/v1/backup")]
[Authorize]
public class BackupController : ControllerBase
{
    private readonly IBackupService _backupService;

    public BackupController(IBackupService backupService)
    {
        _backupService = backupService;
    }

    /// <summary>
    /// 导出所有配置
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var data = await _backupService.ExportAsync();
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = $"bi_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";

        return File(bytes, "application/json", fileName);
    }

    /// <summary>
    /// 导出配置（返回JSON数据）
    /// </summary>
    [HttpGet("export/json")]
    public async Task<IActionResult> ExportJson()
    {
        var data = await _backupService.ExportAsync();
        return Ok(new { code = 0, message = "success", data });
    }

    /// <summary>
    /// 导入配置
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] BackupData data, [FromQuery] bool overwrite = false)
    {
        if (data == null)
            return Ok(new { code = 1, message = "无效的备份数据" });

        var result = await _backupService.ImportAsync(data, overwrite);

        return Ok(new
        {
            code = result.Success ? 0 : 1,
            message = result.Message,
            data = new
            {
                result.DatasourcesImported,
                result.DatasetsImported,
                result.ChartsImported,
                result.PanelsImported,
                result.MenusImported,
                result.KpiCategoriesImported,
                result.KpiDefinitionsImported
            }
        });
    }

    /// <summary>
    /// 从文件导入配置
    /// </summary>
    [HttpPost("import/file")]
    public async Task<IActionResult> ImportFile(IFormFile file, [FromQuery] bool overwrite = false)
    {
        if (file == null || file.Length == 0)
            return Ok(new { code = 1, message = "请上传备份文件" });

        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var json = await reader.ReadToEndAsync();
            var data = JsonSerializer.Deserialize<BackupData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data == null)
                return Ok(new { code = 1, message = "无效的备份文件格式" });

            var result = await _backupService.ImportAsync(data, overwrite);

            return Ok(new
            {
                code = result.Success ? 0 : 1,
                message = result.Message,
                data = new
                {
                    result.DatasourcesImported,
                    result.DatasetsImported,
                    result.ChartsImported,
                    result.PanelsImported,
                    result.MenusImported,
                    result.KpiCategoriesImported,
                    result.KpiDefinitionsImported
                }
            });
        }
        catch (JsonException)
        {
            return Ok(new { code = 1, message = "备份文件JSON格式错误" });
        }
    }
}

