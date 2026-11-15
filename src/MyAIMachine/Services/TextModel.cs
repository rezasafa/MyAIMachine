using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ML;
using Microsoft.ML.Data;
namespace MyAIMachine.Services;

public record Example(string Input, string Response);
public record ModelReply(string Text, float Similarity, bool Matched);
public sealed class TextRow { public string Text { get; set; } = ""; }
public sealed class FeatureRow { [ColumnName("Features")] public float[] Features { get; set; } = []; }

// Immutable snapshot; no shared PredictionEngine (which is not thread safe).
public sealed class TextModel
{
    private readonly MLContext ml = new(seed: 42);
    private readonly ITransformer transformer;
    private readonly Example[] examples;
    private readonly float[][] vectors;
    public static string Normalize(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormKC)
        .Replace('ي', 'ی').Replace('ك', 'ک').Replace('\u200c', ' ').Trim().ToLowerInvariant(), @"\s+", " ");
    private TextModel(ITransformer transformer, Example[] examples)
    {
        this.transformer = transformer;
        this.examples = examples;
        vectors = Features(examples.Select(x => x.Input));
    }
    private float[][] Features(IEnumerable<string> texts)
    {
        var data = ml.Data.LoadFromEnumerable(texts.Select(x => new TextRow { Text = Normalize(x) }));
        return ml.Data.CreateEnumerable<FeatureRow>(transformer.Transform(data), false).Select(x => x.Features).ToArray();
    }
    public static (TextModel Model, byte[] Artifact) Train(Example[] examples)
    {
        if (examples.Length == 0) throw new ArgumentException("حداقل یک نمونه آموزشی لازم است.");
        var ml = new MLContext(seed: 42);
        var data = ml.Data.LoadFromEnumerable(examples.Select(x => new TextRow { Text = Normalize(x.Input) }));
        var transformer = ml.Transforms.Text.FeaturizeText("Features", nameof(TextRow.Text)).Fit(data);
        using var stream = new MemoryStream();
        ml.Model.Save(transformer, data.Schema, stream);
        return (new TextModel(transformer, examples), stream.ToArray());
    }
    public static TextModel Load(byte[] artifact, Example[] examples)
    {
        using var stream = new MemoryStream(artifact);
        return new TextModel(new MLContext().Model.Load(stream, out _), examples);
    }
    public ModelReply Reply(string input, float threshold, string fallback)
    {
        var normalized = Normalize(input);
        var exact = Array.FindIndex(examples, x => Normalize(x.Input) == normalized);
        if (exact >= 0) return new(examples[exact].Response, 1, true);
        var vector = Features([input])[0];
        var best = -1; var score = 0f;
        for (var i = 0; i < vectors.Length; i++)
        {
            var similarity = Cosine(vector, vectors[i]);
            if (similarity > score) { score = similarity; best = i; }
        }
        return best >= 0 && score >= threshold ? new(examples[best].Response, score, true) : new(fallback, score, false);
    }
    private static float Cosine(float[] a, float[] b)
    {
        double dot = 0, aa = 0, bb = 0;
        for (var i = 0; i < a.Length; i++) { dot += a[i] * b[i]; aa += a[i] * a[i]; bb += b[i] * b[i]; }
        return aa == 0 || bb == 0 ? 0 : (float)Math.Clamp(dot / Math.Sqrt(aa * bb), 0, 1);
    }
}
