import type { NextRequest } from "next/server";
import { callApi, usesMockApi } from "@/lib/api";
import type { AskResponse, CallStats, ResultsResponse } from "@/types";

function mockAnswer() {
  return Response.json({
    request_id: "mock",
    conversation_id: "mock",
    status: "answered",
    answer: "Dette er et mock-svar. [1]",
    sources: [
      {
        chunk_id: "LOV-2005-06-17-62:§15-6",
        law: "Arbeidsmiljøloven",
        description: "Prøvetid",
        section: "§ 15-6",
        url: "https://lovdata.no/",
      },
    ],
    asked_at: new Date().toISOString(),
  } satisfies AskResponse);
}

function mockResults() {
  const stats = {
    calls: 42,
    answered: 30,
    not_found: 8,
    out_of_scope: 1,
    errors: 3,
    error_rate: 3 / 42,
    cost_nok_total: 1.26,
    cost_nok_median: 0.027,
    cost_nok_p95: 0.081,
    latency_ms_p50: 3400,
    latency_ms_p95: 8200,
    llm_latency_ms_p50: 3100,
    input_tokens: 84000,
    cached_tokens: 12000,
    output_tokens: 21000,
    reasoning_tokens: 9000,
  } satisfies CallStats;
  const now = new Date().toISOString();
  return Response.json({
    generated_at: now,
    total: stats,
    days: [
      { date: "2026-10-21", stats },
      { date: "2026-10-20", stats },
    ],
    recent_calls: [
      {
        request_id: "mock-1",
        created_at: now,
        status: "answered",
        failure_reason: null,
        model: "gpt-6-luna-2026-09-22",
        attempts: 1,
        input_tokens: 2100,
        output_tokens: 480,
        cost_nok: 0.027,
        latency_ms: 3400,
      },
      {
        request_id: "mock-2",
        created_at: now,
        status: "error",
        failure_reason: "model_unavailable",
        model: null,
        attempts: 1,
        input_tokens: 0,
        output_tokens: 0,
        cost_nok: 0,
        latency_ms: 30000,
      },
    ],
  } satisfies ResultsResponse);
}

async function proxy(
  req: NextRequest,
  { params }: RouteContext<"/api/[...path]">,
) {
  const { path } = await params;

  if (usesMockApi()) {
    const route = `${req.method} ${path.join("/")}`;
    if (route === "POST ask") return mockAnswer();
    if (route === "GET results") return mockResults();
  }

  return callApi(req, path);
}

export { proxy as GET, proxy as POST };
