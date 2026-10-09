export type AnswerStatus = "answered" | "not_found" | "out_of_scope";

// These types are temporary until we have developed backend more
export interface AskRequest {
  question: string;
  conversation_id?: string;
}

// Refers to the legal source
export interface Source {
  chunk_id: string;
  law: string;
  section: string;
  url: string;
}

export interface AskResponse {
  request_id: string;
  conversation_id: string;
  status: AnswerStatus;
  answer: string;
  sources: Source[];
}
