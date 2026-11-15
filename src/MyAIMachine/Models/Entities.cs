namespace MyAIMachine.Models;

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public string Title { get; set; } = "گفتگوی جدید";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; set; } = [];
}
public class ChatMessage
{
    public long Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public float? Similarity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class TrainingExample
{
    public long Id { get; set; }
    public string Input { get; set; } = "";
    public string Response { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class ModelSettings
{
    public int Id { get; set; } = 1;
    public float MinimumSimilarity { get; set; } = .45f;
    public string FallbackResponse { get; set; } = "هنوز پاسخ این پرسش را یاد نگرفته‌ام. یک نمونه در بخش آموزش اضافه کنید و مدل را آموزش دهید.";
}
public class ModelVersion
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int ExampleCount { get; set; }
    public byte[] Artifact { get; set; } = [];
    public string ExamplesJson { get; set; } = "[]";
}
public class AppLog
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "Information";
    public string Event { get; set; } = "";
    public string Detail { get; set; } = "";
}
