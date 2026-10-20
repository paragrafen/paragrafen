import type { ErrorResponse } from "@/types";

export function errorResponse(status: number, message: string, cause?: unknown) {
  const requestId = crypto.randomUUID();
  console.error(`Proxy request ${requestId} failed: ${message}`, cause ?? "");
  return Response.json(
    {
      request_id: requestId,
      error: { code: "internal_error", message },
    } satisfies ErrorResponse,
    { status },
  );
}

export async function callApi(req: Request, path: string[]) {
  const apiUrl = process.env.API_URL;
  if (!apiUrl) {
    return errorResponse(500, "API_URL is not set");
  }

  const target = `${apiUrl}/${path.map(encodeURIComponent).join("/")}`;
  const { search } = new URL(req.url);
  const hasBody = req.method !== "GET";

  let res: Response;
  try {
    res = await fetch(target + search, {
      method: req.method,
      headers: hasBody ? { "content-type": "application/json" } : undefined,
      body: hasBody ? await req.text() : undefined,
      signal: req.signal,
    });
  } catch (error) {
    if (req.signal.aborted) {
      return new Response(null, { status: 499 });
    }
    return errorResponse(502, "The API could not be reached", error);
  }

  const contentType = res.headers.get("content-type");

  return new Response(res.body, {
    status: res.status,
    headers: contentType ? { "content-type": contentType } : undefined,
  });
}
