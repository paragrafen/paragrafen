

export type HealthResponse = { status: string };

export async function getHealth(): Promise<HealthResponse> {
  const res = await fetch(`${API_URL}/api/health`);
  if (!res.ok) throw new Error(`API returned ${res.status}`);
  return res.json();
}
