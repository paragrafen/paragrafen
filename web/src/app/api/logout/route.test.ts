import { expect, it } from "vitest";
import { POST } from "./route";

it("deletes the session cookie", async () => {
  const res = await POST();

  expect(res.status).toBe(204);
  const setCookie = res.headers.get("set-cookie") ?? "";
  expect(setCookie).toMatch(/^session=;/);
  expect(setCookie).toContain("Path=/");
  expect(setCookie).toContain("Expires=Thu, 01 Jan 1970");
});
