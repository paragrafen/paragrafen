const API_URL = process.env.API_URL;
const USE_MOCK = !API_URL && process.env.NODE_ENV !== "production";

export async function POST(
  req: Request,
  { params }: RouteContext<"/api/[...path]">,
) {
  const { path } = await params;

  if (USE_MOCK) {
    return Response.json({
      request_id: "mock",
      conversation_id: "mock",
      status: "answered",
      answer: "Dette er et mock-svar. [1]",
      sources: [
        {
          chunk_id: "LOV-2005-06-17-62:§15-6",
          law: "Arbeidsmiljøloven",
          section: "§ 15-6",
          url: "https://lovdata.no/",
        },
      ],
    });
  }

  if (!API_URL) {
    return Response.json(
      { error: { code: "internal_error", message: "API_URL is not set" } },
      { status: 500 },
    );
  }

  const res = await fetch(`${API_URL}/${path.join("/")}`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: await req.text(),
  });

  return new Response(res.body, {
    status: res.status,
    headers: { "content-type": "application/json" },
  });
}
