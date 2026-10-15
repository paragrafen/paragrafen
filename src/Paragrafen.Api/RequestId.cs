namespace Paragrafen.Api;

public static class RequestId
{
    private const string Key = "RequestId";

    public static Guid GetRequestId(this HttpContext context)
    {
        if (context.Items[Key] is Guid id)
            return id;

        id = Guid.CreateVersion7();
        context.Items[Key] = id;
        return id;
    }
}
