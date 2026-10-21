import type { ErrorResponse } from "@/types";

export interface ApiError {
  code: string;
  requestId?: string;
}

export async function readError(res: Response): Promise<ApiError> {
  const body: Partial<ErrorResponse> | null = await res
    .json()
    .catch(() => null);
  return {
    code: body?.error?.code ?? "internal_error",
    requestId: body?.request_id,
  };
}
