import { useEffect, useState } from "react";
import { type ApiError, readError } from "@/lib/errors";
import type { ResultsResponse } from "@/types";

type ResultsState =
  | { data: ResultsResponse }
  | { error: ApiError }
  | { login: true };

export async function fetchResults(signal: AbortSignal): Promise<ResultsState> {
  const res = await fetch("/api/results", { signal });
  if (res.status === 401) return { login: true };
  if (res.status === 403) return { error: { code: "forbidden" } };
  if (!res.ok) return { error: await readError(res) };
  return { data: await res.json() };
}

// One state value, so a later success always replaces an earlier error.
// With cacheComponents, a hidden page keeps its state and fetches again when shown.
export function useResults() {
  const [state, setState] = useState<ResultsState | null>(null);

  useEffect(() => {
    const controller = new AbortController();

    fetchResults(controller.signal)
      .then((next) => {
        if ("login" in next) {
          window.location.assign("/login");
          return;
        }
        setState(next);
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setState({ error: { code: "internal_error" } });
        }
      });

    return () => controller.abort();
  }, []);

  return {
    data: state && "data" in state ? state.data : null,
    error: state && "error" in state ? state.error : null,
  };
}
