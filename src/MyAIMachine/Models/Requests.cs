using System.ComponentModel.DataAnnotations;
namespace MyAIMachine.Models;
public record SendMessageRequest(Guid? ConversationId, [Required, StringLength(4000, MinimumLength = 1)] string Message);
public record TeachRequest([Required, StringLength(4000)] string Input, [Required, StringLength(8000)] string Response);
public record SettingsRequest([Range(.05, 1)] float MinimumSimilarity, [Required, StringLength(2000)] string FallbackResponse);

public record ImportExamplesRequest([Required, MinLength(1), MaxLength(500)] TeachRequest[] Examples);
public record PreviewRequest([Required, StringLength(4000)] string Input);
