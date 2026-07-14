# RAG intelligence for multi-course questions

Research date: 2026-07-14

Scope: the next production increment beyond `advanced-rag-comparison-citation-research.md`: reliable questions about two or more courses, assessment weights/grades, and qualified judgments such as “môn nào dễ hơn?”.

## Executive recommendation

Do not make the chatbot “smarter” by increasing `top-k` or by letting Gemini improvise a verdict. Use three bounded layers:

1. **Intent/slot plan:** identify courses, requested dimensions, whether the question asks for facts, arithmetic, comparison, or a subjective recommendation.
2. **Evidence plane:** retrieve prose with hybrid search, but query exact course facts from a structured projection created at ingestion. Every value keeps document/chunk provenance.
3. **Deterministic synthesis:** calculate totals/differences/rankings in application code; ask Gemini only to explain the verified result in natural Vietnamese. Return partial/unknown cells rather than filling gaps.

This preserves the repo's existing authorization-aware RAG while making questions such as “điểm hai môn như nào?”, “khác nhau ở đâu?”, and “môn nào dễ hơn với em?” answerable without turning relevance scores into facts.

## 1. Query planning: cover every course and dimension

The existing service rewrites up to four queries, takes the maximum embedding similarity, and later enforces course-level coverage. This is useful, but course-only coverage is too coarse: a DBA103 assessment chunk and an IOT102 overview chunk technically cover both courses while leaving `IOT102.assessment` unanswered.

Create a bounded plan whose unit of coverage is an **evidence slot**:

```json
{
  "intent": "compare",
  "entities": ["DBA103", "IOT102"],
  "dimensions": ["assessment", "workload"],
  "slots": [
    {"entity":"DBA103", "dimension":"assessment", "query":"DBA103 assessment grading weights"},
    {"entity":"IOT102", "dimension":"assessment", "query":"IOT102 assessment grading weights"},
    {"entity":"DBA103", "dimension":"workload", "query":"DBA103 workload assignments labs"},
    {"entity":"IOT102", "dimension":"workload", "query":"IOT102 workload assignments labs"}
  ]
}
```

Rules:

- Resolve course codes deterministically from authorized subject metadata before asking Gemini to expand wording.
- Run independent slots in parallel and retain qualified evidence per slot before global reranking.
- Cap the Cartesian product (for example, eight slots). If the user requests more, prioritize explicit dimensions and say what was not covered.
- Use a follow-up retrieval hop only when a later query depends on an earlier answer. Ordinary A-vs-B comparisons are parallel, not agentic.
- Keep slot provenance on each candidate so generation knows *why* a chunk was selected.

This section intentionally does not repeat the decomposition literature already summarized in the earlier research note; it narrows the implementation boundary to `(course, dimension)` coverage.

## 2. Structured course facts: the reliable path for grades and weights

Syllabi often encode assessment in tables. Flattening those tables can detach values from headers. Google's Gemini Layout Parser explicitly preserves table structure, ancestor headings, and contextual chunks for RAG; it also supports table annotations. [Google Document AI — Gemini Layout Parser](https://docs.cloud.google.com/document-ai/docs/layout-parse-chunk)

At ingestion, produce both normal chunks and a validated fact projection:

```json
{
  "courseCode": "PRN222",
  "factType": "assessment_component",
  "name": "Final exam",
  "weightPercent": 30,
  "maximumScore": null,
  "minimumScore": null,
  "term": null,
  "documentId": "...",
  "chunkIndex": 12,
  "evidenceText": "Final exam | 30%",
  "schemaVersion": 1
}
```

