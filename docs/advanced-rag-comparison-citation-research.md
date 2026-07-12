# Advanced RAG for comparisons, citations, and completeness

Research date: 2026-07-12  
Scope: complex comparison questions, multi-query/multi-hop retrieval, claim-level citations, structured generation, compact citation UX, and completeness evaluation for the PRN222_FINAL chatbot.

## Executive conclusion

The current chatbot already has useful foundations: query rewriting, up to four query embeddings, hybrid lexical/vector scoring, reranking, source-marker normalization, answer-level grounding validation, authorization-aware retrieval, and expandable citation cards. The main remaining weakness for questions such as “compare subject A and subject B” is **coverage**, not merely relevance: `BuildCandidateMatchesAsync` takes the maximum vector score across rewritten queries and then globally truncates candidates, so one entity or one comparison dimension can dominate all selected evidence. `IsAnswerGroundedAsync` can reject unsupported content, but it does not prove that every requested entity and comparison dimension was answered.

The recommended next architecture is a bounded, non-agentic query planner for ordinary comparisons, with an optional iterative path only for genuinely dependent questions:

1. Parse the request into a strict `ComparisonPlan` containing entities, dimensions, and independent/dependent subqueries.
2. Retrieve each subquery independently and retain a minimum evidence quota per entity/dimension before global reranking.
3. Generate a strict structured answer made of atomic claims, each carrying source IDs.
4. Validate every factual claim against only its cited chunks; reject or omit partially supported claims.
5. Run a coverage gate against the plan before rendering.
6. Render inline citation chips and one collapsed “Sources (n)” disclosure; show details only on interaction.

This is more reliable than simply increasing top-k or asking the model to “be comprehensive.”

## 1. Query decomposition for multi-entity comparisons

### What primary sources establish

