namespace Paragrafen.Core.Contracts;

public record AskRequest(
    string Question,
    Guid ConversationId
);

