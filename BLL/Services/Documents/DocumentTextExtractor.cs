using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using UglyToad.PdfPig;
using DrawingText = DocumentFormat.OpenXml.Drawing.Text;
using WordText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace PRN222_FINAL.BLL;

public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    public const string SupportedFormatsLabel = "PDF, DOCX, PPTX, XLSX, TXT, MD, CSV";

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".pptx", ".xlsx", ".txt", ".md", ".csv"
    };

    public static bool IsSupportedFileName(string? fileName) =>
        SupportedExtensions.Contains(Path.GetExtension(fileName ?? string.Empty));

    public async Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;
        if (!SupportedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Only {SupportedFormatsLabel} files are supported.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return extension switch
        {
            ".txt" or ".md" or ".csv" => await ExtractPlainTextAsync(stream, cancellationToken),
            ".pdf" => ExtractPdf(stream, cancellationToken),
            ".docx" => ExtractWord(stream, cancellationToken),
            ".pptx" => ExtractPowerPoint(stream, cancellationToken),
            ".xlsx" => ExtractSpreadsheet(stream, cancellationToken),
            _ => throw new InvalidOperationException($"Only {SupportedFormatsLabel} files are supported.")
        };
    }

    private static async Task<string> ExtractPlainTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (text.IndexOf('\0') >= 0)
        {
            throw new InvalidOperationException("The selected text file contains binary data and cannot be indexed.");
        }

        return text;
    }

    private static string ExtractPdf(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var document = PdfDocument.Open(stream);
            var pages = new List<string>(document.NumberOfPages);
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(page.Text))
                {
                    pages.Add(page.Text);
                }
            }

            return string.Join("\n\n", pages);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("The PDF is invalid, encrypted, or contains no extractable text.", ex);
        }
    }

    private static string ExtractWord(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var document = WordprocessingDocument.Open(stream, false);
            var body = document.MainDocumentPart?.Document.Body;
            if (body is null)
            {
                return string.Empty;
            }

            var paragraphs = new List<string>();
            foreach (var paragraph in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = string.Concat(paragraph.Descendants<WordText>().Select(item => item.Text)).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    paragraphs.Add(text);
                }
            }

            return string.Join('\n', paragraphs);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("The DOCX file is invalid or cannot be read.", ex);
        }
    }

    private static string ExtractPowerPoint(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var document = PresentationDocument.Open(stream, false);
            var presentationPart = document.PresentationPart;
            var slideIds = presentationPart?.Presentation.SlideIdList?.ChildElements
                .OfType<DocumentFormat.OpenXml.Presentation.SlideId>()
                .ToList() ?? [];
            var slides = new List<string>(slideIds.Count);
            foreach (var slideId in slideIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (slideId.RelationshipId?.Value is not { Length: > 0 } relationshipId
                    || presentationPart?.GetPartById(relationshipId) is not SlidePart slidePart)
                {
                    continue;
                }

                var text = string.Join(" ", slidePart.Slide.Descendants<DrawingText>()
                    .Select(item => item.Text?.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item)));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    slides.Add(text);
                }
            }

            return string.Join("\n\n", slides);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("The PPTX file is invalid or cannot be read.", ex);
        }
    }

    private static string ExtractSpreadsheet(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var document = SpreadsheetDocument.Open(stream, false);
            var workbookPart = document.WorkbookPart;
            var sharedStrings = workbookPart?.SharedStringTablePart?.SharedStringTable?
                .Elements<SharedStringItem>()
                .Select(item => item.InnerText)
                .ToList() ?? [];
            var rows = new List<string>();

            foreach (var sheet in workbookPart?.Workbook.Sheets?.Elements<Sheet>() ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (sheet.Id?.Value is not { Length: > 0 } relationshipId
                    || workbookPart?.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(sheet.Name?.Value))
                {
                    rows.Add($"Sheet: {sheet.Name.Value}");
                }

                foreach (var row in worksheetPart.Worksheet.Descendants<Row>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var values = row.Elements<Cell>()
                        .Select(cell => GetCellText(cell, sharedStrings))
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToList();
                    if (values.Count > 0)
                    {
                        rows.Add(string.Join(" | ", values));
                    }
                }
            }

            return string.Join('\n', rows);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("The XLSX file is invalid or cannot be read.", ex);
        }
    }

    private static string GetCellText(Cell cell, IReadOnlyList<string> sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText?.Trim() ?? string.Empty;
        }

        var value = cell.CellValue?.Text?.Trim() ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString
            && int.TryParse(value, out var sharedStringIndex)
            && sharedStringIndex >= 0
            && sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex].Trim();
        }

        return value;
    }
}
