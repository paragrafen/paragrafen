import { describe, expect, it } from "vitest";
import { isAdmin } from "./session";

const ROLE = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

function token(claims: object) {
  const payload = Buffer.from(JSON.stringify(claims)).toString("base64url");
  return `header.${payload}.signature`;
}

describe("isAdmin", () => {
  it("is true when Admin is one of several roles", () => {
    expect(isAdmin(token({ [ROLE]: ["User", "Admin"] }))).toBe(true);
  });

  it("is true for a single Admin role", () => {
    expect(isAdmin(token({ [ROLE]: "Admin" }))).toBe(true);
  });

  it("is false for a normal user", () => {
    expect(isAdmin(token({ [ROLE]: "User" }))).toBe(false);
  });

  it("is false without a role claim", () => {
    expect(isAdmin(token({ sub: "1" }))).toBe(false);
  });

  it.each([undefined, "", "mock", "a.%%%.c", "a.bnVsbA.c"])(
    "is false for %j",
    (value) => {
      expect(isAdmin(value)).toBe(false);
    },
  );
});
