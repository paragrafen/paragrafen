using Npgsql;
using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Exceptions;
using Paragrafen.Core.Models;
using Pgvector;

namespace Paragrafen.Data.Search;

public sealed class ChunkSearch(NpgsqlDataSource dataSource) : IChunkSearch
{
    public async Task<IReadOnlyList<ChunkMatch>> FindClosestAsync(
        ReadOnlyMemory<float> embedding, int limit, CancellationToken cancellationToken)
    {
        // <=> is cosine distance: 0 is identical, 2 is opposite. It must match the index's vector_cosine_ops.
        // The nearest chunks are picked on chunks alone, so Postgres can use chunks_embedding_idx; only those are joined.
        // sections.content is the provision text itself; chunks.embedding_text also has the law title and section in front.
        await using var command = dataSource.CreateCommand("""
            WITH nearest AS (
                SELECT id, section_id, embedding <=> $1 AS distance
                FROM chunks
                ORDER BY embedding <=> $1
                LIMIT $2
            )
            SELECT n.id, s.content, d.metadata ->> 'title', s.section_ref, d.metadata ->> 'dokid', n.distance
            FROM nearest n
            JOIN sections s ON s.id = n.section_id
            JOIN documents d ON d.id = s.document_id
            ORDER BY n.distance
            """);

        command.Parameters.Add(new NpgsqlParameter { Value = new Vector(embedding) });
        command.Parameters.Add(new NpgsqlParameter { Value = limit });

        var matches = new List<ChunkMatch>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                matches.Add(new ChunkMatch(
                    ChunkId: reader.GetGuid(0),
                    Text: reader.GetString(1),
                    Law: reader.IsDBNull(2) ? "" : reader.GetString(2),
                    SectionRef: reader.IsDBNull(3) ? null : reader.GetString(3),
                    DokId: reader.GetString(4),
                    Distance: reader.GetDouble(5)));
            }
        }
        catch (NpgsqlException ex) when (ex.IsTransient)
        {
            throw new ServiceUnavailableException(
                "search_unavailable", "The law texts could not be searched right now. Try again shortly.", ex);
        }

        return matches;
    }
}
