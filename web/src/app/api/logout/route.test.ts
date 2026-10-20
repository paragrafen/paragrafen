import { expect, it } from "vitest";
import { POST } from "./route";

it("deletes the session cookie and sends the browser to /login", async () => {
  const res = await POST();

  expect(res.status).toBe(303);
  expect(res.headers.get("location")).toBe("/login");
  const setCookie = res.headers.get("set-cookie") ?? "";
  expect(setCookie).toMatch(/^session=;/);
  expect(setCookie).toContain("Path=/");
  expect(setCookie).toContain("Expires=Thu, 01 Jan 1970");
});
