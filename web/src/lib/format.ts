const LOCALE = "nb-NO";
const EMPTY = "–";

export function formatNok(value: number | null) {
  if (value === null) return EMPTY;
  const options: Intl.NumberFormatOptions =
    Math.abs(value) >= 1
      ? { minimumFractionDigits: 2, maximumFractionDigits: 2 }
      : { maximumSignificantDigits: 2 };
  return `${value.toLocaleString(LOCALE, options)} kr`;
}

export function formatPercent(value: number) {
  return value.toLocaleString(LOCALE, {
    style: "percent",
    maximumFractionDigits: 1,
  });
}

export function formatMs(value: number | null) {
  if (value === null) return EMPTY;
  if (value < 1000) return `${value} ms`;
  return `${(value / 1000).toLocaleString(LOCALE, { maximumFractionDigits: 1 })} s`;
}

export function formatCount(value: number) {
  return value.toLocaleString(LOCALE);
}

export function formatDate(value: string) {
  const [year, month, day] = value.split("-");
  return `${day}.${month}.${year}`;
}

export function formatTime(value: string) {
  return new Date(value).toLocaleString(LOCALE, {
    timeZone: "Europe/Oslo",
    dateStyle: "short",
    timeStyle: "medium",
  });
}