Microsoft’s RAG architecture guide distinguishes simple questions from complex multipart questions. It defines decomposition as breaking a complex query into smaller, self-contained subqueries, executing those independently, aggregating their results, and answering the original query from that accumulated context. Its examples explicitly include comparison questions and questions about multiple independent entities. It also recommends ordering dependent subquestions from least to most dependent and decontextualizing pronouns and implicit entities. [Microsoft Azure Architecture Center — decomposition](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/rag/rag-information-retrieval#decomposition)

Azure AI Search’s agentic retrieval performs the same higher-level pattern dynamically: query planning creates focused subqueries, executes them in parallel through keyword/vector/hybrid retrieval, semantically reranks each, retains references, and merges the results. Microsoft notes that this improves coverage for complex questions but adds latency relative to a single-query pipeline. [Azure AI Search — agentic retrieval](https://learn.microsoft.com/en-us/azure/search/agentic-retrieval-overview)

Original research also supports separating independent decomposition from dependent multi-hop retrieval. Self-Ask generates explicit follow-up questions for compositional questions, while IRCoT alternates reasoning and retrieval when the next useful query depends on evidence found in a prior step. These patterns justify parallel retrieval for ordinary course comparisons and a bounded iterative loop only for dependent hops. [Press et al. — Self-Ask](https://arxiv.org/abs/2210.03350), [Trivedi et al. — IRCoT](https://arxiv.org/abs/2212.10509), [official IRCoT implementation](https://github.com/stonybrooknlp/ircot)

Microsoft’s retrieval guidance recommends broad first-stage retrieval, result merging, model-based reranking, and final truncation. It warns that larger final context can improve coverage but also increases noise and cost, so top-N must be selected by evaluation rather than intuition. [Microsoft Azure Architecture Center — reranking pipeline](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/rag/rag-information-retrieval#design-a-reranking-pipeline)

### Implication for this repository

`BuildRetrievalQueriesAsync` currently mixes the original question with rewrites. This is query expansion, but it is not a coverage-aware plan. `BuildCandidateMatchesAsync` then calculates `Max` similarity over query embeddings and applies one global `Take(RerankCandidateK)`. For a comparison, this can retrieve excellent evidence for DBA103 and none for PRN222 while still producing a high aggregate ranking.

Introduce a strict planner DTO, for example:

```json
{
  "kind": "comparison",
  "entities": ["DBA103", "PRN222"],
  "dimensions": ["credits", "assessment", "skills"],
  "subqueries": [
    {"id":"q1","entity":"DBA103","dimension":"credits","dependsOn":[]},
    {"id":"q2","entity":"PRN222","dimension":"credits","dependsOn":[]},
    {"id":"q3","entity":"DBA103","dimension":"assessment","dependsOn":[]},
    {"id":"q4","entity":"PRN222","dimension":"assessment","dependsOn":[]}
  ]
}
```

Recommended bounded flow:

- Keep the original query unchanged as the synthesis instruction.
- Detect a comparison only when there are at least two authorized/recognized entities or an explicit comparison intent.
- Generate one self-contained retrieval query per `(entity, dimension)` pair, capped by a configurable limit (for example 6–8).
- Execute independent subqueries concurrently; do not concatenate all subqueries into one routing string.
- Keep at least one qualified evidence chunk per required entity/dimension before global deduplication and reranking.
- Merge duplicate chunks by `(DocumentId, ChunkIndex)`, but retain the set of subquery IDs that each chunk satisfies.
- If a required cell has no evidence above threshold, do not infer it from the other entity. Mark that cell as “Không đủ dữ liệu trong tài liệu” and return `partial_evidence`, or ask a clarification when entity resolution itself is ambiguous.
- Use iterative/multi-hop retrieval only for dependent questions (for example “the lecturer of the course that has more credits”). Resolve hop 1, validate the entity, then issue hop 2. A fixed comparison should not pay the cost or nondeterminism of a full agent loop.

Avoid using the current `SplitQuestionBatch` path for semantic comparisons: answering each clause independently and concatenating results does not synthesize similarities/differences and can renumber citations incorrectly after `MergeCitations`.

## 2. Claim-level citation correctness

### What primary sources establish

Google’s Check Grounding API treats a sentence as a claim, returns claim spans, cited chunks, and optional claim-level support scores. Google’s definition is strict: perfect grounding requires every claim to be wholly entailed; a partially correct compound claim is ungrounded. It also recommends smaller facts with metadata instead of one very large fact, and permits a citation threshold that trades citation count for stronger support. [Google Cloud — Check grounding with RAG](https://docs.cloud.google.com/generative-ai-app-builder/docs/check-grounding?hl=en)

The ALCE benchmark evaluates answers with citations along fluency, correctness, and citation quality. Its citation evaluation separates **citation correctness** (does the cited passage entail the claim?) from **citation completeness/recall** (are claims that need support actually cited?). The paper’s central generation requirement is that factual statements cite one or a few supporting passages. [Gao et al., EMNLP 2023 — ALCE paper](https://arxiv.org/abs/2305.14627), [official ALCE implementation](https://github.com/princeton-nlp/ALCE)

A later attribution study distinguishes citation correctness from citation faithfulness: a passage can support a statement even if the model did not actually rely on it, a post-rationalization failure. This reinforces generating claims from selected evidence and carrying source IDs through generation, rather than attaching plausible citations afterward. [Wallat et al. — Correctness is not Faithfulness in RAG Attributions](https://arxiv.org/abs/2412.18004)

RefChecker independently finds that fine-grained claim checking is more effective than coarse response-level checking and represents claims as atomic claim triplets before checking them against references. This supports the proposed atomic-claim boundary rather than relying only on the repo’s current whole-answer decision. [Hu et al. — RefChecker](https://arxiv.org/abs/2405.14486), [official Amazon Science implementation](https://github.com/amazon-science/RefChecker)

### Implication for this repository

`HasValidSourceMarkers` verifies marker syntax/range and `ValidateGroundingAsync` evaluates the answer against the whole retrieved context. That can allow a sentence marked `[1]` to be supported only by chunk `[2]`, because answer-level validation does not enforce the claim-to-citation edge.

Recommended claim contract:

```json
{
  "claims": [
    {
      "id": "c1",
      "text": "DBA103 có 3 tín chỉ.",
      "sourceIds": ["s2"],
      "entity": "DBA103",
      "dimension": "credits"
    }
  ],
  "unknowns": []
}
```

Validation must operate per claim:

- Split compound factual sentences into atomic claims before validation. “A has 3 credits and B has 4 credits” is two claims, even if rendered in one row.
- Validate each claim against **only** the cited source chunks, not the entire context.
- A cited claim passes only on full entailment. Partial support fails the whole atomic claim.
- Require at least one citation for every externally verifiable factual claim; allow uncited discourse such as “Tóm lại” or “Dưới đây là so sánh.”
- Remove failed claims or regenerate once from the validated evidence; never silently keep the sentence and remove only its marker.
- Compute citation precision = supported cited claims / cited claims, and citation recall = supported claims with citations / all support-requiring claims.
- Store stable source IDs in the structured result. Convert them to display ordinals only after deduplication. This avoids the current risk of changing citation identity when merged citations are reordered.

Do not show the internal retrieval score as if it were factual confidence. `Score` is relevance, not entailment probability. If retained in developer diagnostics, label it “retrieval relevance”; omit it from the normal student UI.

## 3. Structured answer schema

OpenAI’s Structured Outputs uses JSON Schema and strict adherence, and OpenAI recommends `json_schema` over the older JSON object mode for supported models. This is appropriate for separating generation semantics from presentation. [OpenAI API — Structured model outputs](https://developers.openai.com/api/docs/guides/structured-outputs)

Because the repo uses compatible Gemini/Groq-style chat endpoints, schema support must be capability-detected. Where the provider supports a strict response schema, send it natively. Otherwise prompt for JSON, parse defensively, reject unknown/missing fields, and fall back to the existing safe extractive answer. Never treat “valid JSON” alone as proof of grounding.

Recommended provider-neutral response model:

```json
{
  "status": "grounded|partial_evidence|insufficient_evidence|clarification_required",
  "answerType": "direct|comparison|list|explanation",
  "summary": "short grounded synthesis",
  "sections": [
    {
      "heading": "Điểm giống nhau",
      "claims": [{"text":"...", "sourceIds":["s1"]}]
    }
  ],
  "comparison": {
    "columns": ["Tiêu chí", "DBA103", "PRN222"],
    "rows": [
      {
        "dimension": "Số tín chỉ",
        "cells": [
          {"text":"3", "sourceIds":["s1"]},
          {"text":"4", "sourceIds":["s2"]}
        ]
      }
    ]
  },
  "unknowns": [{"entity":"PRN222", "dimension":"assessment", "reason":"no_qualified_evidence"}]
}
```

The server should validate this model, calculate final status itself, and render Markdown/HTML itself. The LLM must not decide authorization, final citation ordinals, or whether a missing evidence cell counts as complete.

## 4. Compact citation UX with progressive disclosure

OpenAI describes deep-research output as a structured report with citations/source links for verification and a separate “sources used” section and activity history for deeper review. This is a useful progressive-disclosure model: evidence is immediately reachable without displaying every excerpt by default. [OpenAI Help Center — Deep research](https://help.openai.com/en/articles/10500283-deep-research-faq), [OpenAI — Introducing deep research](https://openai.com/index/introducing-deep-research/)

Google’s grounding response exposes claim start/end spans connected to cited chunks. This supports placing a compact citation control beside the exact claim it verifies rather than showing only a detached source list. For Vietnamese text, note Google’s warning that returned spans may use UTF-8 byte offsets rather than character offsets; this repo can avoid that rendering hazard by generating explicit claim objects rather than reconstructing spans from a flat string. [Google Cloud — grounding output fields](https://docs.cloud.google.com/generative-ai-app-builder/docs/check-grounding?hl=en#output-data)

Recommended UI for this repo:

- Render small inline chips such as superscript `[1]` immediately after each claim/cell. Make them buttons/links with an accessible label like `Mở nguồn 1: syllabus DBA103`.
- Below the answer, show one collapsed control: `Nguồn (3)` plus stacked miniature file icons/titles. Do not auto-open the first source; current `if (index === 0) item.open = true` defeats compactness.
- Clicking an inline chip opens/focuses the corresponding source detail. Clicking `Nguồn (3)` reveals filename, subject/chapter, excerpt, and document action.
- Deduplicate the source drawer but do not cap away a source that an inline claim references. The current hard cap of five sources can create an unverifiable answer if generation uses more than five.
- Hide chunk index and raw retrieval score by default; place them under an optional “Chi tiết kỹ thuật” disclosure.
- Keep excerpts collapsed and short; highlight the supporting sentence if a verified span is available.
- Use `<button>` or `<a>` for inline chips and native `<details>/<summary>` for the drawer, with visible keyboard focus and `aria-expanded`/descriptive labels.
- On narrow screens, open source detail as a bottom sheet; on desktop, a popover/side panel preserves reading position.

## 5. Evaluation for answer completeness

Microsoft explicitly frames groundedness as a precision property (the answer adds nothing outside context) and response completeness as a recall property (the answer omits none of the critical expected information). Its Foundry evaluator lists retrieval metrics separately from final-answer metrics, so a passing groundedness score cannot prove completeness. [Microsoft Foundry — RAG evaluators](https://learn.microsoft.com/en-us/azure/foundry/concepts/evaluation-evaluators/rag-evaluators)

Microsoft also recommends Precision@K, Recall@K, and MRR for retrieval, with positive and negative test queries. Recall@K is especially important here because comparison failures often begin before generation: one entity’s needed chunk never reaches context. [Microsoft Azure Architecture Center — evaluate search results](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/rag/rag-information-retrieval#evaluate-your-search-results)

RAGAS formalizes separate generation and retrieval dimensions—faithfulness, answer relevance, and context relevance—and validates them against human judgments. It is useful as an offline signal, but it should complement deterministic domain checks rather than replace them. [Es et al., EACL 2024 — RAGAS](https://aclanthology.org/2024.eacl-demo.16/)

RAGChecker decomposes evaluation further into retriever and generator diagnostics at claim level, including claim recall, context precision, context utilization, noise sensitivity, hallucination, and faithfulness. Its official implementation is useful as an offline reference when diagnosing whether a failure belongs to retrieval or generation. [Ru et al. — RAGChecker](https://arxiv.org/abs/2408.08067), [official Amazon Science implementation](https://github.com/amazon-science/RAGChecker)

### Proposed evaluation dataset and gates

Create a Vietnamese golden set whose expected answer is expressed as atomic facts and comparison cells, not just one reference paragraph:

```json
{
  "question": "So sánh DBA103 và PRN222 về tín chỉ và hình thức đánh giá",
  "requiredSlots": [
    "DBA103.credits", "PRN222.credits",
    "DBA103.assessment", "PRN222.assessment"
  ],
  "expectedEvidence": {
    "DBA103.credits": ["document-id:chunk-id"],
    "PRN222.credits": ["document-id:chunk-id"]
  }
}
```

Measure at least:

- **Subquery coverage:** required planned subqueries with at least one qualified result / required subqueries.
- **Evidence-slot recall:** required entity-dimension slots whose expected evidence was retrieved / required slots.
- **Recall@K per subquery**, not only on the merged list.
- **Claim groundedness precision:** fully supported generated claims / factual generated claims.
- **Citation correctness:** cited claim-source edges that entail the claim / all cited edges.
- **Citation completeness:** support-requiring claims with at least one correct citation / all support-requiring claims.
- **Answer slot completeness:** required slots correctly answered / required slots.
- **Unknown honesty:** missing-evidence slots explicitly marked unknown / missing-evidence slots.
- **Authorization leakage:** unauthorized chunks in retrieval, generation context, citations, or excerpts; release threshold must be zero.
- **Latency and provider-fallback rate:** report simple and decomposed questions separately.

Suggested release gates for the curated corpus:

- Authorization leakage = 0.
- Unsupported factual claims = 0 in deterministic exact-fact tests; >= 0.98 claim groundedness precision in broader LLM-judged evals with manual review of failures.
- Citation correctness >= 0.98 and citation completeness >= 0.95.
- Required-slot completeness >= 0.95 overall and 1.00 for critical syllabus facts.
- Negative/unanswerable cases must not fabricate; expected status is `insufficient_evidence` or `partial_evidence`.
- Compare the new pipeline head-to-head against the current baseline and reject improvements that meet quality only by violating the agreed latency budget.

LLM judges should return their intermediate claim/slot decisions for audit. Sample failures must be reviewed by a human because model-based scores are neither ground truth nor stable across provider/model changes.

## 6. Concrete implementation order

1. Add `QueryPlan`, `RetrievalSubquery`, `EvidenceSlot`, `AnswerClaim`, and structured comparison response models in BLL; preserve existing public `ChatAnswer` fields for backward compatibility.
2. Replace free-form `RewriteQueriesAsync` output for complex questions with a strict planner result. Keep the current simple path for direct questions.
3. Change candidate construction to retain results and provenance per subquery; enforce per-slot evidence before merged reranking.
4. Change generation to structured claims/source IDs, then perform claim-to-cited-chunk validation and coverage validation.
5. Derive `grounded_answer`, `partial_evidence`, or `insufficient_evidence` on the server.
6. Render comparison tables from structured data and implement inline citation chips plus a collapsed source drawer.
7. Extend `verify-chatbot.ps1` with a golden comparison corpus, per-subquery retrieval metrics, claim citation metrics, completeness gates, authorization tests, and simple-vs-complex latency reporting.

## 7. Risks and trade-offs

- **Latency/cost:** decomposition adds embedding, retrieval, and reranking work. Bound the planner, parallelize independent subqueries, cache identical embeddings, and use the simple path by default.
- **Planner error:** an LLM can omit an entity or invent a dimension. Validate entities against authorized subject metadata and reconcile the plan against deterministic entities/dimensions extracted from the original question.
- **Noise from excess queries:** more recall can reduce precision. Use per-subquery thresholds, deduplication, reranking, and final evidence budgets.
- **Provider variance:** native structured schema support differs. Put strict parsing and validation in the provider adapter and retain extractive fallback.
- **Citation overload:** inline markers can clutter prose. Atomic claim chips plus a single collapsed drawer offer verification without permanently occupying vertical space.
- **Backward compatibility:** historical messages store flat content plus flat citations. Continue rendering those through the legacy path; use the structured model for new messages and optionally persist a schema version.

## Source quality note

This note prioritizes first-party product documentation and original peer-reviewed/preprint work. Product features marked preview by their owners should be treated as design evidence, not as a requirement to adopt their hosted service. Recommendations above are inferences tailored to the inspected repository and are labeled as such.
