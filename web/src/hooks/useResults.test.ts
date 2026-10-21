import { afterEach, describe, expect, it, vi } from "vitest";
import { fetchResults } from "./useResults";

function stubFetch(response: Response) {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(response));
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("fetchResults", () => {
  it("returns the results", async () => {
    stubFetch(Response.json({ total: { calls: 3 } }));

    const result = await fetchResults(new AbortController().signal);

    expect(result).toEqual({ data: { total: { calls: 3 } } });
  });

  it("asks for a new login when the session has expired", async () => {
    stubFetch(new Response(null, { status: 401 }));

    expect(await fetchResults(new AbortController().signal)).toEqual({
      login: true,
    });
  });

  it("reports forbidden for a non-admin, whose 403 has no body", async () => {
    stubFetch(new Response(null, { status: 403 }));

    expect(await fetchResults(new AbortController().signal)).toEqual({
      error: { code: "forbidden" },
    });
  });

  it("passes on the API's error code and request id", async () => {
    stubFetch(
      Response.json(
        {
          request_id: "0199f0c4-0000-7000-8000-000000000000",
          error: { code: "model_unavailable", message: "Down" },
        },
        { status: 502 },
      ),
    );

    expect(await fetchResults(new AbortController().signal)).toEqual({
      error: {
        code: "model_unavailable",
        requestId: "0199f0c4-0000-7000-8000-000000000000",
      },
    });
  });
});
