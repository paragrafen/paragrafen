using System.Text.Json.Serialization;

namespace Paragrafen.Core.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<AnswerStatus>))]
public enum AnswerStatus
{
    [JsonStringEnumMemberName("answered")] Answered,
    [JsonStringEnumMemberName("not_found")] NotFound,
    [JsonStringEnumMemberName("out_of_scope")] OutOfScope
}
