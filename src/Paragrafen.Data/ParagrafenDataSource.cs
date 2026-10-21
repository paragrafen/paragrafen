using Azure.Core;
using Npgsql;
using Pgvector.Npgsql;

namespace Paragrafen.Data;

public static class ParagrafenDataSource
{
    private static readonly TokenRequestContext PostgresScope =
        new(["https://ossrdbms-aad.database.windows.net/.default"]);

    // Locally the connection string has the Docker password. In Azure it has none, so the managed identity's Entra token is used.
    public static NpgsqlDataSource Create(string connectionString, TokenCredential credential)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseVector();
        if (string.IsNullOrEmpty(builder.ConnectionStringBuilder.Password))
        {
            builder.UsePasswordProvider(
                _ => credential.GetToken(PostgresScope, CancellationToken.None).Token,
                async (_, cancellationToken) => (await credential.GetTokenAsync(PostgresScope, cancellationToken)).Token);
        }
        return builder.Build();
    }
}
