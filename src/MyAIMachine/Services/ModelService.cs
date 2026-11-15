using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyAIMachine.Data;
using MyAIMachine.Models;
namespace MyAIMachine.Services;

public interface IModelService
{
    Task<ModelReply> ReplyAsync(string input, CancellationToken ct);
    Task<long> TrainAsync(CancellationToken ct);
}
public sealed class ModelService(IDbContextFactory<AppDbContext> factory) : IModelService
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private (long Id, TextModel Model)? current;
    public async Task<ModelReply> ReplyAsync(string input, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var settings = await db.ModelSettings.AsNoTracking().SingleAsync(ct);
        await gate.WaitAsync(ct);
        TextModel? model;
        try
        {
            var latest = await db.ModelVersions.OrderByDescending(x => x.Id).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
            if (latest.HasValue && current?.Id != latest.Value)
            {
                var version = await db.ModelVersions.AsNoTracking().SingleAsync(x => x.Id == latest, ct);
                current = (version.Id, TextModel.Load(version.Artifact, JsonSerializer.Deserialize<Example[]>(version.ExamplesJson)!));
            }
            model = current?.Model;
        }
        finally { gate.Release(); }
        return model?.Reply(input, settings.MinimumSimilarity, settings.FallbackResponse) ?? new(settings.FallbackResponse, 0, false);
    }
    public async Task<long> TrainAsync(CancellationToken ct)
    {
        if (!await gate.WaitAsync(0, ct)) throw new InvalidOperationException("مدل مشغول است؛ چند لحظه دیگر تلاش کنید.");
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var rows = await db.TrainingExamples.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
            if (rows.Count == 0) throw new InvalidOperationException("ابتدا یک نمونه آموزشی اضافه کنید.");
            var examples = rows.GroupBy(x => TextModel.Normalize(x.Input)).Select(g => g.Last()).Select(x => new Example(x.Input, x.Response)).ToArray();
            var trained = await Task.Run(() => TextModel.Train(examples), ct);
            var version = new ModelVersion { Artifact = trained.Artifact, ExamplesJson = JsonSerializer.Serialize(examples), ExampleCount = examples.Length };
            db.ModelVersions.Add(version);
            db.AppLogs.Add(new AppLog { Event = "ModelTrained", Detail = $"Examples: {examples.Length}" });
            await db.SaveChangesAsync(ct); // artifact and its dataset are committed together
            current = (version.Id, trained.Model);
            return version.Id;
        }
        finally { gate.Release(); }
    }
}
