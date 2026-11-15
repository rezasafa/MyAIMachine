using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyAIMachine.Data;
using MyAIMachine.Models;
using MyAIMachine.Services;
namespace MyAIMachine.Controllers;
[ApiController, Route("api/chat")]
public class ChatController(AppDbContext db, IModelService model, BrowserIdentity identity) : ControllerBase
{
    private string Owner => identity.GetOwner(HttpContext);
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Conversations.AsNoTracking()
        .Where(x => x.OwnerId == Owner).OrderByDescending(x => x.UpdatedAt).Take(100)
        .Select(x => new { x.Id, x.Title, x.UpdatedAt }).ToListAsync(ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Messages(Guid id, CancellationToken ct)
    {
        if (!await db.Conversations.AnyAsync(x => x.Id == id && x.OwnerId == Owner, ct)) return NotFound();
        return Ok(await db.ChatMessages.AsNoTracking().Where(x => x.ConversationId == id).OrderBy(x => x.Id)
            .Select(x => new { x.Role, x.Content, x.Similarity }).ToListAsync(ct));
    }
    [HttpPost]
    public async Task<IActionResult> Send(SendMessageRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest(new { detail = "پیام خالی است." });
        Conversation? conversation;
        if (request.ConversationId.HasValue)
        {
            conversation = await db.Conversations.SingleOrDefaultAsync(x => x.Id == request.ConversationId && x.OwnerId == Owner, ct);
            if (conversation is null) return NotFound();
        }
        else
        {
            conversation = new Conversation { OwnerId = Owner, Title = request.Message.Trim()[..Math.Min(request.Message.Trim().Length, 80)] };
            db.Conversations.Add(conversation);
        }
        var reply = await model.ReplyAsync(request.Message, ct);
        conversation.UpdatedAt = DateTime.UtcNow;
        conversation.Messages.Add(new ChatMessage { Role = "user", Content = request.Message.Trim() });
        conversation.Messages.Add(new ChatMessage { Role = "assistant", Content = reply.Text, Similarity = reply.Similarity });
        db.AppLogs.Add(new AppLog { Event = "ChatReply", Detail = $"Matched: {reply.Matched}; Similarity: {reply.Similarity:F3}" });
        await db.SaveChangesAsync(ct);
        return Ok(new { conversationId = conversation.Id, reply.Text, reply.Similarity, reply.Matched });
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var conversation = await db.Conversations.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == Owner, ct);
        if (conversation is null) return NotFound();
        db.Conversations.Remove(conversation);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