Gemini Structured Outputs can constrain extraction to a JSON Schema, but Google warns that syntactically valid JSON does **not** guarantee semantically correct values; application validation remains mandatory. [Gemini API — Structured outputs](https://ai.google.dev/gemini-api/docs/structured-output)

Validation and computation should therefore be deterministic:

- Parse percentages/numbers with invariant rules, normalize decimal separators, and reject values outside the domain.
- Preserve the table header and row label in `evidenceText`; never store a bare `30%` fact.
- Detect duplicates and conflicts by `(course, factType, name, term)`. Do not silently choose the newest-looking value unless document version policy proves it is authoritative.
- Validate assessment totals against the syllabus rule (often 100%, but keep this configurable). A failed total marks the extracted set invalid, not “approximately correct.”
- Compute sums, differences, averages, sorting, and threshold checks in C#, not in generated prose. Table-QA research shows numerical reasoning needs explicit evidence extraction and symbolic operations; TAT-QA covers addition, subtraction, comparison, sorting, and composed operations over table-plus-text evidence. [TAT-QA, ACL 2021](https://aclanthology.org/2021.acl-long.254/)
- Keep raw chunks as the audit source and use the fact projection as a read model, not as a replacement for source documents.

Interpret “điểm môn học” explicitly:

- If the user asks **cơ cấu điểm**, return syllabus assessment components and weights.
- If the user asks **điểm của tôi**, use a separately authorized grade source; never infer personal grades from course documents.
- If the wording is ambiguous and both interpretations are plausible, ask one short clarification rather than exposing or fabricating personal data.

## 3. Retrieval and reranking

For `gemini-embedding-2`, Google recommends asymmetric retrieval formatting: `task: search result | query: ...` for the query and `title: ... | text: ...` for documents, used consistently across indexing and search. [Gemini API — Embeddings](https://ai.google.dev/gemini-api/docs/embeddings)

Use two first-stage lists per slot:

- **Lexical:** exact course codes, assessment labels, percentages, CLO codes, lecturer names, and Vietnamese/English terminology.
- **Dense Gemini:** paraphrases and conceptual questions such as workload, skills, similarity, or learning outcomes.

Do not combine raw lexical and cosine scores with fixed arithmetic unless they are calibrated on the corpus: their ranges are not comparable. Fuse their *ranks* with Reciprocal Rank Fusion, then rerank the fused pool. Microsoft's official RRF description uses `sum(1 / (rank + k))` and explains why rank fusion normalizes multiple search systems. [Azure AI Search — hybrid RRF scoring](https://learn.microsoft.com/en-us/azure/search/hybrid-search-ranking)

Rerank approximately 20–40 fused candidates per slot or comparison, then enforce the slot quota again. Google's production RAG guidance distinguishes a low-latency Ranking API (document-query relevance) from a higher-latency Gemini LLM reranker, reported at roughly 1–2 seconds; this supports a fast ranker by default and an LLM reranker only for difficult comparisons. [Vertex AI RAG Engine — reranking](https://cloud.google.com/vertex-ai/generative-ai/docs/retrieval-and-ranking)

Practical policy for this repo:

- Keep exact structured facts even when their embedding score is mediocre.
- Use reranking for relevance, not truth. A reranker score cannot justify a factual claim.
- Deduplicate after attaching all matching slot IDs; do not lose coverage provenance.
- Tune candidate depth, thresholds, RRF weights, and final context size from a golden set, separately for exact-number and semantic questions.

## 4. “Môn nào dễ hơn?” must be a qualified judgment

“Difficulty” is not a directly retrievable fact unless an authorized source explicitly defines or measures it. The chatbot should classify the evidence it has:

| Evidence available | Allowed answer |
|---|---|
| Only syllabus structure | “Theo cấu trúc đánh giá, A có nhiều bài thực hành hơn; chưa đủ căn cứ kết luận dễ hơn.” |
| Workload/prerequisites/outcomes for both courses | Compare observable demands, then give a conditional fit statement. |
| Valid aggregate grade distributions for comparable cohorts | Report descriptive statistics with cohort/term/sample context; do not claim causation. |
| Student preferences/background supplied in chat | Give a personalized *fit* recommendation, clearly separated from objective course facts. |
| Missing or conflicting evidence | Return partial evidence or ask for the missing preference; never force a winner. |

Recommended response contract:

```json
{
  "verdict": "conditional|tie|insufficient_evidence",
  "objectiveFactors": [
    {"factor":"lab_workload", "course":"PRN222", "value":"higher", "sourceIds":["s3"]}
  ],
  "personalFit": "Nếu bạn mạnh thực hành .NET, PRN222 có thể hợp hơn.",
  "unknowns": ["No comparable pass-rate data"],
  "confidenceLabel": "evidence_complete|evidence_partial"
}
```

The server, not the model, sets `evidence_complete` only when required slots for both courses pass validation. Confidence labels describe **evidence sufficiency**, never subjective certainty or retrieval similarity. Selective-QA research finds that model probabilities alone are poorly calibrated under domain shift and evaluates systems by the accuracy/coverage trade-off when abstaining. [Kamath, Jia, and Liang — Selective Question Answering under Domain Shift](https://arxiv.org/abs/2006.09462)

For every factual sentence, validate it only against its cited source. Google's grounding API treats a partially supported compound sentence as ungrounded and exposes per-claim citations/support scores, reinforcing atomic claims rather than a whole-answer “looks grounded” check. [Google Agent Search — Check grounding with RAG](https://docs.cloud.google.com/generative-ai-app-builder/docs/check-grounding)

## 5. Evaluation: prove versatility, not just fluency

Build a Vietnamese evaluation set from real authorized documents. Each example should define question type, required slots, expected values/operations, allowed source chunks, and expected answer status. Include paraphrases and follow-up turns.

Required suites:

1. **Exact facts:** credits, assessment weights, attendance threshold, lecturer; score value exact match and source-cell match.
2. **Multi-course coverage:** 2–4 courses × 1–3 dimensions; score slot recall before generation and answer-slot completeness after generation.
3. **Numerical reasoning:** totals, differences, highest weight, equal values, invalid totals, decimal/percent formats.
4. **Qualified difficulty:** complete, partial, missing, and conflicting evidence; assert that an unconditional winner is forbidden without the required evidence policy.
5. **Conversational follow-up:** pronouns and ellipsis such as “còn môn kia?”, while preserving authorization and resolved entities.
6. **Unanswerable/adversarial:** nonexistent course, missing assessment row, prompt injection in a chunk, unauthorized subject, conflicting document versions.
7. **Robustness:** reorder table rows/columns and rephrase headings without changing the expected answer. FREB-TQA identifies table-structure invariance, relevant-cell grounding, and numerical robustness as separate desiderata. [FREB-TQA, NAACL 2024](https://aclanthology.org/2024.naacl-long.137/)

Release gates should include:

- authorization leakage = 0;
- exact numeric answer accuracy = 1.00 on deterministic facts;
- expected source-cell accuracy = 1.00 on deterministic facts;
- evidence-slot recall and answer-slot completeness reported separately;
- unsupported unconditional difficulty verdicts = 0;
- missing/conflicting evidence correctly yields `partial`/`insufficient`;
- p50/p95 latency and Gemini calls split by simple vs. decomposed questions;
- head-to-head regression against the current pipeline, with paired per-example results.

LLM-judge scores are supplementary. Google recommends evaluating a judge against human ratings and provides confusion matrices/balanced metrics for that calibration; do not treat an uncalibrated Gemini judge as ground truth. [Vertex AI — evaluate a judge model](https://docs.cloud.google.com/vertex-ai/generative-ai/docs/models/evaluate-judge-model)

Curate production failures into versioned datasets rather than tuning from anecdotes. Gemini API logs can be saved/exported as evaluation datasets and rerun through Batch API, subject to the documented logging limitations and retention window. [Gemini API — Logs and datasets](https://ai.google.dev/gemini-api/docs/logs-datasets)

## 6. Implementation order for this repository

1. Add `CourseFact`, `AssessmentComponent`, provenance, conflict status, and schema version; extract and validate them during document indexing.
2. Add deterministic question classification for course entities, `assessment_structure` vs. `personal_grade`, comparison dimensions, and difficulty intent.
3. Change retrieval from course-only coverage to `(course, dimension)` slot coverage; run lexical and Gemini lists separately, fuse by rank, then rerank.
4. Add a deterministic comparison engine for numeric facts and an evidence-policy engine for difficulty verdicts.
5. Generate structured claims from verified facts/chunks; server-render the answer and citations, with `partial`/`insufficient` statuses.
6. Add the golden suites and gates above before tuning thresholds or enabling a more expensive reranker.

## Non-goals and trade-offs

- **No open-web answer blending by default:** course facts should come from authorized institutional documents or grade systems. Web search can explain general concepts, but must be labeled and kept out of course-specific verdicts.
- **No full autonomous agent:** bounded plans are cheaper, easier to test, and sufficient for ordinary comparisons. Escalate only dependent multi-hop requests.
- **Structured extraction adds storage and reindex work:** it is justified for repeated exact/numeric questions because correctness and provenance are testable.
- **Managed Google ranking/layout services add cost and data-governance review:** the architecture does not require them. Their documented behavior is a design reference; local extraction/reranking can implement the same boundaries.
- **Personal grades are a separate security domain:** connect them only with explicit authorization, minimal disclosure, and audit logging.

## Source quality note

Sources are first-party product documentation and original research papers. Product preview features are evidence for design patterns, not a requirement to adopt a particular cloud service. Recommendations are repository-specific inferences from those sources and the current `RagChatService` behavior.
