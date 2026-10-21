import { useEffect, useState } from "react";
import { type ApiError, readError } from "@/lib/errors";
import type { ResultsResponse } from "@/types";

export function useResults() {
  const [data, setData] = useState<ResultsResponse | null>(null);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    const controller = new AbortController();

    async function load() {
      try {
        const res = await fetch("/api/results", { signal: controller.signal });
        if (res.status === 401) {
          window.location.assign("/login");
          return;
        }
        if (res.status === 403) {
          setError({ code: "forbidden" });
          return;
        }
        if (!res.ok) {
          setError(await readError(res));
          return;
        }
        setData(await res.json());
      } catch {
        if (!controller.signal.aborted) setError({ code: "internal_error" });
      }
    }

    load();
    return () => controller.abort();
  }, []);

  return { data, error };
}
