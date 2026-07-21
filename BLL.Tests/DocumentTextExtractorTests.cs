using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PRN222_FINAL.BLL;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace PRN222_FINAL.BLL.Tests;

public sealed class DocumentTextExtractorTests
{
    private readonly DocumentTextExtractor _extractor = new();

    [Fact]
    public async Task ExtractAsync_ReadsUtf8PlainText()
    {
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Course outline and assessment."));

        var result = await _extractor.ExtractAsync(stream, "outline.txt");

        Assert.Equal("Course outline and assessment.", result);
    }

    [Fact]
    public async Task ExtractAsync_ReadsParagraphsFromDocx()
    {
        await using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(
                new Body(
                    new Paragraph(new Run(new Text("PRN222 uses secure cookie authentication."))),
                    new Paragraph(new Run(new Text("The final assessment is documented.")))));
            mainPart.Document.Save();
        }
        stream.Position = 0;

        var result = await _extractor.ExtractAsync(stream, "syllabus.docx");

        Assert.Contains("PRN222 uses secure cookie authentication.", result, StringComparison.Ordinal);
        Assert.Contains("The final assessment is documented.", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractAsync_ReadsTextFromPdf()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        page.AddText("PRN222 syllabus PDF content.", 12, new PdfPoint(50, 780), font);
        await using var stream = new MemoryStream(builder.Build());

        var result = await _extractor.ExtractAsync(stream, "syllabus.pdf");

        Assert.Contains("PRN222 syllabus PDF content.", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractAsync_ReadsTextFromPptx()
    {
        await using var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation, true))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation();
            var slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.Slide = new P.Slide(
                new P.CommonSlideData(
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1, Name = string.Empty },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(new A.TransformGroup()),
                        new P.Shape(
                            new P.NonVisualShapeProperties(
                                new P.NonVisualDrawingProperties { Id = 2, Name = "Content" },
                                new P.NonVisualShapeDrawingProperties(),
                                new P.ApplicationNonVisualDrawingProperties()),
                            new P.ShapeProperties(),
                            new P.TextBody(
                                new A.BodyProperties(),
                                new A.ListStyle(),
                                new A.Paragraph(new A.Run(new A.Text("PPTX lecture assessment content."))))))));
            slidePart.Slide.Save();
            var slideIdList = presentationPart.Presentation.AppendChild(new P.SlideIdList());
            slideIdList.Append(new P.SlideId
            {
                Id = 256,
                RelationshipId = presentationPart.GetIdOfPart(slidePart)
            });
            presentationPart.Presentation.Save();
        }
        stream.Position = 0;

        var result = await _extractor.ExtractAsync(stream, "lecture.pptx");

        Assert.Contains("PPTX lecture assessment content.", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractAsync_ReadsCellsFromXlsx()
    {
        await using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new S.Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new S.Worksheet(
                new S.SheetData(
                    new S.Row(
                        new S.Cell { DataType = S.CellValues.String, CellValue = new S.CellValue("Assessment") },
                        new S.Cell { DataType = S.CellValues.String, CellValue = new S.CellValue("Final exam 60%") })));
            worksheetPart.Worksheet.Save();
            var sheets = workbookPart.Workbook.AppendChild(new S.Sheets());
            sheets.Append(new S.Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Syllabus"
            });
            workbookPart.Workbook.Save();
        }
        stream.Position = 0;

        var result = await _extractor.ExtractAsync(stream, "assessment.xlsx");

        Assert.Contains("Sheet: Syllabus", result, StringComparison.Ordinal);
        Assert.Contains("Assessment | Final exam 60%", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("lecture.pdf")]
    [InlineData("syllabus.docx")]
    [InlineData("slides.pptx")]
    [InlineData("grades.xlsx")]
    [InlineData("notes.txt")]
    [InlineData("readme.md")]
    [InlineData("schedule.csv")]
    public void IsSupportedFileName_AcceptsConfiguredDocumentFormats(string fileName)
    {
        Assert.True(DocumentTextExtractor.IsSupportedFileName(fileName));
    }

    [Fact]
    public async Task ExtractAsync_RejectsUnsupportedBinaryFormats()
    {
        await using var stream = new MemoryStream([0x01, 0x02, 0x03]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _extractor.ExtractAsync(stream, "archive.exe"));

        Assert.Contains(DocumentTextExtractor.SupportedFormatsLabel, exception.Message, StringComparison.Ordinal);
    }
}
