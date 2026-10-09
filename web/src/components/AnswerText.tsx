import { splitCitations } from "@/lib/citations";
import type { Source } from "@/types";

interface AnswerTextProps {
  answer: string;
  sources: Source[];
}

export function AnswerText({ answer, sources }: AnswerTextProps) {
  return (
    <p className="leading-relaxed">
      {splitCitations(answer).map((part, i) => {
        if (part.kind === "text") return part.text;

        const label = `[${part.number}]`;
        const source = sources[part.number - 1];
        if (!source) return label;

        return (
          <a
            key={i}
            href={source.url}
            target="_blank"
            rel="noreferrer"
            className="font-medium text-primary hover:text-primary-hover"
          >
            {label}
          </a>
        );
      })}
    </p>
  );
}
