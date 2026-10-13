export type AnswerPart =
  { kind: "text"; text: string } | { kind: "citation"; number: number };

const CITATION = /(\[\d+\])/;

export function splitCitations(answer: string): AnswerPart[] {
  return answer
    .split(CITATION)
    .filter((part) => part !== "")
    .map((part): AnswerPart => {
      const match = part.match(/^\[(\d+)\]$/);
      return match
        ? { kind: "citation", number: Number(match[1]) }
        : { kind: "text", text: part };
    });
}
