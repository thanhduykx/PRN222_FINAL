# UX benchmark research: Learning, document, AI assistant, and admin analytics

## Scope and method

This note benchmarks the current product category against first-party guidance from Microsoft Power BI, Tableau, Instructure Canvas, Moodle, Google People + AI Research (PAIR), and W3C WAI-ARIA. The goal is not to copy a vendor UI. It is to extract repeatable interaction patterns for an ASP.NET Core Razor Pages application that serves learners, lecturers, and administrators.

The recommendations cover:

- role-based information architecture;
- dashboard and reporting hierarchy;
- navigation, filters, and data tables;
- responsive behavior and accessibility;
- AI assistant onboarding, trust, feedback, and failure states.

## Executive findings

1. **Use a role-specific overview as the landing page, not a universal feature list.** Canvas defines its Dashboard as the default landing page and an overview of Canvas activity; its global navigation persists across pages. Moodle makes administration links context-sensitive: what appears depends on both the user's role and current location. Therefore, the application should preserve a stable global shell while changing the dashboard, shortcuts, metrics, and available actions by role. Sources: [Canvas Basics Guide](https://community.canvaslms.com/html/assets/Canvas_Basics_Guide.pdf), [Moodle Administration block](https://docs.moodle.org/501/en/Settings).

2. **Separate dashboard monitoring from detailed reports.** Microsoft describes a dashboard as a single-page overview for monitoring current state, with tiles that lead to underlying reports for detail. Power BI also recommends one-screen storytelling, prominent KPIs, the most important information in the upper-left reading path, and fewer tiles on phones. Tableau similarly recommends a clear audience and purpose, placing the main view upper-left, and limiting a dashboard to roughly two or three views to preserve clarity and performance. Sources: [Power BI dashboard design tips](https://learn.microsoft.com/en-us/power-bi/create-reports/service-dashboards-design-tips), [Power BI dashboard concepts](https://learn.microsoft.com/en-us/power-bi/create-reports/service-dashboards), [Tableau dashboard best practices](https://help.tableau.com/current/pro/desktop/en-us/dashboards_best_practices.htm).

3. **Treat filters as an explicit, consistent data scope.** Tableau supports controls appropriate to the field and task—single-select list/dropdown and multi-select list/dropdown—and recommends clear filter titles. It also distinguishes local filters from dashboard-wide filters. The product should show filter scope clearly, keep shared filters synchronized across KPI cards/charts/tables, expose active filters as removable chips, and provide a single reset action. Sources: [Tableau dashboard best practices](https://help.tableau.com/current/pro/desktop/en-us/dashboards_best_practices.htm), [Apply filters to multiple worksheets](https://help.tableau.com/current/pro/desktop/en-us/filtering_global.htm), [Accessible dashboard filters](https://help.tableau.com/current/pro/desktop/en-us/accessibility_dashboards.htm).

4. **Use semantic HTML tables until spreadsheet-like interaction is truly required.** W3C distinguishes a static table from an interactive grid. A grid requires managed keyboard focus and arrow/Home/End navigation, while native HTML tables retain simpler document semantics. For current admin lists, use a native `<table>` with caption, scoped headers, sortable button labels and `aria-sort`; only adopt `role="grid"` if cell editing or grid-style selection is implemented completely. Sources: [WAI-ARIA Table Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/table/), [WAI-ARIA Grid Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/grid/), [Grid and table properties](https://www.w3.org/WAI/ARIA/apg/practices/grid-and-table-properties/).

5. **Design responsive states deliberately rather than shrinking desktop.** Tableau warns that automatic resizing can create compressed views or scrollbars and supports device-specific layouts. Power BI recommends fewer tiles on tablets and phones. Moodle's responsive theme moves secondary blocks below primary content on small screens. Therefore, mobile should reorder and simplify content: primary action and status first, filters in a dismissible drawer, cards or essential columns instead of a squeezed wide table, and nonessential analytics deferred to drill-down. Sources: [Tableau dashboard best practices](https://help.tableau.com/current/pro/desktop/en-us/dashboards_best_practices.htm), [Power BI dashboard design tips](https://learn.microsoft.com/en-us/power-bi/create-reports/service-dashboards-design-tips), [Moodle Blocks](https://docs.moodle.org/501/en/Blocks).

6. **An AI assistant needs calibrated expectations, evidence, feedback, and a non-AI fallback.** Google PAIR recommends staged onboarding that explains capabilities and limits, explanations at the level needed for trust, explicit feedback whose effect is understandable, editable/resettable preferences, and a manual fallback when AI fails. This maps directly to a document-grounded chatbot: state that answers are based on indexed course documents, show citations next to claims, distinguish missing evidence from system errors, offer helpful/not-helpful feedback, and preserve document search/browse as a fallback. Sources: [PAIR Mental Models](https://pair.withgoogle.com/guidebook-v2/chapter/mental-models/), [PAIR Explainability + Trust](https://pair.withgoogle.com/guidebook-v2/chapter/explainability-trust/), [PAIR Feedback + Control](https://pair.withgoogle.com/guidebook-v2/chapter/feedback-controls/), [PAIR patterns](https://pair.withgoogle.com/guidebook-v2/patterns).

## Recommended information architecture

Keep the same global shell structure across roles, but only render destinations that a role can use. Do not show disabled destinations that inevitably end at Access Denied.

### Learner

1. **Tổng quan** — current courses, recent documents, unfinished/recent activity, and a clear “Hỏi trợ lý” action.
2. **Môn học** — enrolled course list and course detail.
3. **Kho tài liệu** — search, filter, preview/download, citations.
4. **Trợ lý học tập** — course/document-grounded chat and history.
5. **Tài khoản** — profile/security.

### Lecturer

1. **Tổng quan giảng dạy** — assigned courses, document/indexing health, recent student activity where permitted, and work requiring attention.
2. **Môn học** — only assigned/responsible courses, with course management actions.
3. **Kho tài liệu** — upload, metadata, indexing state, retry/error resolution.
4. **Trợ lý học tập** — visible to lecturers for testing course knowledge, verifying citations, and identifying document gaps; the interface must make the active course/document scope explicit.
5. **Báo cáo môn học** — course-level usage and content coverage, not system-wide finance/admin metrics.
6. **Tài khoản**.

### Administrator

1. **Tổng quan hệ thống** — a small number of operational KPIs and exception queues.
2. **Người dùng** — accounts, roles, activity, deletion eligibility.
3. **Môn học** — ownership/assignment and system governance.
4. **Kho tài liệu** — system-wide inventory and indexing failures.
5. **Báo cáo** — detailed usage, billing, AI consumption, and drill-down tables.
6. **Thiết lập trợ lý** — model/provider configuration and status.
7. **Trợ lý học tập** — optional but useful as an administrator verification tool; keep it visually secondary to governance tasks and never grant document access beyond the admin's authorization policy.
8. **Tài khoản**.

This structure follows the Canvas pattern of persistent global navigation plus dashboard overview, and Moodle's role/context-sensitive settings model. Sources: [Canvas Basics Guide](https://community.canvaslms.com/html/assets/Canvas_Basics_Guide.pdf), [Moodle Administration block](https://docs.moodle.org/501/en/Settings).

## Screen-level design recommendations

### 1. Role landing dashboards

- Use a clear title, role-aware subtitle, “last updated” time, and one primary action.
- First row: at most three or four KPI/status cards. Every card must state its unit, period, comparison or operational meaning; a bare number is insufficient context.
- Second row: two main views maximum on common laptop sizes. Prefer bars/lines over decorative circular charts for comparison and trends; Power BI warns against hard-to-read 3D and overused circular charts.
- Third row: one exception/action queue (failed indexing, unassigned course, user approaching deletion eligibility) rather than another decorative chart.
- Clicking a card or chart opens a filtered report/list with the same scope, following Power BI's dashboard-to-report drill-down model.
- Do not mix time windows in adjacent KPIs without an explicit label. Keep color meaning stable across pages.

Sources: [Power BI dashboard design tips](https://learn.microsoft.com/en-us/power-bi/create-reports/service-dashboards-design-tips), [View and interact with Power BI dashboards](https://learn.microsoft.com/en-us/power-bi/explore-reports/end-user-dashboard-open), [Tableau refine dashboard](https://help.tableau.com/current/pro/desktop/en-us/dashboards_refine.htm).

### 2. Reports and analytics

- Put persistent report scope directly under the page title: date range, course, role, document type/status; show “Áp dụng” only if changes are staged, otherwise update consistently and announce loading.
- Display active filters as chips plus “Xóa tất cả”. Preserve filter state in the query string so URLs are shareable and back/forward navigation works naturally in Razor Pages.
- Use the same filters for every KPI/chart/table on the page unless a component explicitly labels a local scope.
- Keep overview charts to two or three; move additional breakdowns into tabs or drill-down pages.
- Provide empty-state explanations (“Không có dữ liệu trong khoảng thời gian này”) distinct from data-load errors and authorization restrictions.
- Show data freshness near the page title and, for billing/usage, the reporting timezone.

Sources: [Tableau dashboard best practices](https://help.tableau.com/current/pro/desktop/en-us/dashboards_best_practices.htm), [Apply filters to multiple worksheets](https://help.tableau.com/current/pro/desktop/en-us/filtering_global.htm), [Power BI dashboard interaction](https://learn.microsoft.com/en-us/power-bi/explore-reports/end-user-dashboard-open).

### 3. User, course, and document lists

- Use one toolbar in a consistent order: search, primary filters, result count, view/export action, primary create/upload action.
- Use native table markup on desktop. Add a caption or accessible name, `<th scope="col">`, and a visible keyboard focus style. A sortable header is a button with direction in both icon/text and `aria-sort`.
- Keep row actions in a clearly labelled menu when there are more than two. Destructive actions require confirmation that names the affected item and states why the action may be unavailable.
- For deletion eligibility, show the state as structured information: assigned courses count, last active date, eligibility date, and reason. Do not rely on a disabled red button alone.
- On narrow screens, do not force every column into a horizontal squeeze. Use cards or show the identity/status columns and expose the remainder in a row detail disclosure.
- Pagination must retain query/filter state and announce the current result range.

Sources: [WAI-ARIA Table Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/table/), [Grid and table properties](https://www.w3.org/WAI/ARIA/apg/practices/grid-and-table-properties/), [Tableau accessible dashboards](https://help.tableau.com/current/pro/desktop/en-us/accessibility_dashboards.htm).

### 4. Course and document workspace

- Course detail should become a workspace with a stable contextual header (course name/code, lecturer/ownership, status) and a small local navigation: Tổng quan, Tài liệu, Hoạt động/Báo cáo where authorized.
- Upload flow should show accepted types/limits before selection, then separate transfer progress, processing/indexing state, completion, and recoverable error. Users must never interpret “uploaded” as “available to chatbot” if indexing is pending.
- Document rows/cards should show indexing status and the action that resolves the state. Use progressive disclosure for technical error details.
- For learners, replace management controls with preview/download/ask-about-this-document actions; do not merely disable lecturer controls.

### 5. AI assistant

- First-use message: what the assistant can answer, the currently selected course/document scope, what it cannot guarantee, and how sources are used.
- The composer should keep the active scope visible and editable. Offer example prompts that are specific to the selected course rather than generic marketing copy.
- Each answer should group citations adjacent to the supported content; citation items should open the exact document/context when available.
- Distinguish four states: generating, grounded answer, insufficient evidence, and technical failure. “Không tìm thấy căn cứ trong tài liệu đã chọn” is not the same as a generic server error.
- Add explicit helpful/not-helpful controls after answers and acknowledge what the feedback does. Do not imply instant model retraining unless that is true.
- Preserve user control: stop generation, retry, copy, clear/new conversation, change scope, and fall back to document search/browse.
- Avoid presenting the assistant as human-like or all-knowing. Explain user benefit and limits instead of provider/model internals; model selection belongs in admin settings.

Sources: [PAIR Mental Models](https://pair.withgoogle.com/guidebook-v2/chapter/mental-models/), [PAIR Explainability + Trust](https://pair.withgoogle.com/guidebook-v2/chapter/explainability-trust/), [PAIR Feedback + Control](https://pair.withgoogle.com/guidebook-v2/chapter/feedback-controls/).

## Navigation and component rules

- Use a stable top-level destination for each primary object/task: Tổng quan, Môn học, Kho tài liệu, Trợ lý, Báo cáo, Người dùng, Thiết lập. Avoid burying a frequent primary task inside an unrelated dropdown.
- Highlight exactly one current destination and include `aria-current="page"`.
- Use top-level navigation for application areas and contextual tabs/breadcrumbs inside a course or report. Do not mix both levels visually.
- Labels should describe user concepts (“Thiết lập trợ lý”), not implementation concepts (“AI config”).
- Prefer a native `<select>` when choosing one value from a closed list. This is appropriate for model allowlists, course filters, roles, and status filters; free text is appropriate only when arbitrary values are valid.
- Destructive red is reserved for destructive actions/errors. Status colors always include text/icon so color is not the sole signal.
- Every async action has loading, success, empty, and error states; focus moves to or is announced by the resulting status without unexpectedly jumping the user.

## Responsive and accessibility acceptance criteria

1. All primary flows are usable with keyboard alone; focus order follows visual/task order and focus is always visible.
2. Navigation, dialogs, dropdowns, tabs, tables, and status messages use native HTML semantics first; ARIA augments but does not replace them.
3. Charts have a meaningful title/description and an accessible tabular summary or equivalent values; information is not encoded by color alone.
4. Form controls have persistent labels and linked validation messages. Placeholder text is not the only label.
5. The application is usable at 320 CSS pixels and at 200% zoom without two-dimensional page scrolling; wide data may scroll inside a clearly labelled table region.
6. Mobile layouts reorder by task priority: header and main action, status/KPI, primary content, then secondary information. Filters become a button-triggered panel with visible active-count.
7. Touch targets and row actions have sufficient separation, particularly destructive controls.
8. Reduced-motion preferences disable nonessential transitions; loading indicators retain a textual status.
9. Test semantic tables with screen readers before introducing a custom ARIA grid. W3C explicitly cautions that poor ARIA can be less robust than native semantics.

Sources: [WAI-ARIA Table Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/table/), [WAI-ARIA Grid Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/grid/), [Tableau accessible dashboards](https://help.tableau.com/current/pro/desktop/en-us/accessibility_dashboards.htm), [Tableau device layout guidance](https://help.tableau.com/current/pro/desktop/en-us/dashboards_best_practices.htm).

## Prioritized implementation backlog

### P0 — Correct navigation and state integrity

1. Introduce role-specific landing pages and remove inaccessible destinations from each role's navigation.
2. Flatten primary destinations: keep Trợ lý and Thiết lập trợ lý as explicit top-level admin destinations; keep course/report subpages contextual.
3. Standardize page headings, active navigation, authorization/empty/error states, and breadcrumbs/context tabs.
4. Make report filters share one scope and persist in the query string; add visible filter chips, reset, result count, data freshness, and timezone.
5. Replace any editable text fields that represent closed enumerations with native dropdowns and server-side allowlist validation.

### P1 — Make core workspaces understandable

1. Build role-aware dashboards with three to four actionable KPIs, at most two principal visualizations, and one exception queue.
2. Refactor user/course/document lists to a shared accessible list pattern, including responsive row details and explicit action eligibility/reasons.
3. Turn course detail into a contextual workspace; show upload vs indexing states separately.
4. Redesign chat states around visible scope, citations, insufficient-evidence messaging, retry/fallback, and explicit feedback.

### P2 — Systematize and optimize

1. Consolidate design tokens for spacing, typography, surface, border, status, focus, and responsive breakpoints.
2. Extract reusable Razor partials/tag helpers for page header, filter bar, status badge, empty/error panel, pagination, KPI card, and row action menu.
3. Add accessible chart summaries, keyboard and screen-reader regression checks, 320px/200%-zoom snapshots, and per-role smoke tests.
4. Instrument product analytics for search success, zero-result queries, indexing failures/retries, dashboard-to-detail drill-down, chatbot insufficient-evidence rate, citation opens, and helpful/not-helpful feedback. Use these measures to validate the redesign rather than relying only on aesthetics.

## Suggested validation measures

- Task completion rate and median time for: finding a course document, uploading and confirming indexing, identifying an ineligible user deletion, opening a report with the intended scope, and obtaining a cited chatbot answer.
- Navigation backtracking rate and Access Denied visits by role.
- Search zero-result rate and filter-reset rate.
- Upload-to-index completion time and retry success rate.
- Dashboard card/chart drill-through rate.
- Chat insufficient-evidence rate, citation-open rate, explicit helpfulness, retry rate, and fallback-to-document-search rate.
- Accessibility defects found by keyboard, screen-reader, 320px, and 200%-zoom checks.

These metrics should be segmented by role and device class. They provide a Product Analyst/BI feedback loop: diagnose where users fail, implement a targeted design change, and compare task outcomes before and after release.
