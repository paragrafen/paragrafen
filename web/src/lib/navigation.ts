const BASE = "http://paragrafen.invalid";

export function safeNext(value: string | null) {
  if (!value?.startsWith("/")) {
    return "/";
  }
  const url = new URL(value, BASE);
  return url.origin === BASE ? url.pathname + url.search + url.hash : "/";
}
