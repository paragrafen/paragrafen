namespace Paragrafen.Core.Contracts;

// Will add or change fields later, when we know more
public record AskResponse(
    Guid RequestId,
    Guid ConversationId,
    AnswerStatus Status,
    string Answer,
    List<Source> Sources,
    DateTime AskedAt
);
