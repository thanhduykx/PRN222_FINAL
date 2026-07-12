using PRN222_FINAL.BLL;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class ChatGroundingPolicyTests
{
    [Theory]
    [InlineData("Nội dung được hỗ trợ [1].", 1, true)]
    [InlineData("Hai nguồn hỗ trợ [1] [2].", 2, true)]
    [InlineData("Không có nguồn.", 2, false)]
    [InlineData("Nguồn không tồn tại [3].", 2, false)]
    [InlineData("Nguồn số 0 [0].", 2, false)]
    [InlineData("Nguồn hợp lệ [1] nhưng kèm nguồn sai [9].", 2, false)]
    [InlineData("Ý thứ nhất có nguồn [1]. Ý thứ hai thiếu nguồn.", 2, false)]
    [InlineData("Tóm tắt:\n- Ý thứ nhất [1]\n- Ý thứ hai [2]", 2, true)]
    public void HasValidSourceMarkers_EnforcesAvailableSourceRange(
        string answer,
        int sourceCount,
        bool expected)
    {
        Assert.Equal(expected, ChatGroundingPolicy.HasValidSourceMarkers(answer, sourceCount));
    }

    [Fact]
    public void RemoveSourceMarkers_PreservesFactualNumbers()
    {
        var result = ChatGroundingPolicy.RemoveSourceMarkers("Môn học có 3 tín chỉ [1] và 45 giờ [2].");

        Assert.Equal("Môn học có 3 tín chỉ   và 45 giờ  .", result);
    }

    [Theory]
    [InlineData("Rag", false, 2, ChatGroundingPolicy.GroundedAnswerStatus)]
    [InlineData("Rag", false, 0, ChatGroundingPolicy.InsufficientEvidenceStatus)]
    [InlineData("OutOfScope", false, 0, ChatGroundingPolicy.InsufficientEvidenceStatus)]
    [InlineData("SmallTalk", false, 0, ChatGroundingPolicy.SmallTalkStatus)]
    [InlineData("Rag", true, 0, ChatGroundingPolicy.ClarificationRequiredStatus)]
    public void ResolveAnswerStatus_SeparatesProductOutcomes(
        string answerSource,
        bool needsClarification,
        int citationCount,
        string expected)
    {
        Assert.Equal(
            expected,
            ChatGroundingPolicy.ResolveAnswerStatus(answerSource, needsClarification, citationCount));
    }
}
