using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.DAL.Repositories;
using PRN222_FINAL.BLL.Mapping;

namespace PRN222_FINAL.BLL;

public sealed record ChatAnswer(
    string Answer,
    IReadOnlyList<SourceCitation> Citations,
    IReadOnlyList<ChatMessage> History,
    string? ResolvedSubject = null,
    bool NeedsClarification = false,
    IReadOnlyList<string>? SubjectOptions = null,
    string AnswerSource = "Rag",
    bool HasDirectCitation = true,
    string? FallbackModel = null,
    string AnswerStatus = ChatGroundingPolicy.GroundedAnswerStatus);

public interface IRagChatService
{
    Task<ChatAnswer> AskAsync(
        Guid sessionId,
        string question,
        string? userDisplayName = null,
        string? subjectFilter = null,
        string? language = null,
        IReadOnlyCollection<string>? allowedSubjects = null,
        ChatSessionOwnerInfo? ownerInfo = null,
        DocumentAccessScope? accessScope = null,
        string? answerDepth = null,
        CancellationToken cancellationToken = default);
}

public sealed class RagChatService : IRagChatService
{
    private const int TopK = 8;
    private const int RerankCandidateK = 20;
    private const int MaxBatchQuestions = 50;
    private const double MinimumScore = 0.42;
    private const double MinimumLocalRerankScore = 0.48;
    private const double MinimumLlmRerankScore = 0.55;
    private const double MinimumAnswerGroundingRatio = 0.42;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled, RegexTimeout);
    private static readonly string[] AssessmentComponentPatterns =
    {
        @"^(?:\d+[.)]\s*)?(?<name>.+?)\s*(?:\||:|\s[-–—]\s)\s*(?<value>\d+(?:[.,]\d+)?)\s*%\s*$",
        @"^(?:\d+[.)]\s*)?(?<name>.+?)\s*\(\s*(?<value>\d+(?:[.,]\d+)?)\s*%\s*\)?\s*$",
        @"^(?:\d+[.)]\s*)?(?<name>.+?)\s+(?<value>\d+(?:[.,]\d+)?)\s*%\s*$",
        @"^(?<value>\d+(?:[.,]\d+)?)\s*%\s*(?:\||:|\s[-–—]\s)?\s*(?<name>.+?)\s*$"
    };
    private static readonly string[] PromptInjectionSignals =
    {
        "ignore",
        "disregard",
        "forget previous",
        "forget all",
        "bypass",
        "jailbreak",
        "system prompt",
        "developer message",
        "hidden instruction",
        "reveal prompt",
        "show prompt",
        "do not follow",
        "bo qua",
        "mac ke",
        "quen tat ca",
        "khong can tuan thu",
        "khong can theo tai lieu",
        "tra loi ngoai tai lieu",
        "bo qua quy tac",
        "bo qua quy chuan",
        "bo qua bao mat",
        "bo qua luat le",
        "phot lo"
    };

    private static readonly string[] CasualChatSignals =
    {
        "hi",
        "hello",
        "hey",
        "xin chao",
        "chao",
        "chao ban",
        "alo",
        "cam on",
        "thanks",
        "thank you",
        "tam biet",
        "bye",
        "an com chua",
        "com chua",
        "khoe khong",
        "on khong"
    };

    private static readonly string[] ExternalQuestionSignals =
    {
        "thoi tiet",
        "weather",
        "nhiet do",
        "temperature",
        "du bao",
        "forecast",
        "hom nay mua khong",
        "may gio",
        "what time",
        "current time",
        "tin tuc",
        "news today",
        "gia vang",
        "stock price",
        "ty gia",
        "exchange rate",
        "ket qua bong da",
        "lich thi dau"
    };

    private static readonly string[] DocumentQuestionSignals =
    {
        "tai lieu",
        "document",
        "syllabus",
        "mon",
        "hoc",
        "course",
        "subject",
        "chapter",
        "chuong",
        "tin chi",
        "credit",
        "nocredit",
        "assessment",
        "danh gia",
        "diem",
        "ty le diem",
        "so sanh",
        "khac nhau",
        "de hon",
        "kho hon",
        "compare",
        "difference",
        "easier",
        "harder",
        "quiz",
        "exam",
        "final exam",
        "session",
        "buoi",
        "lich hoc",
        "assignment",
        "clo",
        "learning outcome",
        "requirement",
        "yeu cau",
        "noi dung",
        "bai hoc",
        "lecture"
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "in", "is", "it", "of", "on", "or", "that", "the", "to",
        "who", "what", "where", "when", "why", "how", "please", "tell", "about",
        "va", "la", "cua", "cho", "trong", "khi", "voi", "mot", "cac", "nhung", "duoc", "tu", "theo", "nay", "do", "thi", "o",
        "ai", "gi", "nao", "hay", "cho", "biet", "ve", "khong", "da", "bao", "nhieu", "may", "hoi", "can", "noi", "noi dung"
    };

    private static readonly HashSet<string> AnswerScaffoldTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "answer", "based", "below", "checked", "citation", "cited", "course", "data", "document", "documents", "found", "from",
        "information", "ok", "okay", "quick", "sure", "student", "source", "sources",
        "ban", "cau", "co", "day", "du", "duoi", "gon", "hoi", "lieu", "minh", "nha", "nhe", "nguon", "noi", "phan", "roi",
        "sau", "sinh", "tai", "theo", "thay", "thong", "tin", "toi", "tom", "tra", "trich", "vien", "xem"
    };

    private readonly IKnowledgeRepository _repository;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILocalChatCompletionService _chatCompletionService;

    public RagChatService(
        IKnowledgeRepository repository,
        IEmbeddingService embeddingService,
        ILocalChatCompletionService chatCompletionService)
    {
        _repository = repository;
        _embeddingService = embeddingService;
        _chatCompletionService = chatCompletionService;
    }

    public async Task<ChatAnswer> AskAsync(
        Guid sessionId,
        string question,
        string? userDisplayName = null,
        string? subjectFilter = null,
        string? language = null,
        IReadOnlyCollection<string>? allowedSubjects = null,
        ChatSessionOwnerInfo? ownerInfo = null,
        DocumentAccessScope? accessScope = null,
        string? answerDepth = null,
        CancellationToken cancellationToken = default)
    {
        var trimmedQuestion = question.Trim();
        var responseLanguage = NormalizeLanguage(language);
        if (string.IsNullOrWhiteSpace(trimmedQuestion))
        {
            throw new InvalidOperationException("Question cannot be empty.");
        }

        var session = KnowledgeModelMapper.ToModel(await _repository.GetOrCreateSessionAsync(sessionId, cancellationToken, KnowledgeModelMapper.ToData(ownerInfo)));
        var historyBeforeQuestion = session.Messages.ToList();

        await _repository.AddMessageAsync(sessionId, KnowledgeModelMapper.ToData(new ChatMessage
        {
            Role = "user",
            Content = trimmedQuestion
        }), cancellationToken, KnowledgeModelMapper.ToData(ownerInfo));

        if (LooksLikePromptInjection(trimmedQuestion))
        {
            return await SaveAssistantAnswer(
                sessionId,
                BuildOutOfScopeAnswer(responseLanguage),
                Array.Empty<SourceCitation>(),
                cancellationToken,
                ownerInfo,
                answerSource: "OutOfScope",
                hasDirectCitation: false);
        }

        var questionBatch = SplitQuestionBatch(trimmedQuestion);
        if (questionBatch.Count > 1)
        {
            var scopedChunks = await GetScopedChunksAsync(allowedSubjects, accessScope, cancellationToken);
            var questionsToAnswer = questionBatch.Take(MaxBatchQuestions).ToList();
            var answers = new List<SingleQuestionAnswer>(questionsToAnswer.Count);

            foreach (var batchQuestion in questionsToAnswer)
            {
                answers.Add(await BuildSingleQuestionAnswerAsync(
                    batchQuestion,
                    historyBeforeQuestion,
                    userDisplayName,
                    responseLanguage,
                    subjectFilter,
                    allowedSubjects,
                    scopedChunks,
                    accessScope,
                    answerDepth,
                    cancellationToken));
            }

            var answerText = FormatBatchAnswer(answers, questionBatch.Count - questionsToAnswer.Count, responseLanguage);
            var citations = MergeCitations(answers.SelectMany(item => item.Citations));
            return await SaveAssistantAnswer(sessionId, answerText, citations, cancellationToken, ownerInfo);
        }

        var singleAnswer = await BuildSingleQuestionAnswerAsync(
            trimmedQuestion,
            historyBeforeQuestion,
            userDisplayName,
            responseLanguage,
            subjectFilter,
            allowedSubjects,
            scopedChunks: null,
            accessScope,
            answerDepth,
            cancellationToken);

        return await SaveAssistantAnswer(
            sessionId,
            singleAnswer.Answer,
            singleAnswer.Citations,
            cancellationToken,
            ownerInfo,
            singleAnswer.ResolvedSubject,
            singleAnswer.NeedsClarification,
            singleAnswer.SubjectOptions,
            singleAnswer.AnswerSource,
            singleAnswer.HasDirectCitation,
            singleAnswer.FallbackModel,
            singleAnswer.AnswerStatus);
    }

    private async Task<SingleQuestionAnswer> BuildSingleQuestionAnswerAsync(
        string question,
        IReadOnlyList<ChatMessage> historyBeforeQuestion,
        string? userDisplayName,
        string responseLanguage,
        string? subjectFilter,
        IReadOnlyCollection<string>? allowedSubjects,
        IReadOnlyList<DocumentChunk>? scopedChunks,
        DocumentAccessScope? accessScope,
        string? answerDepth,
        CancellationToken cancellationToken)
    {
        if (IsBotIdentityQuestion(question))
        {
            return new SingleQuestionAnswer(question, BuildBotIdentityAnswer(responseLanguage), Array.Empty<SourceCitation>(), AnswerSource: "SmallTalk", HasDirectCitation: false);
        }

        if (IsUserIdentityQuestion(question))
        {
            return new SingleQuestionAnswer(question, BuildUserIdentityAnswer(userDisplayName, responseLanguage), Array.Empty<SourceCitation>(), AnswerSource: "SmallTalk", HasDirectCitation: false);
        }

        if (IsPersonalGradeQuestion(question))
        {
            var personalGradeAnswer = responseLanguage == "vi"
                ? "Mình chưa có nguồn điểm cá nhân đã được xác thực và cấp quyền, nên không thể xem hoặc suy đoán điểm của bạn. Nếu bạn đang hỏi **cơ cấu điểm của môn học**, hãy cho mình mã môn."
                : "I do not have an authorized personal-grade source, so I cannot view or infer your grades. If you mean the **course assessment structure**, provide the course code.";
            return new SingleQuestionAnswer(
                question,
                personalGradeAnswer,
                Array.Empty<SourceCitation>(),
                AnswerSource: "OutOfScope",
                HasDirectCitation: false,
                AnswerStatus: ChatGroundingPolicy.InsufficientEvidenceStatus);
        }

        var intent = await ClassifyQuestionIntentAsync(
            question,
            historyBeforeQuestion,
            responseLanguage,
            cancellationToken);

        if (intent.Intent == ChatQueryIntent.Unsafe)
        {
            return new SingleQuestionAnswer(question, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>(), AnswerSource: "OutOfScope", HasDirectCitation: false);
        }

        if (intent.Intent == ChatQueryIntent.SmallTalk)
        {
            return new SingleQuestionAnswer(question, BuildCasualChatAnswer(question, responseLanguage), Array.Empty<SourceCitation>(), AnswerSource: "SmallTalk", HasDirectCitation: false);
        }

        if (intent.Intent == ChatQueryIntent.ExternalQuestion)
        {
            return new SingleQuestionAnswer(question, BuildExternalScopeAnswer(responseLanguage), Array.Empty<SourceCitation>(), AnswerSource: "OutOfScope", HasDirectCitation: false);
        }

        var retrievalQueries = await BuildRetrievalQueriesAsync(
            question,
            historyBeforeQuestion,
            cancellationToken);
        var resolvedQuestion = retrievalQueries.FirstOrDefault(query => !query.Equals(question, StringComparison.OrdinalIgnoreCase))
                               ?? question;
        var retrievalQuestion = string.Join("\n", retrievalQueries);

        scopedChunks ??= await GetScopedChunksAsync(allowedSubjects, accessScope, cancellationToken);
        if (scopedChunks.Count == 0)
        {
            return await BuildInsufficientAnswerAsync(
                question,
                subjectFilter,
                historyBeforeQuestion,
                Array.Empty<DocumentChunk>(),
                responseLanguage,
                cancellationToken);
        }

        var route = ResolveSubjectRoute(retrievalQuestion, subjectFilter, scopedChunks);
        if (!string.IsNullOrWhiteSpace(route.SelectedSubject))
        {
            scopedChunks = scopedChunks
                .Where(chunk => SubjectMatches(chunk.Subject, route.SelectedSubject))
                .ToList();
            if (scopedChunks.Count == 0)
            {
                return await BuildInsufficientAnswerAsync(
                    question,
                    route.SelectedSubject,
                    historyBeforeQuestion,
                    Array.Empty<DocumentChunk>(),
                    responseLanguage,
                    cancellationToken);
            }
        }

        var exactFactIntent = DetectExactFactIntent(retrievalQuestion);
        if (TryBuildMultiSubjectCreditComparison(retrievalQuestion, scopedChunks, responseLanguage) is { } comparisonAnswer)
        {
            return comparisonAnswer;
        }

        if (TryBuildExactFactAnswer(retrievalQuestion, scopedChunks, responseLanguage) is { } exactFactAnswer)
        {
            return exactFactAnswer with { ResolvedSubject = route.SelectedSubject ?? exactFactAnswer.ResolvedSubject };
        }

        if (TryBuildAssessmentStructureAnswer(retrievalQuestion, scopedChunks, responseLanguage) is { } assessmentAnswer)
        {
            return assessmentAnswer with { ResolvedSubject = route.SelectedSubject ?? assessmentAnswer.ResolvedSubject };
        }

        if (exactFactIntent != ExactFactIntent.None)
        {
            return await BuildInsufficientAnswerAsync(
                question,
                route.SelectedSubject ?? subjectFilter,
                historyBeforeQuestion,
                Array.Empty<DocumentChunk>(),
                responseLanguage,
                cancellationToken);
        }

        if (TryBuildSubjectSkillsAnswer(retrievalQuestion, scopedChunks, responseLanguage) is { } skillsAnswer)
        {
            return skillsAnswer with { ResolvedSubject = route.SelectedSubject ?? skillsAnswer.ResolvedSubject };
        }

        if (TryBuildSubjectOverviewAnswer(retrievalQuestion, scopedChunks, responseLanguage) is { } subjectOverviewAnswer)
        {
            return subjectOverviewAnswer with { ResolvedSubject = route.SelectedSubject ?? subjectOverviewAnswer.ResolvedSubject };
        }

        var queryTerms = ExtractTerms(retrievalQuestion);

        var contentTerms = RemoveCourseScopeTerms(queryTerms);
        var candidateMatches = await BuildCandidateMatchesAsync(
            retrievalQueries,
            queryTerms,
            contentTerms,
            scopedChunks,
            cancellationToken);

        if (candidateMatches.Count == 0)
        {
            return await BuildInsufficientAnswerAsync(
                question,
                route.SelectedSubject ?? subjectFilter,
                historyBeforeQuestion,
                scopedChunks,
                responseLanguage,
                cancellationToken);
        }

        var matches = await RerankMatchesAsync(retrievalQuestion, candidateMatches, responseLanguage, cancellationToken);
        if (matches.Count == 0)
        {
            return await BuildInsufficientAnswerAsync(
                question,
                route.SelectedSubject ?? subjectFilter,
                historyBeforeQuestion,
                scopedChunks,
                responseLanguage,
                cancellationToken);
        }

        var citations = matches.Select(item => new SourceCitation
        {
            DocumentId = item.Chunk.DocumentId,
            FileName = item.Chunk.FileName,
            Subject = item.Chunk.Subject,
            Chapter = item.Chunk.Chapter,
            ChunkIndex = item.Chunk.ChunkIndex,
            Score = Math.Round(item.Score, 3),
            Excerpt = CreateExcerpt(item.Chunk.Text)
        }).ToList();

        var matchedChunks = matches.Select(item => item.Chunk).ToList();
        var resolvedSubject = route.SelectedSubject ?? ResolveSubject(matchedChunks);
        var comparisonQuestion = retrievalQueries.FirstOrDefault(query =>
                                     ExtractCourseCodes(query).Count >= 2
                                     && IsMultiSubjectQuestion(NormalizeQuestion(query)))
                                 ?? question;
        if (TryBuildMultiSubjectStudyComparison(comparisonQuestion, matches, scopedChunks, responseLanguage) is { } studyComparison)
        {
            return studyComparison;
        }

        var generatedAnswer = await _chatCompletionService.GenerateAnswerAsync(
            ApplyAnswerDepth(resolvedQuestion, answerDepth, responseLanguage),
            resolvedSubject,
            historyBeforeQuestion,
            matchedChunks,
            responseLanguage,
            cancellationToken);
        var answer = string.IsNullOrWhiteSpace(generatedAnswer)
                     || (!IsInsufficientDataAnswer(generatedAnswer) && !ChatGroundingPolicy.HasValidSourceMarkers(generatedAnswer, matchedChunks.Count))
            ? BuildGroundedAnswer(contentTerms, matchedChunks, responseLanguage, ExtractCourseCodes(retrievalQuestion))
            : generatedAnswer.Trim();

        if (IsInsufficientDataAnswer(answer))
        {
            return await BuildInsufficientAnswerAsync(
                question,
                resolvedSubject,
                historyBeforeQuestion,
                scopedChunks,
                responseLanguage,
                cancellationToken);
        }

        if (!await IsAnswerGroundedAsync(resolvedQuestion, answer, matchedChunks, queryTerms, responseLanguage, cancellationToken))
        {
            return await BuildInsufficientAnswerAsync(
                question,
                resolvedSubject,
                historyBeforeQuestion,
                scopedChunks,
                responseLanguage,
                cancellationToken);
        }

        var normalizedSources = ChatGroundingPolicy.NormalizeSourceMarkers(answer);
        var referencedCitations = normalizedSources.OriginalSourceNumbers
            .Where(sourceNumber => sourceNumber >= 1 && sourceNumber <= citations.Count)
            .Select(sourceNumber => citations[sourceNumber - 1])
            .ToList();
        return new SingleQuestionAnswer(
            question,
            normalizedSources.Answer,
            referencedCitations,
            resolvedSubject);
    }

    private static string ApplyAnswerDepth(string question, string? answerDepth, string language)
    {
        var instruction = answerDepth?.Trim().ToLowerInvariant() switch
        {
            "concise" => language == "vi" ? "Trả lời thật ngắn gọn, tối đa 3 ý chính." : "Answer concisely in at most 3 key points.",
            "deep" => language == "vi" ? "Giải thích kỹ, có cấu trúc và nêu mối liên hệ giữa các ý trong tài liệu." : "Explain thoroughly with structure and relationships between ideas in the documents.",
            _ => language == "vi" ? "Trả lời vừa đủ, rõ ràng và dễ học." : "Give a balanced, clear answer that is easy to study."
        };
        return $"{question}\n\nYêu cầu cách trình bày: {instruction}";
    }

    private async Task<IReadOnlyList<DocumentChunk>> GetScopedChunksAsync(
        IReadOnlyCollection<string>? allowedSubjects,
        DocumentAccessScope? accessScope,
        CancellationToken cancellationToken)
    {
        var normalizedAllowedSubjects = allowedSubjects?
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedAllowedSubjects is null || normalizedAllowedSubjects.Count == 0)
        {
            return Array.Empty<DocumentChunk>();
        }

        if (accessScope is not null)
        {
            var scopedChunks = (await _repository.GetChunksAsync(
                    KnowledgeModelMapper.ToData(accessScope),
                    normalizedAllowedSubjects,
                    cancellationToken))
                .Select(KnowledgeModelMapper.ToModel)
                .ToList();
            return DeduplicateChunks(scopedChunks);
        }

        var chunks = await _repository.GetChunksAsync(cancellationToken);

        var allowedChunks = chunks.Select(KnowledgeModelMapper.ToModel)
            .Where(chunk => normalizedAllowedSubjects.Any(subject => SubjectMatches(chunk.Subject, subject)))
            .ToList();
        return DeduplicateChunks(allowedChunks);
    }

    private static IReadOnlyList<DocumentChunk> DeduplicateChunks(IReadOnlyList<DocumentChunk> chunks)
    {
        return chunks
            .GroupBy(chunk => string.Join(
                '\u001F',
                NormalizeQuestion(chunk.Subject),
                NormalizeQuestion(chunk.Chapter),
                TextEncodingHelper.NormalizeForIndexing(chunk.Text)), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private async Task<SingleQuestionAnswer> BuildInsufficientAnswerAsync(
        string question,
        string? subject,
        IReadOnlyList<ChatMessage> historyBeforeQuestion,
        IReadOnlyList<DocumentChunk> subjectChunks,
        string responseLanguage,
        CancellationToken cancellationToken)
    {
        var documentFallback = await TryBuildDocumentFallbackAnswerAsync(
            question,
            subject,
            historyBeforeQuestion,
            subjectChunks,
            responseLanguage,
            cancellationToken);
        if (documentFallback is not null)
        {
            return documentFallback;
        }

        return new SingleQuestionAnswer(
            question,
            BuildOutOfScopeAnswer(responseLanguage),
            Array.Empty<SourceCitation>(),
            subject,
            AnswerSource: "OutOfScope",
            HasDirectCitation: false);
    }

    private async Task<SingleQuestionAnswer?> TryBuildDocumentFallbackAnswerAsync(
        string question,
        string? subject,
        IReadOnlyList<ChatMessage> historyBeforeQuestion,
        IReadOnlyList<DocumentChunk> subjectChunks,
        string responseLanguage,
        CancellationToken cancellationToken)
    {
        if (subjectChunks.Count == 0)
        {
            return null;
        }

        if (TryBuildIndexedPartsAnswer(question, subjectChunks, responseLanguage) is { } indexedPartsAnswer)
        {
            return indexedPartsAnswer;
        }

        var fallbackChunks = SelectDocumentFallbackChunks(question, subjectChunks);
        if (fallbackChunks.Count == 0)
        {
            return null;
        }

        var resolvedSubject = string.IsNullOrWhiteSpace(subject) ? ResolveSubject(fallbackChunks) : subject.Trim();
        var queryTerms = ExtractTerms(question);
        var answerSelectionTerms = new HashSet<string>(queryTerms, StringComparer.OrdinalIgnoreCase);
        answerSelectionTerms.UnionWith(BuildDocumentFallbackTerms(question));
        string? generatedAnswer = null;
        try
        {
            generatedAnswer = await _chatCompletionService.GenerateAnswerAsync(
                question,
                resolvedSubject,
                historyBeforeQuestion,
                fallbackChunks,
                responseLanguage,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            generatedAnswer = null;
        }

        var answer = string.IsNullOrWhiteSpace(generatedAnswer)
                     || IsInsufficientDataAnswer(generatedAnswer)
                     || !ChatGroundingPolicy.HasValidSourceMarkers(generatedAnswer, fallbackChunks.Count)
            ? BuildGroundedAnswer(answerSelectionTerms, fallbackChunks, responseLanguage, ExtractCourseCodes(question))
            : generatedAnswer.Trim();
        if (IsInsufficientDataAnswer(answer))
        {
            return null;
        }

        if (!await IsAnswerGroundedAsync(question, answer, fallbackChunks, queryTerms, responseLanguage, cancellationToken))
        {
            return null;
        }

        var fallbackCitations = BuildCitations(fallbackChunks, 0.74);
        var normalizedSources = ChatGroundingPolicy.NormalizeSourceMarkers(answer);
        var referencedCitations = normalizedSources.OriginalSourceNumbers
            .Where(sourceNumber => sourceNumber >= 1 && sourceNumber <= fallbackCitations.Count)
            .Select(sourceNumber => fallbackCitations[sourceNumber - 1])
            .ToList();
        return new SingleQuestionAnswer(
            question,
            normalizedSources.Answer,
            referencedCitations,
            resolvedSubject,
            AnswerSource: "Rag",
            HasDirectCitation: referencedCitations.Count > 0);
    }

    private static SingleQuestionAnswer? TryBuildIndexedPartsAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language)
    {
        if (!IsIndexedPartsQuestion(question) || chunks.Count == 0)
        {
            return null;
        }

        var grouped = chunks
            .GroupBy(chunk => string.IsNullOrWhiteSpace(chunk.Chapter) ? "Không rõ chương" : chunk.Chapter.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Min(chunk => chunk.ChunkIndex))
            .Take(12)
            .ToList();
        if (grouped.Count == 0)
        {
            return null;
        }

        var subject = ResolveSubject(chunks);
        var lines = grouped.Select(group =>
        {
            var sectionTitles = group
                .Select(chunk => chunk.SectionTitle)
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();
            var suffix = sectionTitles.Count == 0 ? string.Empty : $" ({string.Join(", ", sectionTitles)})";
            return language == "vi"
                ? $"- {group.Key}{suffix}: {group.Count()} chunk"
                : $"- {group.Key}{suffix}: {group.Count()} chunks";
        });

        var answer = language == "vi"
            ? $"Mình thấy {ExtractSubjectCode(subject)} đã index các chương/phần sau:\n\n{string.Join("\n", lines)}"
            : $"I found these indexed chapters/sections for {ExtractSubjectCode(subject)}:\n\n{string.Join("\n", lines)}";

        var citationChunks = grouped.Select(group => group.First()).Take(5).ToList();
        return new SingleQuestionAnswer(
            question,
            answer,
            BuildCitations(citationChunks, 0.9),
            subject,
            AnswerSource: "Rag",
            HasDirectCitation: true);
    }

    private static bool IsIndexedPartsQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return (normalized.Contains("index", StringComparison.Ordinal)
                || normalized.Contains("da index", StringComparison.Ordinal)
                || normalized.Contains("indexed", StringComparison.Ordinal))
               && (normalized.Contains("chuong", StringComparison.Ordinal)
                   || normalized.Contains("phan", StringComparison.Ordinal)
                   || normalized.Contains("section", StringComparison.Ordinal)
                   || normalized.Contains("chapter", StringComparison.Ordinal));
    }

    private static IReadOnlyList<DocumentChunk> SelectDocumentFallbackChunks(
        string question,
        IReadOnlyList<DocumentChunk> chunks)
    {
        var questionTerms = ExtractTerms(question);
        var intentTerms = BuildDocumentFallbackTerms(question);
        var terms = new HashSet<string>(questionTerms, StringComparer.OrdinalIgnoreCase);
        terms.UnionWith(intentTerms);
        if (terms.Count == 0)
        {
            return Array.Empty<DocumentChunk>();
        }

        return chunks
            .Select(chunk =>
            {
                var textScore = CountSharedTerms(terms, chunk.Text);
                var queryTextScore = CountSharedTerms(questionTerms, chunk.Text);
                var intentTextScore = CountSharedTerms(intentTerms, chunk.Text);
                var metadataScore = CountSharedTerms(terms, BuildChunkMetadataText(chunk));
                var score = (intentTextScore * 2.0) + queryTextScore + (textScore * 0.3) + (metadataScore * 0.4);
                return new { Chunk = chunk, Score = score, IntentTextScore = intentTextScore };
            })
            .Where(item => item.Score >= 1.0 && (intentTerms.Count == 0 || item.IntentTextScore > 0))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.ChunkIndex)
            .Select(item => item.Chunk)
            .Take(6)
            .ToList();
    }

    private static HashSet<string> BuildDocumentFallbackTerms(string question)
    {
        var normalized = NormalizeQuestion(question);
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (normalized.Contains("danh gia", StringComparison.Ordinal)
            || normalized.Contains("ty le", StringComparison.Ordinal)
            || normalized.Contains("thi", StringComparison.Ordinal)
            || normalized.Contains("assessment", StringComparison.Ordinal)
            || normalized.Contains("exam", StringComparison.Ordinal))
        {
            terms.UnionWith(new[]
            {
                "assessment", "exam", "final", "quiz", "participation", "assignment", "weight", "percentage", "criteria", "grading",
                "danh", "gia", "diem", "thi", "cuoi", "ky", "thuc", "hanh", "tham", "gia", "ty", "le"
            });
        }

        if (normalized.Contains("tai lieu", StringComparison.Ordinal)
            || normalized.Contains("nguon", StringComparison.Ordinal)
            || normalized.Contains("resource", StringComparison.Ordinal)
            || normalized.Contains("material", StringComparison.Ordinal)
            || normalized.Contains("textbook", StringComparison.Ordinal))
        {
            terms.UnionWith(new[]
            {
                "resource", "resources", "material", "materials", "textbook", "book", "url", "website", "youtube", "clip",
                "tai", "lieu", "nguon", "hoc", "giao", "trinh", "tham", "khao"
            });
        }

        if (normalized.Contains("sinh vien", StringComparison.Ordinal)
            || normalized.Contains("can lam", StringComparison.Ordinal)
            || normalized.Contains("lam gi", StringComparison.Ordinal)
            || normalized.Contains("student", StringComparison.Ordinal))
        {
            terms.UnionWith(new[]
            {
                "student", "students", "learner", "learners", "activity", "activities", "practice", "assignment", "completion", "criteria",
                "sinh", "vien", "lam", "thuc", "hanh", "bai", "tap", "hoat", "dong", "yeu", "cau"
            });
        }

        if (normalized.Contains("chuan dau ra", StringComparison.Ordinal)
            || normalized.Contains("outcome", StringComparison.Ordinal)
            || normalized.Contains("clo", StringComparison.Ordinal))
        {
            terms.UnionWith(new[]
            {
                "clo", "outcome", "outcomes", "learning", "objective", "objectives", "knowledge", "skill",
                "chuan", "dau", "ra", "muc", "tieu", "kien", "thuc", "ky", "nang"
            });
        }

        return terms;
    }

    private static IReadOnlyList<SourceCitation> BuildCitations(IReadOnlyList<DocumentChunk> chunks, double score)
    {
        return chunks
            .GroupBy(chunk => new { chunk.DocumentId, chunk.ChunkIndex })
            .Select(group => group.First())
            .Take(5)
            .Select(chunk => new SourceCitation
            {
                DocumentId = chunk.DocumentId,
                FileName = chunk.FileName,
                Subject = chunk.Subject,
                Chapter = chunk.Chapter,
                ChunkIndex = chunk.ChunkIndex,
                Score = score,
                Excerpt = CreateExcerpt(chunk.Text)
            })
            .ToList();
    }

    private async Task<IReadOnlyList<ScoredChunk>> BuildCandidateMatchesAsync(
        IReadOnlyList<string> retrievalQueries,
        IReadOnlySet<string> queryTerms,
        IReadOnlySet<string> contentTerms,
        IReadOnlyList<DocumentChunk> scopedChunks,
        CancellationToken cancellationToken)
    {
        var needsContentEvidence = contentTerms.Count > 0;
        var minimumSharedTerms = contentTerms.Count >= 4 ? 2 : 1;
        var queryEmbeddings = new List<Dictionary<int, double>>();

        foreach (var query in retrievalQueries.Where(query => !string.IsNullOrWhiteSpace(query)).Take(4))
        {
            queryEmbeddings.Add(await _embeddingService.EmbedAsync(
                query,
                EmbeddingInputType.SearchQuery,
                cancellationToken));
        }

        if (queryEmbeddings.Count == 0)
        {
            queryEmbeddings.Add(await _embeddingService.EmbedAsync(
                string.Join(" ", queryTerms),
                EmbeddingInputType.SearchQuery,
                cancellationToken));
        }

        var courseCodes = ExtractCourseCodes(string.Join("\n", retrievalQueries));

        var scoredCandidates = scopedChunks
            .Select(chunk =>
            {
                var vectorScore = queryEmbeddings.Count == 0
                    ? 0
                    : queryEmbeddings.Max(embedding => _embeddingService.CosineSimilarity(embedding, chunk.Embedding));
                var textSharedTerms = CountSharedTerms(queryTerms, chunk.Text);
                var contentSharedTerms = CountSharedTerms(contentTerms, chunk.Text);
                var metadataSharedTerms = CountSharedTerms(queryTerms, BuildChunkMetadataText(chunk));
                var lexicalScore = CalculateLexicalScore(
                    textSharedTerms,
                    metadataSharedTerms,
                    contentSharedTerms,
                    queryTerms.Count,
                    contentTerms.Count);
                var courseCodeMatched = courseCodes.Count > 0
                                        && CountSharedTerms(courseCodes, BuildChunkMetadataText(chunk)) > 0;

                return new ScoredChunk(
                    chunk,
                    CalculateRetrievalScore(vectorScore, lexicalScore, courseCodeMatched),
                    textSharedTerms,
                    contentSharedTerms,
                    textSharedTerms + metadataSharedTerms,
                    metadataSharedTerms,
                    Math.Clamp(vectorScore, 0, 1),
                    lexicalScore);
            })
            .Where(item => HasRetrievalEvidence(item, needsContentEvidence, minimumSharedTerms))
            .ToList();

        var rankedCandidates = RankByReciprocalRankFusion(scoredCandidates);
        var comparisonQuestion = retrievalQueries.FirstOrDefault(query =>
                                     ExtractCourseCodes(query).Count >= 2
                                     && IsMultiSubjectQuestion(NormalizeQuestion(query)))
                                 ?? string.Join("\n", retrievalQueries);
        return SelectWithEvidenceCoverage(
            rankedCandidates,
            BuildComparisonEvidenceSlots(comparisonQuestion),
            courseCodes,
            RerankCandidateK);
    }

    private static SubjectRouteResult ResolveSubjectRoute(
        string question,
        string? subjectFilter,
        IReadOnlyList<DocumentChunk> chunks)
    {
        var subjects = chunks
            .Select(chunk => chunk.Subject)
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(subject => subject)
            .ToList();

        if (subjects.Count == 0)
        {
            return new SubjectRouteResult(null, false, Array.Empty<string>());
        }

        if (subjects.Count == 1)
        {
            return new SubjectRouteResult(subjects[0], false, Array.Empty<string>());
        }

        var normalizedQuestion = NormalizeQuestion(question);
        var explicitMatches = subjects
            .Where(subject => GetSubjectAliases(subject).Any(alias => normalizedQuestion.Contains(alias, StringComparison.Ordinal)))
            .ToList();

        if (explicitMatches.Count == 1)
        {
            return new SubjectRouteResult(explicitMatches[0], false, Array.Empty<string>());
        }

        // When a question mentions more than one subject, keep the full scope
        // and answer in one pass instead of asking the user to choose a subject.

        if (!string.IsNullOrWhiteSpace(subjectFilter) && !IsAllSubjectsFilter(subjectFilter))
        {
            var selected = subjects.FirstOrDefault(subject => SubjectMatches(subject, subjectFilter))
                ?? subjectFilter.Trim();
            return new SubjectRouteResult(selected, false, Array.Empty<string>());
        }

        return new SubjectRouteResult(null, false, Array.Empty<string>());
    }

    private static bool SubjectMatches(string subject, string filter)
    {
        var normalizedSubject = (subject ?? string.Empty).Trim();
        var normalizedFilter = (filter ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedSubject) || string.IsNullOrWhiteSpace(normalizedFilter))
        {
            return false;
        }

        if (normalizedSubject.Equals(normalizedFilter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var subjectCode = ExtractSubjectCode(normalizedSubject);
        var filterCode = ExtractSubjectCode(normalizedFilter);
        return !string.IsNullOrWhiteSpace(subjectCode)
               && subjectCode.Equals(filterCode, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllSubjectsFilter(string value)
    {
        var normalized = NormalizeQuestion(value);
        return normalized is "all" or "all subjects" or "tat ca" or "tat ca mon";
    }

    private static IReadOnlyList<string> GetSubjectAliases(string subject)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedSubject = NormalizeQuestion(subject);
        if (normalizedSubject.Length >= 3)
        {
            aliases.Add(normalizedSubject);
        }

        var code = ExtractSubjectCode(subject);
        var normalizedCode = NormalizeQuestion(code);
        if (normalizedCode.Length >= 3)
        {
            aliases.Add(normalizedCode);
            aliases.Add(normalizedCode.Replace(" ", string.Empty, StringComparison.Ordinal));

            var codePrefix = Regex.Match(normalizedCode, @"^[a-z]+", RegexOptions.CultureInvariant, RegexTimeout).Value;
            if (codePrefix.Length >= 3)
            {
                aliases.Add(codePrefix);
            }
        }

        var separatorIndex = subject.IndexOf('-', StringComparison.Ordinal);
        if (separatorIndex >= 0 && separatorIndex + 1 < subject.Length)
        {
            var name = NormalizeQuestion(subject[(separatorIndex + 1)..]);
            if (name.Length >= 6)
            {
                aliases.Add(name);
            }
        }

        return aliases.ToList();
    }

    private static string ExtractSubjectCode(string subject)
    {
        var trimmed = (subject ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var separatorIndex = trimmed.IndexOf('-', StringComparison.Ordinal);
        var candidate = separatorIndex > 0
            ? trimmed[..separatorIndex]
            : trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? trimmed;
        return new string(candidate
            .Where(character => char.IsLetterOrDigit(character) || character is '_' or '.')
            .ToArray())
            .ToUpperInvariant();
    }

    private static bool IsMultiSubjectQuestion(string normalizedQuestion)
    {
        return normalizedQuestion.Contains("so sanh", StringComparison.Ordinal)
               || normalizedQuestion.Contains("khac nhau", StringComparison.Ordinal)
               || normalizedQuestion.Contains("giong nhau", StringComparison.Ordinal)
               || normalizedQuestion.Contains("de hon", StringComparison.Ordinal)
               || normalizedQuestion.Contains("kho hon", StringComparison.Ordinal)
               || normalizedQuestion.Contains("hai mon", StringComparison.Ordinal)
               || normalizedQuestion.Contains("2 mon", StringComparison.Ordinal)
               || normalizedQuestion.Contains("compare", StringComparison.Ordinal)
               || normalizedQuestion.Contains("difference", StringComparison.Ordinal)
               || normalizedQuestion.Contains("different", StringComparison.Ordinal)
               || normalizedQuestion.Contains("easier", StringComparison.Ordinal)
               || normalizedQuestion.Contains("harder", StringComparison.Ordinal)
               || normalizedQuestion.Contains("these two courses", StringComparison.Ordinal)
               || normalizedQuestion.Contains("tat ca mon", StringComparison.Ordinal)
               || normalizedQuestion.Contains("cac mon", StringComparison.Ordinal)
               || normalizedQuestion.Contains("all subjects", StringComparison.Ordinal);
    }

    private static SingleQuestionAnswer? TryBuildExactFactAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language)
    {
        var factIntent = DetectExactFactIntent(question);
        if (factIntent == ExactFactIntent.LecturerName)
        {
            return TryBuildLecturerNameAnswer(question, chunks, language);
        }

        if (factIntent == ExactFactIntent.AttendancePercentage)
        {
            return TryBuildAttendanceAnswer(question, chunks, language);
        }

        if (factIntent != ExactFactIntent.Credits)
        {
            return null;
        }

        var evidence = chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Credit = TryExtractCreditFact(chunk.Text),
                SubjectCodeScore = CountSharedTerms(ExtractCourseCodes(question), BuildChunkMetadataText(chunk))
            })
            .Where(item => item.Credit is not null)
            .OrderByDescending(item => item.SubjectCodeScore)
            .ThenBy(item => item.Chunk.ChunkIndex)
            .FirstOrDefault();

        if (evidence?.Credit is null)
        {
            return null;
        }

        var courseLabel = ResolveFactCourseLabel(question, evidence.Chunk);
        var answer = language == "vi"
            ? $"Mình xem trong tài liệu rồi: {courseLabel} có {FormatCreditValue(evidence.Credit.Value)} tín chỉ."
            : $"I checked the documents: {courseLabel} has {FormatCreditValue(evidence.Credit.Value)} credits.";
        var citations = new[]
        {
            new SourceCitation
            {
                DocumentId = evidence.Chunk.DocumentId,
                FileName = evidence.Chunk.FileName,
                Subject = evidence.Chunk.Subject,
                Chapter = evidence.Chunk.Chapter,
                ChunkIndex = evidence.Chunk.ChunkIndex,
                Score = 0.99,
                Excerpt = CreateFactExcerpt(evidence.Chunk.Text, evidence.Credit.EvidenceText)
            }
        };

        return new SingleQuestionAnswer(question, answer, citations, ResolveSubject(new[] { evidence.Chunk }));
    }

    private static SingleQuestionAnswer? TryBuildMultiSubjectStudyComparison(
        string question,
        IReadOnlyList<ScoredChunk> matches,
        IReadOnlyList<DocumentChunk> scopedChunks,
        string language)
    {
        var normalizedQuestion = NormalizeQuestion(question);
        if (!IsMultiSubjectQuestion(normalizedQuestion)
            || DetectExactFactIntent(question) == ExactFactIntent.Credits)
        {
            return null;
        }

        var courseCodes = ExtractCourseCodesInOrder(question).Take(4).ToList();
        if (courseCodes.Count < 2)
        {
            return null;
        }

        if (IsAssessmentWeightQuestion(normalizedQuestion))
        {
            return BuildAssessmentComparisonAnswer(question, matches, scopedChunks, courseCodes, language);
        }

        var isDifficultyQuestion = IsDifficultyQuestion(normalizedQuestion);
        var studentPreference = isDifficultyQuestion ? BuildStudentPreferenceProfile(normalizedQuestion) : null;
        var evidenceTerms = ExtractTerms(isDifficultyQuestion
            ? "khối lượng workload điều kiện prerequisite bài tập assignment dự án project thực hành lab thi exam đánh giá assessment tín chỉ credit session buổi"
            : "mục tiêu nội dung kỹ năng chuẩn đầu ra đánh giá bài tập dự án thực hành tín chỉ workload assessment outcome skill");
        var citations = new List<SourceCitation>();
        var heading = (language, isDifficultyQuestion) switch
        {
            ("vi", true) => "## So sánh độ khó có căn cứ",
            ("vi", false) => "## Điểm khác nhau theo tài liệu",
            (_, true) => "## Evidence-based difficulty comparison",
            _ => "## Differences supported by the documents"
        };
        var lines = new List<string>
        {
            heading
        };

        if (isDifficultyQuestion)
        {
            lines.Add(language == "vi"
                ? "Không thể khẳng định môn nào **dễ hơn một cách khách quan** chỉ từ đề cương. Có thể so sánh các yêu cầu quan sát được như sau:"
                : "A syllabus alone cannot prove which course is **objectively easier**. The observable requirements are:");
        }

        var coursesWithEvidence = 0;
        foreach (var courseCode in courseCodes)
        {
            var courseMatches = matches
                .Where(match => SubjectMatches(match.Chunk.Subject, courseCode))
                .ToList();
            var evidence = courseMatches
                .SelectMany(match => SplitSentences(match.Chunk.Text).Select(sentence => new
                {
                    Match = match,
                    Sentence = sentence,
                    DimensionScore = CountSharedTerms(evidenceTerms, sentence),
                    QuestionScore = CountSharedTerms(ExtractTerms(question), sentence)
                }))
                .Where(item => item.Sentence.Length is > 8 and <= 500 && !IsLowValueFallbackSentence(item.Sentence))
                .OrderByDescending(item => item.DimensionScore)
                .ThenByDescending(item => item.QuestionScore)
                .ThenByDescending(item => item.Match.Score)
                .GroupBy(item => item.Sentence, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(2)
                .ToList();

            lines.Add($"### {courseCode}");
            if (evidence.Count == 0)
            {
                lines.Add(language == "vi"
                    ? "- Chưa đủ bằng chứng phù hợp trong tài liệu được phép truy cập."
                    : "- No qualified evidence was found in the authorized documents.");
                continue;
            }

            coursesWithEvidence++;
            foreach (var item in evidence)
            {
                var sourceNumber = AddComparisonCitation(citations, item.Match, item.Sentence);
                lines.Add($"- {AttachSourceMarker(item.Sentence, sourceNumber)}");
            }
        }

        if (isDifficultyQuestion)
        {
            var preferenceRanking = studentPreference is null
                ? []
                : courseCodes
                    .Select(courseCode => matches
                        .Where(match => SubjectMatches(match.Chunk.Subject, courseCode))
                        .Select(match => new
                        {
                            CourseCode = courseCode,
                            Match = match,
                            Score = CountSharedTerms(studentPreference.Terms, match.Chunk.Text)
                        })
                        .OrderByDescending(item => item.Score)
                        .ThenByDescending(item => item.Match.Score)
                        .FirstOrDefault())
                    .Where(item => item is not null)
                    .OrderByDescending(item => item!.Score)
                    .ToList();
            var bestFit = preferenceRanking.FirstOrDefault();
            var secondFitScore = preferenceRanking.Skip(1).FirstOrDefault()?.Score ?? 0;
            if (studentPreference is not null
                && bestFit is not null
                && bestFit.Score >= 2
                && bestFit.Score > secondFitScore)
            {
                var sourceNumber = AddComparisonCitation(citations, bestFit.Match, bestFit.Match.Chunk.Text);
                var fitStatement = language == "vi"
                    ? $"**Gợi ý theo độ phù hợp:** Với thế mạnh **{studentPreference.LabelVi}** bạn vừa nêu, **{bestFit.CourseCode} có vẻ phù hợp hơn** vì tài liệu môn này chứa nhiều yêu cầu liên quan trực tiếp tới thế mạnh đó. Đây không phải kết luận môn này dễ hơn."
                    : $"**Conditional fit:** Given your stated strength in **{studentPreference.LabelEn}**, **{bestFit.CourseCode} may be a better fit** because its documents contain requirements directly related to that strength. This does not mean the course is objectively easier.";
                lines.Add(AttachSourceMarker(fitStatement, sourceNumber));
            }
            else
            {
                lines.Add(language == "vi"
                    ? "**Kết luận:** môn phù hợp hơn còn phụ thuộc nền tảng, sở thích và thế mạnh của bạn. Nếu bạn cho biết mình mạnh lý thuyết, lập trình hay thực hành, mình có thể tư vấn theo độ phù hợp thay vì đoán độ dễ."
                    : "**Conclusion:** the better fit depends on your background, preferences, and strengths. Share whether you prefer theory, coding, or hands-on work for a conditional recommendation.");
            }
        }

        return new SingleQuestionAnswer(
            question,
            string.Join("\n", lines),
            citations,
            AnswerSource: "Rag",
            HasDirectCitation: citations.Count > 0,
            AnswerStatus: ResolveComparisonAnswerStatus(
                coursesWithEvidence == courseCodes.Count,
                coursesWithEvidence));
    }

    private static SingleQuestionAnswer BuildAssessmentComparisonAnswer(
        string question,
        IReadOnlyList<ScoredChunk> matches,
        IReadOnlyList<DocumentChunk> scopedChunks,
        IReadOnlyList<string> courseCodes,
        string language)
    {
        var citations = new List<SourceCitation>();
        var lines = new List<string>
        {
            language == "vi" ? "## Cơ cấu điểm theo tài liệu" : "## Assessment structure from the documents"
        };
        var coursesWithEvidence = 0;
        var hasInvalidEvidence = false;

        foreach (var courseCode in courseCodes)
        {
            var components = scopedChunks
                .Where(chunk => SubjectMatches(chunk.Subject, courseCode))
                .SelectMany(chunk => ExtractAssessmentComponents(chunk).Select(component => new
                {
                    Chunk = chunk,
                    Score = matches.FirstOrDefault(match =>
                        match.Chunk.DocumentId == chunk.DocumentId && match.Chunk.ChunkIndex == chunk.ChunkIndex)?.Score ?? 0.99,
                    component.Name,
                    component.WeightPercent,
                    component.EvidenceText
                }))
                .GroupBy(item => $"{NormalizeQuestion(item.Name)}|{item.WeightPercent:0.####}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            lines.Add($"### {courseCode}");
            if (components.Count == 0)
            {
                lines.Add(language == "vi"
                    ? "- Chưa tìm thấy thành phần và tỷ trọng điểm trong tài liệu được phép truy cập."
                    : "- No assessment components and weights were found in the authorized documents.");
                continue;
            }

            coursesWithEvidence++;
            foreach (var component in components)
            {
                var sourceNumber = AddComparisonCitation(citations, component.Chunk, component.Score, component.EvidenceText);
                lines.Add($"- {component.Name}: {FormatCreditValue(component.WeightPercent)}% [{sourceNumber}]");
            }

            var total = components.Sum(component => component.WeightPercent);
            var totalSources = string.Concat(components
                .Select(component => AddComparisonCitation(citations, component.Chunk, component.Score, component.EvidenceText))
                .Distinct()
                .Select(sourceNumber => $"[{sourceNumber}]"));
            lines.Add(language == "vi"
                ? $"- **Tổng tỷ trọng tìm thấy:** {FormatCreditValue(total)}%. {totalSources}"
                : $"- **Total weight found:** {FormatCreditValue(total)}%. {totalSources}");
            if (Math.Abs(total - 100) > 0.01)
            {
                hasInvalidEvidence = true;
                lines.Add(language == "vi"
                    ? "- ⚠️ Tổng chưa bằng 100%; kết quả có thể thiếu thành phần hoặc tài liệu đang mâu thuẫn."
                    : "- ⚠️ The total is not 100%; a component may be missing or the documents may conflict.");
            }
            var conflictingNames = components
                .GroupBy(component => NormalizeQuestion(component.Name), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Select(component => component.WeightPercent).Distinct().Count() > 1)
                .Select(group => group.First().Name)
                .ToList();
            if (conflictingNames.Count > 0)
            {
                hasInvalidEvidence = true;
                lines.Add(language == "vi"
                    ? $"- ⚠️ Tài liệu có tỷ trọng mâu thuẫn cho: {string.Join(", ", conflictingNames)}."
                    : $"- ⚠️ Conflicting weights were found for: {string.Join(", ", conflictingNames)}.");
            }
        }

        return new SingleQuestionAnswer(
            question,
            string.Join("\n", lines),
            citations,
            AnswerSource: "Rag",
            HasDirectCitation: citations.Count > 0,
            AnswerStatus: ResolveComparisonAnswerStatus(
                coursesWithEvidence == courseCodes.Count && !hasInvalidEvidence,
                coursesWithEvidence));
    }

    private static string ResolveComparisonAnswerStatus(bool isFullyGrounded, int evidenceCount)
    {
        if (isFullyGrounded)
        {
            return ChatGroundingPolicy.GroundedAnswerStatus;
        }

        return evidenceCount > 0
            ? ChatGroundingPolicy.PartialAnswerStatus
            : ChatGroundingPolicy.InsufficientEvidenceStatus;
    }

    private static SingleQuestionAnswer? TryBuildAssessmentStructureAnswer(
        string question,
        IReadOnlyList<DocumentChunk> scopedChunks,
        string language)
    {
        var normalizedQuestion = NormalizeQuestion(question);
        var asksForAllCourses = normalizedQuestion.Contains("cac mon", StringComparison.Ordinal)
                                || normalizedQuestion.Contains("tat ca mon", StringComparison.Ordinal)
                                || normalizedQuestion.Contains("all courses", StringComparison.Ordinal)
                                || normalizedQuestion.Contains("all subjects", StringComparison.Ordinal);
        var courseCodes = ExtractCourseCodesInOrder(question).Take(8).ToList();
        if (courseCodes.Count == 0 && asksForAllCourses)
        {
            courseCodes = scopedChunks
                .Select(chunk => ExtractSubjectCode(chunk.Subject))
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();
        }
        if (!IsAssessmentWeightQuestion(normalizedQuestion)
            || IsPersonalGradeQuestion(question)
            || courseCodes.Count == 0
            || (courseCodes.Count > 1 && IsMultiSubjectQuestion(normalizedQuestion) && !asksForAllCourses))
        {
            return null;
        }

        return BuildAssessmentComparisonAnswer(
            question,
            Array.Empty<ScoredChunk>(),
            scopedChunks,
            courseCodes,
            language);
    }

    private static IReadOnlyList<AssessmentComponentFact> ExtractAssessmentComponents(DocumentChunk chunk)
    {
        var results = new List<AssessmentComponentFact>();
        var lines = (chunk.Text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(line => Regex.Split(line, @"(?<=%)\s*\)?\s*[.;]\s*", RegexOptions.None, RegexTimeout))
            .Where(line => !string.IsNullOrWhiteSpace(line));
        string? currentComponent = null;
        var currentComponentAge = 0;

        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(currentComponent) && ++currentComponentAge > 4)
            {
                currentComponent = null;
                currentComponentAge = 0;
            }

            var cleaned = line.Trim().Trim('-', '*', '|').Trim();
            if (TryParseInlineAssessmentComponent(cleaned, out var inlineName, out var inlineValue))
            {
                results.Add(new AssessmentComponentFact(
                    NormalizeAssessmentComponentName(inlineName),
                    inlineValue,
                    cleaned));
                currentComponent = null;
                currentComponentAge = 0;
                continue;
            }

            var weight = Regex.Match(
                cleaned,
                @"(?i)^(?:Tỷ\s*trọng|Ty\s*trong|Trọng\s*số|Trong\s*so|Weight|Percentage|Percent)?\s*:?\s*(?<value>\d+(?:[.,]\d+)?)\s*%\s*$",
                RegexOptions.CultureInvariant,
                RegexTimeout);
            if (weight.Success && !string.IsNullOrWhiteSpace(currentComponent)
                && double.TryParse(weight.Groups["value"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                && value is >= 0 and <= 100)
            {
                results.Add(new AssessmentComponentFact(
                    NormalizeAssessmentComponentName(currentComponent),
                    value,
                    $"{currentComponent}: {cleaned}"));
                currentComponent = null;
                currentComponentAge = 0;
                continue;
            }

            if (IsPlausibleAssessmentComponentName(cleaned))
            {
                currentComponent = CleanAssessmentComponentName(cleaned);
                currentComponentAge = 0;
            }
        }

        return results
            .GroupBy(item => $"{NormalizeQuestion(item.Name)}|{item.WeightPercent:0.####}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static bool TryParseInlineAssessmentComponent(string line, out string name, out double value)
    {
        name = string.Empty;
        value = 0;
        foreach (var pattern in AssessmentComponentPatterns)
        {
            var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
            if (!match.Success)
            {
                continue;
            }

            var candidateName = CleanAssessmentComponentName(match.Groups["name"].Value);
            if (!IsPlausibleAssessmentComponentName(candidateName)
                || !double.TryParse(match.Groups["value"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var candidateValue)
                || candidateValue is < 0 or > 100)
            {
                continue;
            }

            name = candidateName;
            value = candidateValue;
            return true;
        }

        return false;
    }

    private static bool IsPlausibleAssessmentComponentName(string value)
    {
        var candidate = CleanAssessmentComponentName(value);
        if (candidate.Length is < 2 or > 100
            || candidate.Contains(':', StringComparison.Ordinal)
            || Regex.IsMatch(candidate, @"[.!?;]", RegexOptions.CultureInvariant, RegexTimeout)
            || !Regex.IsMatch(candidate, @"\p{L}", RegexOptions.CultureInvariant, RegexTimeout))
        {
            return false;
        }

        var wordCount = Regex.Matches(candidate, @"[\p{L}\p{N}]+", RegexOptions.CultureInvariant, RegexTimeout).Count;
        if (wordCount is < 1 or > 12)
        {
            return false;
        }

        var normalized = NormalizeQuestion(candidate);
        var normalizedWords = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        return !normalizedWords.Overlaps(["total", "sum", "overall", "tong"])
            && normalized is not "weight" and not "percentage" and not "percent" and not "ty trong" and not "trong so";
    }

    private static string CleanAssessmentComponentName(string name)
    {
        return Regex.Replace(name.Trim().Trim('-', '*', '|', ':', '.', ';').Trim(), @"^\d+[.)]\s*", string.Empty, RegexOptions.None, RegexTimeout).Trim();
    }

    private static string NormalizeAssessmentComponentName(string name)
    {
        var trimmed = name.Trim();
        var normalized = NormalizeQuestion(trimmed);
        if (normalized.Contains("final exam", StringComparison.Ordinal)
            || normalized.Contains("thi cuoi ky", StringComparison.Ordinal))
        {
            return "Final exam";
        }
        if (normalized.Contains("midterm", StringComparison.Ordinal)
            || normalized.Contains("thi giua ky", StringComparison.Ordinal))
        {
            return "Midterm";
        }
        if (normalized.Contains("participation", StringComparison.Ordinal)
            || normalized.Contains("tham gia", StringComparison.Ordinal))
        {
            return "Participation";
        }
        if (normalized.Contains("assignment", StringComparison.Ordinal)
            || normalized.Contains("bai tap", StringComparison.Ordinal))
        {
            return "Assignment";
        }
        if (normalized.Contains("project", StringComparison.Ordinal)
            || normalized.Contains("du an", StringComparison.Ordinal))
        {
            return "Project";
        }
        if (normalized.Contains("quiz", StringComparison.Ordinal))
        {
            return "Quiz";
        }
        if (normalized.Contains("lab", StringComparison.Ordinal)
            || normalized.Contains("thuc hanh", StringComparison.Ordinal))
        {
            return "Lab";
        }

        return Regex.Replace(trimmed, @"^\d+[.)]\s*", string.Empty, RegexOptions.None, RegexTimeout).Trim();
    }

    private static int AddComparisonCitation(List<SourceCitation> citations, ScoredChunk match, string evidenceText)
    {
        return AddComparisonCitation(citations, match.Chunk, match.Score, evidenceText);
    }

    private static int AddComparisonCitation(
        List<SourceCitation> citations,
        DocumentChunk chunk,
        double score,
        string evidenceText)
    {
        var existingIndex = citations.FindIndex(citation =>
            citation.DocumentId == chunk.DocumentId && citation.ChunkIndex == chunk.ChunkIndex);
        if (existingIndex >= 0)
        {
            return existingIndex + 1;
        }

        citations.Add(new SourceCitation
        {
            DocumentId = chunk.DocumentId,
            FileName = chunk.FileName,
            Subject = chunk.Subject,
            Chapter = chunk.Chapter,
            ChunkIndex = chunk.ChunkIndex,
            Score = Math.Round(score, 3),
            Excerpt = CreateFactExcerpt(chunk.Text, evidenceText)
        });
        return citations.Count;
    }

    private static bool IsAssessmentQuestion(string normalizedQuestion)
    {
        var usesDifferenceIdiom = normalizedQuestion.Contains("khac nhau o diem", StringComparison.Ordinal)
                                 || normalizedQuestion.Contains("khac nhau diem nao", StringComparison.Ordinal)
                                 || normalizedQuestion.Contains("diem khac nhau", StringComparison.Ordinal)
                                 || normalizedQuestion.Contains("diem giong nhau", StringComparison.Ordinal);
        return (!usesDifferenceIdiom && normalizedQuestion.Contains("diem", StringComparison.Ordinal))
               || normalizedQuestion.Contains("danh gia", StringComparison.Ordinal)
               || normalizedQuestion.Contains("ty trong", StringComparison.Ordinal)
               || normalizedQuestion.Contains("assessment", StringComparison.Ordinal)
               || normalizedQuestion.Contains("grading", StringComparison.Ordinal)
               || normalizedQuestion.Contains("weight", StringComparison.Ordinal);
    }

    private static bool IsAssessmentWeightQuestion(string normalizedQuestion)
    {
        var usesDifferenceIdiom = normalizedQuestion.Contains("khac nhau o diem", StringComparison.Ordinal)
                                 || normalizedQuestion.Contains("khac nhau diem nao", StringComparison.Ordinal)
                                 || normalizedQuestion.Contains("diem khac nhau", StringComparison.Ordinal)
                                 || normalizedQuestion.Contains("diem giong nhau", StringComparison.Ordinal);
        return (!usesDifferenceIdiom && normalizedQuestion.Contains("diem", StringComparison.Ordinal))
               || normalizedQuestion.Contains("ty trong", StringComparison.Ordinal)
               || normalizedQuestion.Contains("phan tram", StringComparison.Ordinal)
               || normalizedQuestion.Contains("grading", StringComparison.Ordinal)
               || normalizedQuestion.Contains("weight", StringComparison.Ordinal)
               || normalizedQuestion.Contains("percentage", StringComparison.Ordinal);
    }

    private static bool IsDifficultyQuestion(string normalizedQuestion)
    {
        return normalizedQuestion.Contains("de hon", StringComparison.Ordinal)
               || normalizedQuestion.Contains("kho hon", StringComparison.Ordinal)
               || normalizedQuestion.Contains("do kho", StringComparison.Ordinal)
               || normalizedQuestion.Contains("easier", StringComparison.Ordinal)
               || normalizedQuestion.Contains("harder", StringComparison.Ordinal)
               || normalizedQuestion.Contains("difficulty", StringComparison.Ordinal);
    }

    private static bool IsPersonalGradeQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("diem cua toi", StringComparison.Ordinal)
               || normalized.Contains("diem cua minh", StringComparison.Ordinal)
               || normalized.Contains("diem ca nhan", StringComparison.Ordinal)
               || normalized.Contains("my grade", StringComparison.Ordinal)
               || normalized.Contains("my score", StringComparison.Ordinal)
               || normalized.Contains("personal grade", StringComparison.Ordinal);
    }

    private static StudentPreferenceProfile? BuildStudentPreferenceProfile(string normalizedQuestion)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var labelsVi = new List<string>();
        var labelsEn = new List<string>();

        AddPreference(
            normalizedQuestion,
            ["lap trinh", "code", "coding", "programming", "software", "dotnet", ".net", "java", "web", "database", "co so du lieu"],
            ["programming", "coding", "code", "software", "dotnet", "java", "web", "database", "lap", "trinh", "phan", "mem", "du", "lieu"],
            "lập trình/phần mềm",
            "programming/software",
            terms,
            labelsVi,
            labelsEn);
        AddPreference(
            normalizedQuestion,
            ["phan cung", "hardware", "iot", "sensor", "cam bien", "thiet bi"],
            ["hardware", "iot", "sensor", "device", "embedded", "phan", "cung", "cam", "bien", "thiet", "bi"],
            "phần cứng/IoT",
            "hardware/IoT",
            terms,
            labelsVi,
            labelsEn);
        AddPreference(
            normalizedQuestion,
            ["am nhac", "music", "nhac cu", "instrument", "nghe thuat", "art"],
            ["music", "instrument", "performance", "art", "am", "nhac", "nhac", "cu", "nghe", "thuat"],
            "âm nhạc/nghệ thuật",
            "music/arts",
            terms,
            labelsVi,
            labelsEn);
        AddPreference(
            normalizedQuestion,
            ["thuc hanh", "hands on", "practical", "lab", "project", "du an"],
            ["practice", "practical", "lab", "laboratory", "project", "hands", "thuc", "hanh", "du", "an"],
            "thực hành/dự án",
            "hands-on/project work",
            terms,
            labelsVi,
            labelsEn);
        AddPreference(
            normalizedQuestion,
            ["ly thuyet", "theory", "toan", "math", "analysis", "phan tich"],
            ["theory", "theoretical", "math", "mathematics", "analysis", "ly", "thuyet", "toan", "phan", "tich"],
            "lý thuyết/phân tích",
            "theory/analysis",
            terms,
            labelsVi,
            labelsEn);

        return terms.Count == 0
            ? null
            : new StudentPreferenceProfile(
                string.Join(", ", labelsVi.Distinct(StringComparer.OrdinalIgnoreCase)),
                string.Join(", ", labelsEn.Distinct(StringComparer.OrdinalIgnoreCase)),
                terms);
    }

    private static void AddPreference(
        string normalizedQuestion,
        IReadOnlyList<string> signals,
        IReadOnlyList<string> evidenceTerms,
        string labelVi,
        string labelEn,
        HashSet<string> terms,
        List<string> labelsVi,
        List<string> labelsEn)
    {
        if (!signals.Any(signal => normalizedQuestion.Contains(signal, StringComparison.Ordinal)))
        {
            return;
        }

        terms.UnionWith(evidenceTerms);
        labelsVi.Add(labelVi);
        labelsEn.Add(labelEn);
    }

    private static SingleQuestionAnswer? TryBuildMultiSubjectCreditComparison(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language)
    {
        if (DetectExactFactIntent(question) != ExactFactIntent.Credits
            || !IsMultiSubjectQuestion(NormalizeQuestion(question)))
        {
            return null;
        }

        var courseCodes = Regex.Matches(question, @"\b[A-Za-z]{2,}\d{2,}\b", RegexOptions.CultureInvariant, RegexTimeout)
            .Select(match => match.Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (courseCodes.Count < 2)
        {
            return null;
        }

        var facts = courseCodes.Select(code =>
        {
            var evidence = chunks
                .Where(chunk => SubjectMatches(chunk.Subject, code))
                .Select(chunk => new { Chunk = chunk, Credit = TryExtractCreditFact(chunk.Text) })
                .Where(item => item.Credit is not null)
                .OrderBy(item => item.Chunk.ChunkIndex)
                .FirstOrDefault();
            return new CourseCreditComparisonFact(
                code,
                evidence?.Credit?.Value,
                evidence?.Credit?.EvidenceText,
                evidence?.Chunk);
        }).ToList();
        if (facts.All(item => item.Credit is null))
        {
            return null;
        }

        var citations = new List<SourceCitation>();
        var sourceNumbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var fact in facts.Where(item => item.Credit is not null && item.Chunk is not null))
        {
            sourceNumbers[fact.CourseCode] = citations.Count + 1;
            citations.Add(new SourceCitation
            {
                DocumentId = fact.Chunk!.DocumentId,
                FileName = fact.Chunk.FileName,
                Subject = fact.Chunk.Subject,
                Chapter = fact.Chunk.Chapter,
                ChunkIndex = fact.Chunk.ChunkIndex,
                Score = 0.99,
                Excerpt = CreateFactExcerpt(fact.Chunk.Text, fact.EvidenceText ?? string.Empty)
            });
        }

        var lines = new List<string> { language == "vi" ? "So sánh số tín chỉ:" : "Credit comparison:" };
        foreach (var fact in facts)
        {
            if (fact.Credit is not null && sourceNumbers.TryGetValue(fact.CourseCode, out var sourceNumber))
            {
                var factText = language == "vi"
                    ? $"{fact.CourseCode}: {FormatCreditValue(fact.Credit.Value)} tín chỉ."
                    : $"{fact.CourseCode}: {FormatCreditValue(fact.Credit.Value)} credits.";
                lines.Add($"- {AttachSourceMarker(factText, sourceNumber)}");
            }
            else
            {
                lines.Add(language == "vi"
                    ? $"- {fact.CourseCode}: Chưa tìm thấy số tín chỉ trong tài liệu được phép truy cập."
                    : $"- {fact.CourseCode}: No credit value was found in the authorized documents.");
            }
        }

        var hasAllFacts = facts.All(item => item.Credit is not null);
        if (hasAllFacts)
        {
            var first = facts[0];
            var second = facts[1];
            var comparisonSources = string.Concat(facts.Select(fact => $"[{sourceNumbers[fact.CourseCode]}]"));
            var allCreditsEqual = facts.All(fact => Math.Abs(fact.Credit!.Value - first.Credit!.Value) < 0.0001);
            if (allCreditsEqual)
            {
                var courseList = string.Join(", ", facts.Select(fact => fact.CourseCode));
                lines.Add(language == "vi"
                    ? $"Kết luận: {courseList} có số tín chỉ bằng nhau. {comparisonSources}"
                    : $"Conclusion: {courseList} have the same number of credits. {comparisonSources}");
            }
            else if (facts.Count == 2)
            {
                var firstIsLarger = first.Credit!.Value > second.Credit!.Value;
                var larger = firstIsLarger ? first : second;
                var smaller = firstIsLarger ? second : first;
                var difference = Math.Abs(first.Credit!.Value - second.Credit!.Value);
                lines.Add(language == "vi"
                    ? $"Kết luận: {larger.CourseCode} nhiều hơn {smaller.CourseCode} {FormatCreditValue(difference)} tín chỉ. {comparisonSources}"
                    : $"Conclusion: {larger.CourseCode} has {FormatCreditValue(difference)} more credits than {smaller.CourseCode}. {comparisonSources}");
            }
            else
            {
                var maximum = facts.Max(fact => fact.Credit!.Value);
                var minimum = facts.Min(fact => fact.Credit!.Value);
                var highest = string.Join(", ", facts.Where(fact => Math.Abs(fact.Credit!.Value - maximum) < 0.0001).Select(fact => fact.CourseCode));
                var lowest = string.Join(", ", facts.Where(fact => Math.Abs(fact.Credit!.Value - minimum) < 0.0001).Select(fact => fact.CourseCode));
                lines.Add(language == "vi"
                    ? $"Kết luận: cao nhất là {highest} ({FormatCreditValue(maximum)} tín chỉ); thấp nhất là {lowest} ({FormatCreditValue(minimum)} tín chỉ). {comparisonSources}"
                    : $"Conclusion: {highest} has the highest value ({FormatCreditValue(maximum)} credits); {lowest} has the lowest ({FormatCreditValue(minimum)} credits). {comparisonSources}");
            }
        }

        return new SingleQuestionAnswer(
            question,
            string.Join("\n", lines),
            citations,
            AnswerSource: "Rag",
            HasDirectCitation: citations.Count > 0,
            AnswerStatus: hasAllFacts
                ? ChatGroundingPolicy.GroundedAnswerStatus
                : ChatGroundingPolicy.PartialAnswerStatus);
    }

    private static SingleQuestionAnswer? TryBuildLecturerNameAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language)
    {
        foreach (var chunk in chunks.OrderBy(item => item.ChunkIndex))
        {
            var chunkText = chunk.Text ?? string.Empty;
            var match = Regex.Match(
                chunkText,
                @"(?i)\b(?:Giảng\s+viên(?:\s+phụ\s+trách)?|Lecturer|Instructor|Teacher)\s*:\s*(?<name>.+?)(?=\s+(?:Số\s+tín\s+chỉ|Credits?|Mã\s+môn|Tên\s+môn|Subject|Chapter)\s*:|\s*=+|$)",
                RegexOptions.CultureInvariant,
                RegexTimeout);
            if (!match.Success)
            {
                continue;
            }

            var lecturerName = match.Groups["name"].Value.Trim();
            if (string.IsNullOrWhiteSpace(lecturerName))
            {
                continue;
            }

            var courseLabel = ResolveFactCourseLabel(question, chunk);
            var answer = language == "vi"
                ? $"Theo tài liệu, giảng viên phụ trách {courseLabel} là {lecturerName}."
                : $"According to the document, the lecturer for {courseLabel} is {lecturerName}.";
            return new SingleQuestionAnswer(
                question,
                answer,
                [new SourceCitation
                {
                    DocumentId = chunk.DocumentId,
                    FileName = chunk.FileName,
                    Subject = chunk.Subject,
                    Chapter = chunk.Chapter,
                    ChunkIndex = chunk.ChunkIndex,
                    Score = 0.99,
                    Excerpt = CreateFactExcerpt(chunkText, match.Value.Trim())
                }],
                ResolveSubject([chunk]));
        }

        return null;
    }

    private static SingleQuestionAnswer? TryBuildAttendanceAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language)
    {
        var patterns = new[]
        {
            @"(?i)\bTham\s+dự\s+(?:tối\s+thiểu|ít\s+nhất)\s+(?<value>\d+(?:[.,]\d+)?)\s*%",
            @"(?i)\b(?:minimum\s+attendance|attend\s+at\s+least)\s*(?:is|:)?\s*(?<value>\d+(?:[.,]\d+)?)\s*%"
        };

        foreach (var chunk in chunks.OrderBy(item => item.ChunkIndex))
        {
            var chunkText = chunk.Text ?? string.Empty;
            var match = patterns
                .Select(pattern => Regex.Match(chunkText, pattern, RegexOptions.CultureInvariant, RegexTimeout))
                .FirstOrDefault(candidate => candidate.Success);
            if (match is null || !match.Success)
            {
                continue;
            }

            var rawValue = match.Groups["value"].Value.Replace(',', '.');
            if (!double.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var percentage))
            {
                continue;
            }

            var courseLabel = ResolveFactCourseLabel(question, chunk);
            var formattedPercentage = FormatCreditValue(percentage);
            var answer = language == "vi"
                ? $"Theo tài liệu, sinh viên {courseLabel} cần tham dự tối thiểu {formattedPercentage}% thời lượng môn học."
                : $"According to the document, {courseLabel} students must attend at least {formattedPercentage}% of the course.";
            return new SingleQuestionAnswer(
                question,
                answer,
                [new SourceCitation
                {
                    DocumentId = chunk.DocumentId,
                    FileName = chunk.FileName,
                    Subject = chunk.Subject,
                    Chapter = chunk.Chapter,
                    ChunkIndex = chunk.ChunkIndex,
                    Score = 0.99,
                    Excerpt = CreateFactExcerpt(chunkText, match.Value.Trim())
                }],
                ResolveSubject([chunk]));
        }

        return null;
    }

    private static SingleQuestionAnswer? TryBuildSubjectOverviewAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language)
    {
        if (!IsSubjectOverviewQuestion(question) || chunks.Count == 0)
        {
            return null;
        }

        var evidence = chunks
            .OrderBy(chunk => chunk.ChunkIndex)
            .FirstOrDefault(chunk => !string.IsNullOrWhiteSpace(chunk.Subject) || !string.IsNullOrWhiteSpace(chunk.Text));
        if (evidence is null)
        {
            return null;
        }

        var subject = string.IsNullOrWhiteSpace(evidence.Subject) ? ResolveSubject(chunks) : evidence.Subject.Trim();
        var subjectCode = ExtractSubjectCode(subject);
        var subjectName = ResolveSubjectName(subject, evidence.Text);
        var credit = TryExtractCreditFact(evidence.Text);
        var displayCode = string.IsNullOrWhiteSpace(subjectCode) ? subject : subjectCode;
        var displayName = string.IsNullOrWhiteSpace(subjectName) ? subject : subjectName;

        var answer = language == "vi"
            ? $"Mình thấy trong syllabus: {displayCode} là môn {displayName}."
            : $"I found this in the syllabus: {displayCode} is the course {displayName}.";
        if (credit is not null)
        {
            answer += language == "vi"
                ? $" Môn này có {FormatCreditValue(credit.Value)} tín chỉ."
                : $" This course has {FormatCreditValue(credit.Value)} credits.";
        }

        var citations = new[]
        {
            new SourceCitation
            {
                DocumentId = evidence.DocumentId,
                FileName = evidence.FileName,
                Subject = evidence.Subject,
                Chapter = evidence.Chapter,
                ChunkIndex = evidence.ChunkIndex,
                Score = 0.96,
                Excerpt = CreateExcerpt(evidence.Text)
            }
        };

        return new SingleQuestionAnswer(question, answer, citations, subject);
    }

    private static SingleQuestionAnswer? TryBuildSubjectSkillsAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language)
    {
        var normalizedQuestion = NormalizeQuestion(question);
        var asksForSkills = normalizedQuestion.Contains("ky nang", StringComparison.Ordinal)
                            || normalizedQuestion.Contains("skill", StringComparison.Ordinal)
                            || (normalizedQuestion.Contains("phat trien", StringComparison.Ordinal)
                                && normalizedQuestion.Contains("sinh vien", StringComparison.Ordinal));
        if (!asksForSkills)
        {
            return null;
        }

        foreach (var chunk in chunks.OrderBy(item => item.ChunkIndex))
        {
            var chunkText = chunk.Text ?? string.Empty;
            var section = Regex.Match(
                chunkText,
                @"(?i)KỸ\s+NĂNG\s+MỀM\s+VÀ\s+PHÁT\s+TRIỂN\s+CÁ\s+NHÂN\s*=+\s*(?<body>.+?)(?=\s*=+\s*[\p{L}\p{N}\s]+\s*=+|$)",
                RegexOptions.CultureInvariant,
                RegexTimeout);
            if (!section.Success)
            {
                continue;
            }

            var body = section.Groups["body"].Value.Trim();
            var colonIndex = body.IndexOf(':');
            if (colonIndex >= 0 && colonIndex + 1 < body.Length)
            {
                body = body[(colonIndex + 1)..].Trim();
            }

            var skills = body
                .Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim().Trim('-', '*', ' ').TrimEnd('.'))
                .Where(item => item.Length is > 5 and <= 240)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();
            if (skills.Count == 0)
            {
                continue;
            }

            var courseLabel = ResolveFactCourseLabel(question, chunk);
            var answer = language == "vi"
                ? $"Theo tài liệu, {courseLabel} giúp sinh viên phát triển:\n" + string.Join("\n", skills.Select(item => $"- {item}."))
                : $"According to the document, {courseLabel} helps students develop:\n" + string.Join("\n", skills.Select(item => $"- {item}."));
            return new SingleQuestionAnswer(
                question,
                answer,
                [new SourceCitation
                {
                    DocumentId = chunk.DocumentId,
                    FileName = chunk.FileName,
                    Subject = chunk.Subject,
                    Chapter = chunk.Chapter,
                    ChunkIndex = chunk.ChunkIndex,
                    Score = 0.98,
                    Excerpt = CreateExcerpt(section.Value)
                }],
                ResolveSubject([chunk]));
        }

        return null;
    }

    private static bool IsSubjectOverviewQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        var normalizedCourseCodes = ExtractCourseCodes(question)
            .Select(NormalizeQuestion)
            .ToList();
        return normalizedCourseCodes.Any(code =>
                   normalized.EndsWith($"{code} la gi", StringComparison.Ordinal)
                   || normalized.EndsWith($"{code} la mon gi", StringComparison.Ordinal)
                   || normalized.Equals($"what is {code}", StringComparison.Ordinal)
                   || normalized.Equals($"what is course {code}", StringComparison.Ordinal)
                   || normalized.Equals($"tell me about {code}", StringComparison.Ordinal))
               || (normalized.StartsWith("mon ", StringComparison.Ordinal)
                   && normalized.EndsWith(" la mon gi", StringComparison.Ordinal));
    }

    private static string ResolveSubjectName(string subject, string text)
    {
        var syllabusName = Regex.Match(
            text ?? string.Empty,
            @"(?i)\bSyllabus\s*Name\s*:\s*(?<name>.+?)(?=\s+(?:Subject|Course|Credits?|No\s*Credit|Chapter)\s*:|\s*=+|$)",
            RegexOptions.CultureInvariant,
            RegexTimeout);
        if (syllabusName.Success)
        {
            return syllabusName.Groups["name"].Value.Trim();
        }

        var vietnameseName = Regex.Match(
            text ?? string.Empty,
            @"(?i)\bTên\s+môn\s*:\s*(?<name>.+?)(?=\s+(?:Tên\s+tiếng\s+Anh|Mã\s+môn|Số\s+tín\s+chỉ|Trình\s+độ)\s*:|\s*=+|$)",
            RegexOptions.CultureInvariant,
            RegexTimeout);
        if (vietnameseName.Success)
        {
            return vietnameseName.Groups["name"].Value.Trim();
        }

        var separatorIndex = subject.IndexOf('-', StringComparison.Ordinal);
        return separatorIndex >= 0 && separatorIndex + 1 < subject.Length
            ? subject[(separatorIndex + 1)..].Trim()
            : subject.Trim();
    }

    private static ExactFactIntent DetectExactFactIntent(string question)
    {
        var normalized = NormalizeQuestion(question);
        if (normalized.Contains("tin chi", StringComparison.Ordinal)
            || normalized.Contains("credit", StringComparison.Ordinal)
            || normalized.Contains("nocredit", StringComparison.Ordinal))
        {
            return ExactFactIntent.Credits;
        }

        var asksForPersonName = normalized.Contains("ten", StringComparison.Ordinal)
                                || normalized.Contains("who", StringComparison.Ordinal)
                                || Regex.IsMatch(normalized, @"\bai\b", RegexOptions.CultureInvariant, RegexTimeout);
        var asksForLecturer = normalized.Contains("giang vien", StringComparison.Ordinal)
                             || normalized.Contains("lecturer", StringComparison.Ordinal)
                             || normalized.Contains("instructor", StringComparison.Ordinal)
                             || normalized.Contains("teacher", StringComparison.Ordinal);
        if (asksForPersonName && asksForLecturer)
        {
            return ExactFactIntent.LecturerName;
        }

        var asksForAttendance = normalized.Contains("tham du", StringComparison.Ordinal)
                                || normalized.Contains("chuyen can", StringComparison.Ordinal)
                                || normalized.Contains("attendance", StringComparison.Ordinal)
                                || normalized.Contains("attend", StringComparison.Ordinal);
        var asksForThreshold = normalized.Contains("phan tram", StringComparison.Ordinal)
                               || normalized.Contains("percent", StringComparison.Ordinal)
                               || normalized.Contains("toi thieu", StringComparison.Ordinal)
                               || normalized.Contains("it nhat", StringComparison.Ordinal)
                               || question.Contains('%');
        if (asksForAttendance && asksForThreshold)
        {
            return ExactFactIntent.AttendancePercentage;
        }

        return ExactFactIntent.None;
    }

    private static CreditFact? TryExtractCreditFact(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var patterns = new[]
        {
            @"(?i)\bNo\s*Credit\s*:\s*(?<value>\d+(?:[.,]\d+)?)\b",
            @"(?i)\bNoCredit\s*:\s*(?<value>\d+(?:[.,]\d+)?)\b",
            @"(?i)\b(?:Credits?|Credit)\s*:\s*(?<value>\d+(?:[.,]\d+)?)\b",
            @"(?i)\b(?:So|S[oố])\s*t[ií]n\s*ch[iỉ]\s*:\s*(?<value>\d+(?:[.,]\d+)?)\b",
            @"(?i)\b(?<value>\d+(?:[.,]\d+)?)\s*(?:t[ií]n\s*ch[iỉ]|credits?)\b"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant, RegexTimeout);
            if (!match.Success)
            {
                continue;
            }

            var rawValue = match.Groups["value"].Value.Replace(',', '.');
            if (double.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                return new CreditFact(value, match.Value.Trim());
            }
        }

        return null;
    }

    private static IReadOnlySet<string> ExtractCourseCodes(string text)
    {
        return ExtractCourseCodesInOrder(text).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ExtractCourseCodesInOrder(string text)
    {
        return Regex.Matches(text ?? string.Empty, @"\b[A-Za-z]{2,}\d{2,}\b", RegexOptions.CultureInvariant, RegexTimeout)
            .Select(match => match.Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveFactCourseLabel(string question, DocumentChunk chunk)
    {
        var code = ExtractCourseCodes(question).FirstOrDefault()
                   ?? ExtractCourseCodes(chunk.Subject).FirstOrDefault();
        return string.IsNullOrWhiteSpace(code) ? chunk.Subject : code;
    }

    private static string FormatCreditValue(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.0001
            ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string CreateFactExcerpt(string text, string evidenceText)
    {
        var sourceText = text ?? string.Empty;
        var lines = sourceText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries);
        var evidenceIndex = Array.FindIndex(lines, line => line.Equals(evidenceText, StringComparison.OrdinalIgnoreCase));
        if (evidenceIndex < 0)
        {
            return string.IsNullOrWhiteSpace(evidenceText) ? CreateExcerpt(sourceText) : evidenceText;
        }

        var start = Math.Max(0, evidenceIndex - 2);
        var end = Math.Min(lines.Length - 1, evidenceIndex + 2);
        var excerpt = string.Join(" ", lines[start..(end + 1)].Where(line => !string.IsNullOrWhiteSpace(line)));
        return CreateExcerpt(excerpt);
    }

    private async Task<IReadOnlyList<ScoredChunk>> RerankMatchesAsync(
        string question,
        IReadOnlyList<ScoredChunk> candidates,
        string language,
        CancellationToken cancellationToken)
    {
        var fallback = RerankLocally(question, candidates);
        if (candidates.Count == 0)
        {
            return fallback;
        }

        if (!_chatCompletionService.IsEnabled)
        {
            return fallback;
        }

        IReadOnlyList<ChatChunkRerankResult> reranked;
        try
        {
            reranked = await _chatCompletionService.RerankChunksAsync(
                question,
                candidates.Select(item => item.Chunk).ToList(),
                language,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return fallback;
        }

        if (reranked.Count == 0)
        {
            return Array.Empty<ScoredChunk>();
        }

        var selected = new List<ScoredChunk>();
        var seen = new HashSet<int>();
        foreach (var decision in reranked.OrderByDescending(item => item.Score))
        {
            var candidateIndex = decision.CandidateNumber - 1;
            if (candidateIndex < 0 || candidateIndex >= candidates.Count || !seen.Add(candidateIndex))
            {
                continue;
            }

            var candidate = candidates[candidateIndex];
            var llmConfidence = Math.Clamp(decision.Score, 0, 1);
            if (llmConfidence < MinimumLlmRerankScore)
            {
                continue;
            }

            var boostedScore = Math.Round(Math.Max(candidate.Score, 0.82 + (llmConfidence * 0.18)), 3);
            selected.Add(candidate with { Score = boostedScore });
            if (selected.Count == TopK)
            {
                break;
            }
        }

        return SelectWithEvidenceCoverage(
            selected.Concat(fallback).ToList(),
            BuildComparisonEvidenceSlots(question),
            ExtractCourseCodes(question),
            TopK);
    }

    private static IReadOnlyList<ScoredChunk> RerankLocally(
        string question,
        IReadOnlyList<ScoredChunk> candidates)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<ScoredChunk>();
        }

        var queryTerms = ExtractTerms(question);
        var contentTerms = RemoveCourseScopeTerms(queryTerms);
        var factIntent = DetectExactFactIntent(question);
        var courseCodes = ExtractCourseCodes(question);

        var ranked = candidates
            .Select(item =>
            {
                var rerankScore = CalculateLocalRerankScore(item, queryTerms, contentTerms, factIntent, courseCodes);
                return item with { Score = Math.Round(Math.Max(item.Score, rerankScore), 3) };
            })
            .Where(item => HasStrongLocalRerankEvidence(item, contentTerms))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.ContentSharedTerms)
            .ThenByDescending(item => item.TextSharedTerms)
            .ThenByDescending(item => item.MetadataSharedTerms)
            .ThenByDescending(item => item.VectorScore)
            .ToList();

        return SelectWithCourseCoverage(ranked, courseCodes, TopK);
    }

    private static IReadOnlyList<ScoredChunk> SelectWithCourseCoverage(
        IReadOnlyList<ScoredChunk> rankedCandidates,
        IReadOnlySet<string> courseCodes,
        int limit)
    {
        if (rankedCandidates.Count == 0 || limit <= 0)
        {
            return Array.Empty<ScoredChunk>();
        }

        var selected = new List<ScoredChunk>(Math.Min(limit, rankedCandidates.Count));
        var seenChunks = new HashSet<(Guid DocumentId, int ChunkIndex)>();

        // Reserve one qualified evidence slot per requested course before global truncation.
        foreach (var courseCode in courseCodes)
        {
            var representative = rankedCandidates.FirstOrDefault(candidate =>
                SubjectMatches(candidate.Chunk.Subject, courseCode));
            if (representative is null
                || !seenChunks.Add((representative.Chunk.DocumentId, representative.Chunk.ChunkIndex)))
            {
                continue;
            }

            selected.Add(representative);
            if (selected.Count == limit)
            {
                return selected;
            }
        }

        foreach (var candidate in rankedCandidates)
        {
            if (!seenChunks.Add((candidate.Chunk.DocumentId, candidate.Chunk.ChunkIndex)))
            {
                continue;
            }

            selected.Add(candidate);
            if (selected.Count == limit)
            {
                break;
            }
        }

        return selected;
    }

    private static IReadOnlyList<ScoredChunk> SelectWithEvidenceCoverage(
        IReadOnlyList<ScoredChunk> rankedCandidates,
        IReadOnlyList<ComparisonEvidenceSlot> slots,
        IReadOnlySet<string> courseCodes,
        int limit)
    {
        if (rankedCandidates.Count == 0 || limit <= 0 || slots.Count == 0)
        {
            return SelectWithCourseCoverage(rankedCandidates, courseCodes, limit);
        }

        var selected = new List<ScoredChunk>(Math.Min(limit, rankedCandidates.Count));
        var seen = new HashSet<(Guid DocumentId, int ChunkIndex)>();
        foreach (var slot in slots)
        {
            var representative = rankedCandidates.FirstOrDefault(candidate =>
                SubjectMatches(candidate.Chunk.Subject, slot.CourseCode)
                && (CountSharedTerms(slot.Terms, candidate.Chunk.Text) > 0
                    || CountSharedTerms(slot.Terms, BuildChunkMetadataText(candidate.Chunk)) > 0));
            if (representative is null
                || !seen.Add((representative.Chunk.DocumentId, representative.Chunk.ChunkIndex)))
            {
                continue;
            }

            selected.Add(representative);
            if (selected.Count == limit)
            {
                return selected;
            }
        }

        foreach (var candidate in SelectWithCourseCoverage(rankedCandidates, courseCodes, limit))
        {
            if (!seen.Add((candidate.Chunk.DocumentId, candidate.Chunk.ChunkIndex)))
            {
                continue;
            }

            selected.Add(candidate);
            if (selected.Count == limit)
            {
                return selected;
            }
        }

        return selected;
    }

    private static IReadOnlyList<ScoredChunk> RankByReciprocalRankFusion(IReadOnlyList<ScoredChunk> candidates)
    {
        if (candidates.Count <= 1)
        {
            return candidates;
        }

        const double rankConstant = 60;
        var vectorRanks = candidates
            .OrderByDescending(candidate => candidate.VectorScore)
            .Select((candidate, index) => new { Key = (candidate.Chunk.DocumentId, candidate.Chunk.ChunkIndex), Rank = index + 1 })
            .ToDictionary(item => item.Key, item => item.Rank);
        var lexicalRanks = candidates
            .OrderByDescending(candidate => candidate.LexicalScore)
            .Select((candidate, index) => new { Key = (candidate.Chunk.DocumentId, candidate.Chunk.ChunkIndex), Rank = index + 1 })
            .ToDictionary(item => item.Key, item => item.Rank);

        return candidates
            .OrderByDescending(candidate =>
            {
                var key = (candidate.Chunk.DocumentId, candidate.Chunk.ChunkIndex);
                return (1d / (rankConstant + vectorRanks[key])) + (1d / (rankConstant + lexicalRanks[key]));
            })
            .ThenByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.ContentSharedTerms)
            .ToList();
    }

    private static IReadOnlyList<ComparisonEvidenceSlot> BuildComparisonEvidenceSlots(string question)
    {
        var normalized = NormalizeQuestion(question);
        var courseCodes = ExtractCourseCodesInOrder(question).Take(4).ToList();
        if (courseCodes.Count < 2 || !IsMultiSubjectQuestion(normalized))
        {
            return Array.Empty<ComparisonEvidenceSlot>();
        }

        var dimensions = new List<(string Name, string Terms)>();
        if (IsAssessmentQuestion(normalized))
        {
            dimensions.Add(("assessment", "assessment grading diem danh gia weight ty trong assignment quiz project exam final participation"));
        }
        if (IsDifficultyQuestion(normalized)
            || normalized.Contains("workload", StringComparison.Ordinal)
            || normalized.Contains("khoi luong", StringComparison.Ordinal)
            || normalized.Contains("bai tap", StringComparison.Ordinal)
            || normalized.Contains("thuc hanh", StringComparison.Ordinal))
        {
            dimensions.Add(("workload", "workload prerequisite assignment project lab practice session khoi luong dieu kien bai tap du an thuc hanh buoi"));
        }
        if (normalized.Contains("skill", StringComparison.Ordinal)
            || normalized.Contains("outcome", StringComparison.Ordinal)
            || normalized.Contains("clo", StringComparison.Ordinal)
            || normalized.Contains("ky nang", StringComparison.Ordinal)
            || normalized.Contains("chuan dau ra", StringComparison.Ordinal))
        {
            dimensions.Add(("outcomes", "skill outcome clo objective ky nang chuan dau ra muc tieu"));
        }
        if (normalized.Contains("credit", StringComparison.Ordinal)
            || normalized.Contains("tin chi", StringComparison.Ordinal))
        {
            dimensions.Add(("credits", "credit credits tin chi"));
        }
        if (dimensions.Count == 0)
        {
            dimensions.Add(("overview", "muc tieu noi dung skill ky nang assessment danh gia workload thuc hanh objective content"));
        }

        return courseCodes
            .SelectMany(courseCode => dimensions.Select(dimension => new ComparisonEvidenceSlot(
                courseCode,
                dimension.Name,
                ExtractTerms(dimension.Terms))))
            .Take(8)
            .ToList();
    }

    private static double CalculateLocalRerankScore(
        ScoredChunk candidate,
        IReadOnlySet<string> queryTerms,
        IReadOnlySet<string> contentTerms,
        ExactFactIntent factIntent,
        IReadOnlySet<string> courseCodes)
    {
        var textCoverage = queryTerms.Count == 0 ? 0 : candidate.TextSharedTerms / (double)queryTerms.Count;
        var contentCoverage = contentTerms.Count == 0 ? textCoverage : candidate.ContentSharedTerms / (double)contentTerms.Count;
        var metadataCoverage = queryTerms.Count == 0 ? 0 : Math.Min(1, candidate.MetadataSharedTerms / (double)Math.Min(queryTerms.Count, 4));
        var factBoost = factIntent == ExactFactIntent.Credits && TryExtractCreditFact(candidate.Chunk.Text) is not null ? 0.18 : 0;
        var courseCodeBoost = courseCodes.Count > 0 && CountSharedTerms(courseCodes, BuildChunkMetadataText(candidate.Chunk)) > 0 ? 0.08 : 0;

        return Clamp01(
            (candidate.Score * 0.42)
            + (candidate.LexicalScore * 0.28)
            + (contentCoverage * 0.18)
            + (textCoverage * 0.06)
            + (metadataCoverage * 0.06)
            + factBoost
            + courseCodeBoost);
    }

    private static bool HasStrongLocalRerankEvidence(
        ScoredChunk candidate,
        IReadOnlySet<string> contentTerms)
    {
        if (candidate.Score < MinimumLocalRerankScore)
        {
            return false;
        }

        if (contentTerms.Count == 0)
        {
            return candidate.TextSharedTerms > 0 || candidate.VectorScore >= 0.72;
        }

        var requiredContentTerms = contentTerms.Count >= 3 ? 2 : 1;
        return candidate.ContentSharedTerms >= requiredContentTerms
               || (candidate.ContentSharedTerms > 0 && candidate.TextSharedTerms >= requiredContentTerms)
               || (candidate.MetadataSharedTerms > 0 && candidate.TextSharedTerms > 0 && candidate.VectorScore >= 0.62);
    }

    private static IReadOnlyList<string> SplitQuestionBatch(string input)
    {
        var lines = input
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanQuestionLine)
            .Where(IsLikelyQuestion)
            .ToList();

        if (lines.Count > 1)
        {
            return lines;
        }

        var inlineQuestions = Regex.Matches(input, @"[^?？！]+[?？！]", RegexOptions.None, RegexTimeout)
            .Select(match => CleanQuestionLine(match.Value))
            .Where(IsLikelyQuestion)
            .ToList();

        return inlineQuestions.Count > 1 ? inlineQuestions : Array.Empty<string>();
    }

    private static string CleanQuestionLine(string line)
    {
        var cleaned = Regex.Replace(line.Trim(), @"^\s*(?:[-*•]|\d+[\).\:-])\s*", string.Empty, RegexOptions.None, RegexTimeout);
        var pipeIndex = cleaned.IndexOf('|', StringComparison.Ordinal);
        if (pipeIndex > 0)
        {
            cleaned = cleaned[..pipeIndex].Trim();
        }

        return cleaned.Trim();
    }

    private static bool IsLikelyQuestion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.EndsWith('?') || value.EndsWith('\uFF1F') || value.EndsWith('!'))
        {
            return true;
        }

        var normalized = NormalizeQuestion(value);
        return normalized.Contains(" la gi", StringComparison.Ordinal)
               || normalized.Contains("bao nhieu", StringComparison.Ordinal)
               || normalized.Contains("nhu the nao", StringComparison.Ordinal)
               || normalized.Contains("noi dung nao", StringComparison.Ordinal)
               || normalized.Contains("yeu cau", StringComparison.Ordinal)
               || normalized.Contains("what", StringComparison.Ordinal)
               || normalized.Contains("how", StringComparison.Ordinal)
               || normalized.Contains("which", StringComparison.Ordinal);
    }

    private static string FormatBatchAnswer(
        IReadOnlyList<SingleQuestionAnswer> answers,
        int skippedQuestionCount,
        string language)
    {
        var builder = new StringBuilder();
        builder.AppendLine(language == "vi"
            ? $"Mình nhận {answers.Count} câu hỏi. Trả lời lần lượt:"
            : $"I received {answers.Count} questions. Here are the answers in order:");
        builder.AppendLine();

        for (var index = 0; index < answers.Count; index++)
        {
            var item = answers[index];
            builder.AppendLine($"{index + 1}. {item.Question}");
            builder.AppendLine(item.Answer.Trim());
            builder.AppendLine();
        }

        if (skippedQuestionCount > 0)
        {
            builder.AppendLine(language == "vi"
                ? $"Mình chỉ xử lý tối đa {MaxBatchQuestions} câu mỗi lần, còn {skippedQuestionCount} câu chưa xử lý. Hãy gửi tiếp phần còn lại ở tin nhắn sau."
                : $"I only process up to {MaxBatchQuestions} questions per message. {skippedQuestionCount} questions were not processed; send them in the next message.");
        }

        return builder.ToString().Trim();
    }

    private static IReadOnlyList<SourceCitation> MergeCitations(IEnumerable<SourceCitation> citations)
    {
        return citations
            .GroupBy(item => new { item.DocumentId, item.ChunkIndex })
            .Select(group => group.First())
            .OrderByDescending(item => item.Score)
            .Take(20)
            .ToList();
    }

    private sealed record SingleQuestionAnswer(
        string Question,
        string Answer,
        IReadOnlyList<SourceCitation> Citations,
        string? ResolvedSubject = null,
        bool NeedsClarification = false,
        IReadOnlyList<string>? SubjectOptions = null,
        string AnswerSource = "Rag",
        bool HasDirectCitation = true,
        string? FallbackModel = null,
        string? AnswerStatus = null);

    private enum ExactFactIntent
    {
        None,
        Credits,
        LecturerName,
        AttendancePercentage
    }

    private sealed record CreditFact(double Value, string EvidenceText);

    private sealed record CourseCreditComparisonFact(
        string CourseCode,
        double? Credit,
        string? EvidenceText,
        DocumentChunk? Chunk);

    private sealed record AssessmentComponentFact(
        string Name,
        double WeightPercent,
        string EvidenceText);

    private sealed record ComparisonEvidenceSlot(
        string CourseCode,
        string Dimension,
        IReadOnlySet<string> Terms);

    private sealed record StudentPreferenceProfile(
        string LabelVi,
        string LabelEn,
        IReadOnlySet<string> Terms);

    private sealed record SubjectRouteResult(
        string? SelectedSubject,
        bool NeedsClarification,
        IReadOnlyList<string> CandidateSubjects);

    private sealed record ScoredChunk(
        DocumentChunk Chunk,
        double Score,
        int TextSharedTerms,
        int ContentSharedTerms,
        int SharedTerms,
        int MetadataSharedTerms,
        double VectorScore,
        double LexicalScore);

    private async Task<ChatAnswer> SaveAssistantAnswer(
        Guid sessionId,
        string answer,
        IReadOnlyList<SourceCitation> citations,
        CancellationToken cancellationToken,
        ChatSessionOwnerInfo? ownerInfo = null,
        string? resolvedSubject = null,
        bool needsClarification = false,
        IReadOnlyList<string>? subjectOptions = null,
        string answerSource = "Rag",
        bool hasDirectCitation = true,
        string? fallbackModel = null,
        string? answerStatus = null)
    {
        await _repository.AddMessageAsync(sessionId, KnowledgeModelMapper.ToData(new ChatMessage
        {
            Role = "assistant",
            Content = answer,
            Citations = citations.ToList()
        }), cancellationToken, KnowledgeModelMapper.ToData(ownerInfo));

        var session = KnowledgeModelMapper.ToModel(await _repository.GetOrCreateSessionAsync(sessionId, cancellationToken, KnowledgeModelMapper.ToData(ownerInfo)));
        return new ChatAnswer(
            answer,
            citations,
            session.Messages,
            resolvedSubject,
            needsClarification,
            subjectOptions ?? Array.Empty<string>(),
            answerSource,
            hasDirectCitation,
            fallbackModel,
            answerStatus ?? ChatGroundingPolicy.ResolveAnswerStatus(answerSource, needsClarification, citations.Count));
    }

    private async Task<QueryIntentDecision> ClassifyQuestionIntentAsync(
        string question,
        IReadOnlyList<ChatMessage> history,
        string language,
        CancellationToken cancellationToken)
    {
        var localDecision = ClassifyQuestionLocally(question);
        if (localDecision.Intent is ChatQueryIntent.Unsafe or ChatQueryIntent.SmallTalk or ChatQueryIntent.ExternalQuestion)
        {
            return localDecision;
        }

        QueryIntentDecision modelDecision;
        try
        {
            modelDecision = await _chatCompletionService.ClassifyQuestionAsync(question, history, language, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return localDecision;
        }

        if (modelDecision.Confidence < 0.7)
        {
            return localDecision;
        }

        if (HasDocumentQuestionSignal(question)
            && modelDecision.Intent is ChatQueryIntent.SmallTalk or ChatQueryIntent.ExternalQuestion)
        {
            return localDecision;
        }

        return modelDecision;
    }

    private static QueryIntentDecision ClassifyQuestionLocally(string question)
    {
        if (LooksLikePromptInjection(question))
        {
            return new QueryIntentDecision(ChatQueryIntent.Unsafe, 1, "prompt-injection-signal");
        }

        if (IsCasualChat(question) && !HasDocumentQuestionSignal(question))
        {
            return new QueryIntentDecision(ChatQueryIntent.SmallTalk, 0.95, "local-small-talk");
        }

        if (LooksLikeExternalQuestion(question) && !HasDocumentQuestionSignal(question))
        {
            return new QueryIntentDecision(ChatQueryIntent.ExternalQuestion, 0.95, "local-external-question");
        }

        return new QueryIntentDecision(ChatQueryIntent.DocumentQuestion, 0.6, "default-document-question");
    }

    private async Task<IReadOnlyList<string>> BuildRetrievalQueriesAsync(
        string question,
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        var queries = new List<string> { question.Trim() };
        var localStandaloneQuestion = BuildLocalStandaloneQuestion(question, history);
        AddDistinctQuery(queries, localStandaloneQuestion);

        foreach (var slotQuery in BuildComparisonSlotQueries(localStandaloneQuestion))
        {
            AddDistinctQuery(queries, slotQuery);
        }

        try
        {
            var rewrittenQueries = await _chatCompletionService.RewriteQueriesAsync(question, history, cancellationToken);
            foreach (var rewritten in rewrittenQueries)
            {
                AddDistinctQuery(queries, rewritten);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Retrieval can still run with the original question.
        }

        return queries.Count == 0 ? new[] { question.Trim() } : queries.Take(4).ToList();
    }

    private static string BuildLocalStandaloneQuestion(string question, IReadOnlyList<ChatMessage> history)
    {
        var normalized = NormalizeQuestion(question);
        var refersToPreviousCourses = normalized.Contains("hai mon nay", StringComparison.Ordinal)
                                      || normalized.Contains("2 mon nay", StringComparison.Ordinal)
                                      || normalized.Contains("cac mon nay", StringComparison.Ordinal)
                                      || normalized.Contains("mon kia", StringComparison.Ordinal)
                                      || normalized.Contains("these two courses", StringComparison.Ordinal)
                                      || normalized.Contains("those courses", StringComparison.Ordinal);
        if (!refersToPreviousCourses || ExtractCourseCodes(question).Count >= 2)
        {
            return question.Trim();
        }

        var historyCodes = history
            .TakeLast(8)
            .SelectMany(message => ExtractCourseCodes(message.Content))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .TakeLast(2)
            .ToList();
        return historyCodes.Count < 2
            ? question.Trim()
            : $"{question.Trim()} ({string.Join(" và ", historyCodes)})";
    }

    private static IReadOnlyList<string> BuildComparisonSlotQueries(string question)
    {
        var normalized = NormalizeQuestion(question);
        if (!IsMultiSubjectQuestion(normalized))
        {
            return Array.Empty<string>();
        }

        var courseCodes = ExtractCourseCodes(question).Take(3).ToList();
        if (courseCodes.Count < 2)
        {
            return Array.Empty<string>();
        }

        string dimension;
        if (IsDifficultyQuestion(normalized))
        {
            dimension = "khối lượng học điều kiện tiên quyết bài tập thực hành hình thức đánh giá";
        }
        else if (IsAssessmentQuestion(normalized))
        {
            dimension = "cơ cấu điểm thành phần đánh giá tỷ trọng bài tập thi cuối kỳ";
        }
        else
        {
            dimension = "mục tiêu nội dung kỹ năng đánh giá khối lượng học";
        }
        return courseCodes.Select(code => $"{code} {dimension}").ToList();
    }

    private static void AddDistinctQuery(List<string> queries, string? query)
    {
        var trimmed = query?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        if (!queries.Any(existing => existing.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            queries.Add(trimmed);
        }
    }

    private async Task<bool> IsAnswerGroundedAsync(
        string question,
        string answer,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlySet<string> questionTerms,
        string language,
        CancellationToken cancellationToken)
    {
        if (!ChatGroundingPolicy.AreClaimsSupportedByCitedSources(
                answer,
                chunks.Select(chunk => chunk.Text).ToList()))
        {
            return false;
        }

        var contextText = string.Join("\n\n", chunks.Select(chunk => chunk.Text));
        if (!IsAnswerGrounded(answer, contextText, questionTerms))
        {
            return false;
        }

        GroundingDecision? decision;
        try
        {
            decision = await _chatCompletionService.ValidateGroundingAsync(
                question,
                answer,
                chunks,
                language,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return true;
        }

        return decision is null || decision.IsGrounded;
    }

    private static bool LooksLikePromptInjection(string question)
    {
        var normalized = RemoveDiacritics(question).ToLowerInvariant();
        return PromptInjectionSignals.Any(signal => normalized.Contains(signal, StringComparison.Ordinal));
    }

    private static bool IsCasualChat(string question)
    {
        var normalized = RemoveDiacritics(question).ToLowerInvariant();
        var compact = Regex.Replace(normalized, @"[^\p{L}\p{N}\s]+", " ", RegexOptions.None, RegexTimeout).Trim();
        var compactTokens = compact.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var terms = ExtractTerms(question);

        return CasualChatSignals.Contains(compact)
               || (terms.Count <= 2 && CasualChatSignals.Any(signal => compactTokens.Contains(signal, StringComparer.Ordinal)));
    }

    private static bool LooksLikeExternalQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return ExternalQuestionSignals.Any(signal => normalized.Contains(signal, StringComparison.Ordinal));
    }

    private static bool HasDocumentQuestionSignal(string question)
    {
        var normalized = NormalizeQuestion(question);
        return ExtractCourseCodes(question).Count > 0
               || DocumentQuestionSignals.Any(signal => normalized.Contains(signal, StringComparison.Ordinal));
    }

    private static bool IsBotIdentityQuestion(string question)
    {
        var compact = NormalizeQuestion(question);
        return compact is "bot la ai" or "chatbot la ai" or "ban la ai" or "may la ai"
               || compact.Contains("bot cua ban la ai", StringComparison.Ordinal)
               || compact.Contains("chatbot nay la ai", StringComparison.Ordinal);
    }

    private static bool IsUserIdentityQuestion(string question)
    {
        var compact = NormalizeQuestion(question);
        return compact is "toi la ai" or "minh la ai" or "tao la ai" or "em la ai";
    }

    private static string NormalizeLanguage(string? language)
    {
        return language?.Equals("vi", StringComparison.OrdinalIgnoreCase) == true ? "vi" : "en";
    }

    private static string BuildOutOfScopeAnswer(string language)
    {
        return language == "vi"
            ? "Mình không đủ dữ liệu trong tài liệu để trả lời câu hỏi này."
            : "I do not have enough data in the documents to answer this question.";
    }

    private static string BuildExternalScopeAnswer(string language)
    {
        return language == "vi"
            ? "Mình chỉ hỗ trợ hỏi đáp dựa trên tài liệu đã index. Câu này nằm ngoài phạm vi tài liệu, nên mình không truy xuất nguồn để trả lời."
            : "I only answer from indexed documents. This question is outside the document scope, so I will not retrieve sources for it.";
    }

    private static string BuildBotIdentityAnswer(string language)
    {
        if (language == "en")
        {
            return "I am an AI assistant specialized in searching and explaining content from your learning document repository. Ask a question, and I will look through the documents, summarize the relevant parts clearly, and include sources when there is enough data.";
        }

        return "Mình là AI chuyên hỗ trợ tra cứu và giải thích nội dung trong kho tài liệu học tập. Nói đơn giản: bạn hỏi, mình tìm trong tài liệu, tóm gọn lại cho dễ hiểu, rồi kèm nguồn khi có dữ liệu.";
    }

    private static string BuildUserIdentityAnswer(string? userDisplayName, string language)
    {
        var name = userDisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = language == "vi" ? "bạn" : "you";
        }
        if (language == "en")
        {
            return $"You are {name}. In this app, you are the owner of this document workspace and the person I am helping.";
        }

        return $"Bạn là {name}. Trong ứng dụng này, bạn là chủ kho tài liệu và là người mình đang hỗ trợ.";
    }

    private static string BuildCasualChatAnswer(string question, string language)
    {
        var normalized = RemoveDiacritics(question).ToLowerInvariant();
        if (normalized.Contains("cam on", StringComparison.Ordinal)
            || normalized.Contains("thanks", StringComparison.Ordinal)
            || normalized.Contains("thank you", StringComparison.Ordinal))
        {
            if (language == "vi")
            {
                return "Không có gì. Cần tra phần nào trong tài liệu thì ném câu hỏi qua, mình xem tiếp.";
            }

            return "You're welcome. Send the next document question over and I will check it.";
        }

        if (normalized.Contains("tam biet", StringComparison.Ordinal)
            || normalized.Contains("bye", StringComparison.Ordinal))
        {
            if (language == "vi")
            {
                return "Tạm biệt nhé. Khi cần tra tài liệu thì quay lại, mình vẫn ở đây.";
            }

            return "Goodbye. Come back when you need to check the documents again.";
        }

        if (normalized.Contains("an com", StringComparison.Ordinal)
            || normalized.Contains("com chua", StringComparison.Ordinal))
        {
            if (language == "vi")
            {
                return "Mình không ăn cơm được, nhưng đang trực kho tài liệu đây. Bạn muốn mình xem môn hay phần nào?";
            }

            return "I do not eat, but I am on document duty. What course or section should I check?";
        }

        if (normalized.Contains("khoe khong", StringComparison.Ordinal)
            || normalized.Contains("on khong", StringComparison.Ordinal))
        {
            if (language == "vi")
            {
                return "Mình ổn, đang sẵn sàng tra tài liệu. Bạn đang cần xem câu nào?";
            }

            return "I am good and ready to search the documents. What do you want to check?";
        }

        if (language == "vi")
        {
            return "Chào bạn, mình đây. Hỏi thẳng môn, CLO, đánh giá hoặc phần nào trong tài liệu, mình tra cho.";
        }

        return "Hi, I am here. Ask about a course, CLO, assessment, or document section and I will look it up.";
    }

    private static string NormalizeQuestion(string question)
    {
        var normalized = RemoveDiacritics(question).ToLowerInvariant();
        return Regex.Replace(normalized, @"[^\p{L}\p{N}\s]+", " ", RegexOptions.None, RegexTimeout).Trim();
    }

    private static bool IsInsufficientDataAnswer(string answer)
    {
        var normalized = RemoveDiacritics(answer).ToLowerInvariant();
        return normalized.Contains("khong du du lieu", StringComparison.Ordinal)
               || normalized.Contains("khong tim thay thong tin", StringComparison.Ordinal)
               || normalized.Contains("khong co trong tai lieu", StringComparison.Ordinal)
               || normalized.Contains("not enough data", StringComparison.Ordinal)
               || normalized.Contains("insufficient data", StringComparison.Ordinal)
               || normalized.Contains("do not have enough data", StringComparison.Ordinal);
    }

    private static bool IsAnswerGrounded(
        string answer,
        string contextText,
        IReadOnlySet<string> questionTerms)
    {
        if (string.IsNullOrWhiteSpace(answer) || string.IsNullOrWhiteSpace(contextText))
        {
            return false;
        }

        var answerWithoutSourceMarkers = ChatGroundingPolicy.RemoveSourceMarkers(answer);
        var answerFacts = ExtractGroundingFacts(answerWithoutSourceMarkers);
        if (answerFacts.Count > 0)
        {
            var contextFacts = ExtractGroundingFacts(contextText);
            if (answerFacts.Any(fact => !contextFacts.Contains(fact)))
            {
                return false;
            }
        }

        var answerTerms = ExtractTerms(answerWithoutSourceMarkers);
        answerTerms.ExceptWith(questionTerms);
        answerTerms.RemoveWhere(term => AnswerScaffoldTerms.Contains(term));
        if (answerTerms.Count == 0)
        {
            return true;
        }

        var contextTerms = ExtractTerms(contextText);
        if (contextTerms.Count == 0)
        {
            return false;
        }

        var groundedTerms = answerTerms.Count(answerTerm => contextTerms.Any(contextTerm => TermsMatch(answerTerm, contextTerm)));
        var groundingRatio = groundedTerms / (double)answerTerms.Count;
        var requiredRatio = answerTerms.Count <= 4 ? 0.5 : MinimumAnswerGroundingRatio;
        return groundingRatio >= requiredRatio;
    }

    private static HashSet<string> ExtractGroundingFacts(string text)
    {
        return Regex.Matches(NormalizeQuestion(text), @"\b(?:[a-z]{2,}\d{2,}|\d+(?:[.,]\d+)?%?)\b", RegexOptions.None, RegexTimeout)
            .Select(match => match.Value.Replace(',', '.'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildGroundedAnswer(
        IReadOnlySet<string> queryTerms,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        IReadOnlySet<string>? requiredCourseCodes = null)
    {
        var minimumSharedTerms = queryTerms.Count >= 4 ? 2 : 1;
        var rankedSentences = chunks
            .SelectMany((chunk, sourceIndex) => SplitSentences(chunk.Text).Select(sentence => new
            {
                Text = sentence,
                SourceNumber = sourceIndex + 1,
                chunk.Subject,
                SharedTerms = CountSharedTerms(queryTerms, sentence)
            }))
            .Where(item => item.Text.Length is > 8 and <= 500
                           && item.SharedTerms >= minimumSharedTerms
                           && !IsLowValueFallbackSentence(item.Text))
            .OrderByDescending(item => item.SharedTerms)
            .GroupBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var selectedSentences = new List<(string Text, int SourceNumber)>();
        var selectedSentenceTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var courseCodes = requiredCourseCodes is { Count: > 0 }
            ? requiredCourseCodes
            : ExtractCourseCodes(string.Join(" ", queryTerms));
        foreach (var courseCode in courseCodes)
        {
            var sentence = rankedSentences.FirstOrDefault(item => SubjectMatches(item.Subject, courseCode));
            if (sentence is null || !selectedSentenceTexts.Add(sentence.Text))
            {
                continue;
            }

            selectedSentences.Add((sentence.Text, sentence.SourceNumber));
        }

        foreach (var sentence in rankedSentences)
        {
            if (selectedSentences.Count == 3)
            {
                break;
            }

            if (selectedSentenceTexts.Add(sentence.Text))
            {
                selectedSentences.Add((sentence.Text, sentence.SourceNumber));
            }
        }

        if (selectedSentences.Count == 0)
        {
            return BuildOutOfScopeAnswer(language);
        }

        if (language == "vi")
        {
            return "Tóm tắt từ tài liệu:\n\n" +
                   string.Join("\n", selectedSentences.Select(item => $"- {AttachSourceMarker(item.Text, item.SourceNumber)}"));
        }

        return "Summary from the documents:\n\n" +
               string.Join("\n", selectedSentences.Select(item => $"- {AttachSourceMarker(item.Text, item.SourceNumber)}"));
    }

    private static string AttachSourceMarker(string text, int sourceNumber)
    {
        var trimmed = text.Trim();
        if (trimmed.Length > 0 && trimmed[^1] is '.' or '!' or '?')
        {
            return $"{trimmed[..^1].TrimEnd()} [{sourceNumber}]{trimmed[^1]}";
        }

        return $"{trimmed} [{sourceNumber}]";
    }

    private static bool IsLowValueFallbackSentence(string sentence)
    {
        var normalized = NormalizeQuestion(sentence);
        return normalized.StartsWith("nguon ", StringComparison.Ordinal)
               || normalized.StartsWith("source ", StringComparison.Ordinal)
               || normalized.Contains("http ", StringComparison.Ordinal)
               || normalized.Contains("https ", StringComparison.Ordinal)
               || normalized.Contains("url ", StringComparison.Ordinal);
    }

    private static string ResolveSubject(IReadOnlyList<DocumentChunk> chunks)
    {
        var subject = chunks
            .Select(chunk => chunk.Subject)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(subject))
        {
            return "môn học";
        }

        return subject;
    }

    private static IEnumerable<string> SplitSentences(string text)
    {
        var separators = new[] { ". ", "! ", "? ", "\n" };
        return text
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(sentence => sentence.Trim().Trim('-', '*'))
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence));
    }

    private static int CountSharedTerms(IReadOnlySet<string> queryTerms, string chunkText)
    {
        if (queryTerms.Count == 0)
        {
            return 0;
        }

        var chunkTerms = ExtractTerms(chunkText);
        return queryTerms.Count(queryTerm => chunkTerms.Any(chunkTerm => TermsMatch(queryTerm, chunkTerm)));
    }

    private static bool HasRetrievalEvidence(
        ScoredChunk candidate,
        bool needsContentEvidence,
        int minimumSharedTerms)
    {
        if (candidate.Score < MinimumScore)
        {
            return false;
        }

        var hasTextEvidence = candidate.TextSharedTerms >= minimumSharedTerms
                              || candidate.ContentSharedTerms > 0
                              || candidate.VectorScore >= 0.72;
        if (!hasTextEvidence)
        {
            return false;
        }

        return !needsContentEvidence
               || candidate.ContentSharedTerms > 0
               || candidate.TextSharedTerms >= minimumSharedTerms + 1
               || (candidate.MetadataSharedTerms > 0 && candidate.VectorScore >= 0.62);
    }

    private static double CalculateRetrievalScore(double vectorScore, double lexicalScore, bool courseCodeMatched)
    {
        var normalizedVector = Math.Clamp(vectorScore, 0, 1);
        return Clamp01((normalizedVector * 0.42) + (lexicalScore * 0.50) + (courseCodeMatched ? 0.08 : 0));
    }

    private static double CalculateLexicalScore(
        int textSharedTerms,
        int metadataSharedTerms,
        int contentSharedTerms,
        int queryTermCount,
        int contentTermCount)
    {
        if (queryTermCount == 0)
        {
            return 0;
        }

        var textCoverage = textSharedTerms / (double)queryTermCount;
        var contentCoverage = contentTermCount == 0
            ? textCoverage
            : contentSharedTerms / (double)contentTermCount;
        var metadataCoverage = Math.Min(1, metadataSharedTerms / (double)Math.Min(queryTermCount, 4));

        return Clamp01((contentCoverage * 0.50) + (textCoverage * 0.28) + (metadataCoverage * 0.22));
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0, 1);
    }

    private static bool TermsMatch(string queryTerm, string sourceTerm)
    {
        if (queryTerm.Equals(sourceTerm, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (queryTerm.Length < 3 || sourceTerm.Length < 3)
        {
            return false;
        }

        if (sourceTerm.StartsWith(queryTerm, StringComparison.OrdinalIgnoreCase)
            || queryTerm.StartsWith(sourceTerm, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return LooksLikeTypo(queryTerm, sourceTerm);
    }

    private static bool LooksLikeTypo(string queryTerm, string sourceTerm)
    {
        if (queryTerm.Length < 4 || sourceTerm.Length < 4)
        {
            return false;
        }

        var lengthGap = Math.Abs(queryTerm.Length - sourceTerm.Length);
        if (lengthGap > 2)
        {
            return false;
        }

        var maxDistance = Math.Min(queryTerm.Length, sourceTerm.Length) <= 5 ? 1 : 2;
        return LevenshteinDistanceAtMost(queryTerm, sourceTerm, maxDistance);
    }

    private static bool LevenshteinDistanceAtMost(string left, string right, int maxDistance)
    {
        if (Math.Abs(left.Length - right.Length) > maxDistance)
        {
            return false;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var index = 0; index <= right.Length; index++)
        {
            previous[index] = index;
        }

        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;
            var rowMinimum = current[0];

            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                var cost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    previous[rightIndex - 1] + cost);
                rowMinimum = Math.Min(rowMinimum, current[rightIndex]);
            }

            if (rowMinimum > maxDistance)
            {
                return false;
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length] <= maxDistance;
    }

    private static string BuildChunkMetadataText(DocumentChunk chunk)
    {
        return $"{chunk.FileName} {chunk.Subject} {chunk.Chapter} {chunk.SectionTitle}";
    }

    private static HashSet<string> ExtractTerms(string text)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = RemoveDiacritics(text).ToLowerInvariant();
        foreach (Match match in TokenRegex.Matches(normalized))
        {
            var token = match.Value.Trim();
            if (token.Length >= 2 && !StopWords.Contains(token))
            {
                terms.Add(token);
            }
        }

        return terms;
    }

    private static HashSet<string> RemoveCourseScopeTerms(IReadOnlySet<string> terms)
    {
        var scopedTerms = new HashSet<string>(terms, StringComparer.OrdinalIgnoreCase);
        scopedTerms.RemoveWhere(term =>
        {
            var normalized = Regex.Replace(term ?? string.Empty, @"[^a-z0-9]", string.Empty, RegexOptions.IgnoreCase, RegexTimeout);
            return Regex.IsMatch(normalized, @"^[a-z]{2,}\d{2,}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout)
                   || normalized.Equals("syllabus", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("subject", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("course", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("mon", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("hoc", StringComparison.OrdinalIgnoreCase);
        });
        return scopedTerms;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character == '\u0111' || character == '\u0110' ? 'd' : character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string CreateExcerpt(string text)
    {
        const int maxLength = 320;
        var compact = string.Join(" ", text.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= maxLength ? compact : $"{compact[..maxLength]}...";
    }
}

