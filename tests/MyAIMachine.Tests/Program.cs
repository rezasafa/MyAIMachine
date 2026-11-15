using Microsoft.EntityFrameworkCore;
using MyAIMachine.Data;
using MyAIMachine.Services;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    passed++; Console.WriteLine("PASS: " + name);
}
Check(TextModel.Normalize("  سلام   دنيا كودك  ") == "سلام دنیا کودک", "Persian normalization");
try { TextModel.Train([]); throw new Exception("Empty dataset accepted"); }
catch (ArgumentException) { Check(true, "Empty dataset rejected"); }
var single = TextModel.Train([new("سلام", "سلام دوست من")]);
Check(single.Model.Reply("سلام", .8f, "unknown").Text == "سلام دوست من", "Learn from one example");
Check(!single.Model.Reply("quantum spaceship xyz", .8f, "unknown").Matched, "Unknown text rejected");
var dataset = new Example[] { new("سلام دوست من", "سلام!"), new("ساعت کاری فروشگاه", "از ۹ تا ۱۸"), new("آدرس فروشگاه کجاست", "تهران") };
var trained = TextModel.Train(dataset);
Check(trained.Model.Reply("سلام دوست من", .45f, "unknown").Text == "سلام!", "Exact known answer");
Check(trained.Model.Reply("ساعت کاری فروشگاه چیست", .1f, "unknown").Text == "از ۹ تا ۱۸", "ML similarity retrieves related answer");
var loaded = TextModel.Load(trained.Artifact, dataset);
var before = trained.Model.Reply("ساعت کاری", .1f, "unknown");
var after = loaded.Reply("ساعت کاری", .1f, "unknown");
Check(before.Text == after.Text && Math.Abs(before.Similarity - after.Similarity) < .0001, "Model persistence roundtrip");
Check(!loaded.Reply("", .45f, "unknown").Matched, "Zero feature vector fallback");
var parallel = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => loaded.Reply("ساعت کاری", .1f, "unknown"))));
Check(parallel.All(x => x.Text == after.Text), "Concurrent prediction consistency");
using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=localhost;Database=MyAIMachine;Integrated Security=true").Options);
Check(!db.Database.HasPendingModelChanges(), "Migration snapshot matches model");
Console.WriteLine($"All {passed} checks passed.");
