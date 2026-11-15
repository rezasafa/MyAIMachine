using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyAIMachine.Data;
using MyAIMachine.Models;
using MyAIMachine.Services;
namespace MyAIMachine.Controllers;
[ApiController, Route("api/training")]
public class TrainingController(AppDbContext db, IModelService model) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct, string? search = null, int page = 1)
    {
        const int pageSize = 20;
        var rows = await db.TrainingExamples.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var version = await db.ModelVersions.OrderByDescending(x => x.Id)
            .Select(x => new { x.Id, x.CreatedAt, x.ExampleCount, x.ExamplesJson }).FirstOrDefaultAsync(ct);
        var dataset = rows.GroupBy(x => TextModel.Normalize(x.Input)).Select(g => g.Last())
            .Select(x => new Example(x.Input, x.Response)).ToArray();
        var active = version is null ? [] : System.Text.Json.JsonSerializer.Deserialize<Example[]>(version.ExamplesJson) ?? [];
        var filtered = rows.Where(x => string.IsNullOrWhiteSpace(search) ||
            TextModel.Normalize(x.Input).Contains(TextModel.Normalize(search)) ||
            TextModel.Normalize(x.Response).Contains(TextModel.Normalize(search))).Reverse().ToList();
        var pages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
        page = Math.Clamp(page, 1, pages);
        return Ok(new {
            examples = filtered.Skip((page - 1) * pageSize).Take(pageSize), total = rows.Count,
            filteredTotal = filtered.Count, page, pages, needsTraining = !dataset.SequenceEqual(active),
            version = version is null ? null : new { version.Id, version.CreatedAt, version.ExampleCount },
            settings = await db.ModelSettings.AsNoTracking().SingleAsync(ct)
        });
    }
    [HttpPut("examples/{id:long}")]
    public async Task<IActionResult> Edit(long id, TeachRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Input) || string.IsNullOrWhiteSpace(request.Response))
            return BadRequest(new { detail = "متن ورودی و پاسخ را وارد کنید." });
        var example = await db.TrainingExamples.FindAsync([id], ct);
        if (example is null) return NotFound();
        example.Input = request.Input.Trim();
        example.Response = request.Response.Trim();
        db.AppLogs.Add(new AppLog { Event = "ExampleEdited", Detail = $"Id: {id}" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpPost("import"), RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Import(ImportExamplesRequest request, CancellationToken ct)
    {
        if (request.Examples.Any(x => x is null || string.IsNullOrWhiteSpace(x.Input) || string.IsNullOrWhiteSpace(x.Response)))
            return BadRequest(new { detail = "تمام نمونه‌ها باید پرسش و پاسخ غیرخالی داشته باشند." });
        db.TrainingExamples.AddRange(request.Examples.Select(x => new TrainingExample { Input = x.Input.Trim(), Response = x.Response.Trim() }));
        db.AppLogs.Add(new AppLog { Event = "ExamplesImported", Detail = $"Count: {request.Examples.Length}" });
        await db.SaveChangesAsync(ct);
        return Ok(new { count = request.Examples.Length });
    }
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct) => Ok(await db.TrainingExamples.AsNoTracking()
        .OrderBy(x => x.Id).Select(x => new { x.Input, x.Response }).ToListAsync(ct));
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(PreviewRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Input)) return BadRequest(new { detail = "پرسش آزمایشی را وارد کنید." });
        return Ok(await model.ReplyAsync(request.Input, ct));
    }
    [HttpPost("examples")]
    public async Task<IActionResult> Add(TeachRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Input) || string.IsNullOrWhiteSpace(request.Response)) return BadRequest(new { detail = "متن ورودی و پاسخ را وارد کنید." });
        db.TrainingExamples.Add(new TrainingExample { Input = request.Input.Trim(), Response = request.Response.Trim() });
        db.AppLogs.Add(new AppLog { Event = "ExampleAdded" });
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "نمونه ذخیره شد؛ اکنون مدل را بازآموزی کنید." });
    }
    [HttpDelete("examples/{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var example = await db.TrainingExamples.FindAsync([id], ct);
        if (example is null) return NotFound();
        db.TrainingExamples.Remove(example);
        db.AppLogs.Add(new AppLog { Event = "ExampleDeleted", Detail = $"Id: {id}; retraining required" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpPost("train")]
    public async Task<IActionResult> Train(CancellationToken ct)
    {
        try { return Ok(new { version = await model.TrainAsync(ct) }); }
        catch (InvalidOperationException e) { return Conflict(new { detail = e.Message }); }
    }
    [HttpPut("settings")]
    public async Task<IActionResult> Settings(SettingsRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FallbackResponse)) return BadRequest(new { detail = "پاسخ جایگزین نمی‌تواند خالی باشد." });
        var settings = await db.ModelSettings.SingleAsync(ct);
        settings.MinimumSimilarity = request.MinimumSimilarity;
        settings.FallbackResponse = request.FallbackResponse.Trim();
        db.AppLogs.Add(new AppLog { Event = "SettingsChanged" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpGet("logs")]
    public async Task<IActionResult> Logs(CancellationToken ct) => Ok(await db.AppLogs.AsNoTracking().OrderByDescending(x => x.Id).Take(100).ToListAsync(ct));
}
