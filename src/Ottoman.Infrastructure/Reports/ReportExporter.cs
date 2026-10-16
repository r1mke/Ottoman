using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Ottoman.Application.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ottoman.Infrastructure.Reports;

/// <summary>Turns a <see cref="TableDto"/> into a CSV, Excel or PDF file. One implementation per format, one input shape.</summary>
public class ReportExporter : IReportExporter
{
    static ReportExporter()
    {
        // QuestPDF is free for organisations under the revenue limit of its community licence.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ExportedFile Export(TableDto table, ExportFormat format) => format switch
    {
        ExportFormat.Csv => new ExportedFile(Csv(table), "text/csv; charset=utf-8", FileName(table, "csv")),
        ExportFormat.Xlsx => new ExportedFile(Xlsx(table), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName(table, "xlsx")),
        ExportFormat.Pdf => new ExportedFile(Pdf(table), "application/pdf", FileName(table, "pdf")),
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    // ---------------------------------------------------------------- CSV

    /// <summary>
    /// Semicolon-separated with a BOM, which is what Excel with Bosnian regional settings opens correctly.
    /// Text that starts with = + - or @ would be run as a formula by a spreadsheet, so it is defused.
    /// </summary>
    public static byte[] Csv(TableDto table)
    {
        var sb = new StringBuilder();
        sb.AppendJoin(';', table.Columns.Select(c => Escape(c.Title))).Append("\r\n");
        foreach (var row in table.Rows) sb.AppendJoin(';', row.Select((v, i) => Escape(Cell(v, table.Columns[i])))).Append("\r\n");
        if (table.Totals is not null)
            sb.AppendJoin(';', table.Totals.Select((v, i) => Escape(Cell(v, table.Columns[i])))).Append("\r\n");
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var safe = text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + text : text;
        return safe.Contains(';') || safe.Contains('"') || safe.Contains('\n') || safe.Contains('\r')
            ? "\"" + safe.Replace("\"", "\"\"") + "\""
            : safe;
    }

    /// <summary>Cell text. Money and percentages use two decimals; numbers in a "Money" column are never negative-prefixed text.</summary>
    public static string Cell(object? value, TableColumn column) => value switch
    {
        null => "",
        decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
        double d => d.ToString("0.##", CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };

    // ---------------------------------------------------------------- XLSX

    private static byte[] Xlsx(TableDto table)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SheetName(table.Title));

        for (var c = 0; c < table.Columns.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = table.Columns[c].Title;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#18181B");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var r = 2;
        foreach (var row in table.Rows) WriteRow(ws, r++, row, table.Columns, bold: false);
        if (table.Totals is not null) WriteRow(ws, r, table.Totals, table.Columns, bold: true);

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteRow(IXLWorksheet ws, int rowIndex, IReadOnlyList<object?> row, IReadOnlyList<TableColumn> columns, bool bold)
    {
        for (var c = 0; c < columns.Count; c++)
        {
            var cell = ws.Cell(rowIndex, c + 1);
            switch (row[c])
            {
                case decimal d:
                    cell.Value = d;
                    cell.Style.NumberFormat.Format = "#,##0.00";
                    break;
                case double d:
                    cell.Value = d;
                    break;
                case int i:
                    cell.Value = i;
                    break;
                case null:
                    break;
                default:
                    // Written as text: a value like "=1+1" stays a string and is never evaluated.
                    cell.SetValue(Convert.ToString(row[c], CultureInfo.InvariantCulture) ?? "");
                    break;
            }
            if (bold) cell.Style.Font.Bold = true;
        }
    }

    private static string SheetName(string title)
    {
        var clean = new string(title.Where(ch => ch is not ('[' or ']' or ':' or '*' or '?' or '/' or '\\')).ToArray());
        return clean.Length > 31 ? clean[..31] : clean.Length == 0 ? "Izvještaj" : clean;
    }

    // ---------------------------------------------------------------- PDF

    private static byte[] Pdf(TableDto table)
    {
        var landscape = table.Columns.Count > 5;
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(t => t.FontSize(9));

            page.Header().Column(col =>
            {
                col.Item().Text("OTTOMAN").Bold().FontSize(11).LetterSpacing(0.2f);
                col.Item().PaddingTop(2).Text(table.Title).Bold().FontSize(15);
                col.Item().PaddingBottom(8).Text($"Izrađeno {DateTime.UtcNow:dd.MM.yyyy. HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Darken1);
            });

            page.Content().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    foreach (var c in table.Columns)
                    {
                        if (c.Type is ColumnType.Money or ColumnType.Number or ColumnType.Percent) cd.RelativeColumn(1);
                        else cd.RelativeColumn(2);
                    }
                });

                t.Header(h =>
                {
                    foreach (var c in table.Columns)
                        h.Cell().Background(Colors.Grey.Darken4).Padding(5)
                            .Text(c.Title).Bold().FontColor(Colors.White);
                });

                var shade = false;
                foreach (var row in table.Rows)
                {
                    shade = !shade;
                    for (var c = 0; c < table.Columns.Count; c++)
                        Body(t, row[c], table.Columns[c], shade, bold: false);
                }
                if (table.Totals is not null)
                    for (var c = 0; c < table.Columns.Count; c++)
                        Body(t, table.Totals[c], table.Columns[c], shaded: false, bold: true);
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.Span("Stranica ").FontSize(8);
                x.CurrentPageNumber().FontSize(8);
                x.Span(" / ").FontSize(8);
                x.TotalPages().FontSize(8);
            });
        })).GeneratePdf();
    }

    private static void Body(TableDescriptor t, object? value, TableColumn column, bool shaded, bool bold)
    {
        var cell = t.Cell().Background(shaded ? Colors.Grey.Lighten4 : Colors.White)
            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);
        var aligned = column.Type is ColumnType.Money or ColumnType.Number or ColumnType.Percent ? cell.AlignRight() : cell;
        var text = aligned.Text(Cell(value, column));
        if (bold) text.Bold();
    }

    // ---------------------------------------------------------------- names

    private static string FileName(TableDto table, string extension)
    {
        var sb = new StringBuilder();
        foreach (var ch in Ottoman.Domain.Recommendations.TextNormalizer.StripDiacritics(table.Title).ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '-');
        var slug = string.Join('-', sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        return $"{(slug.Length == 0 ? "izvjestaj" : slug)}.{extension}";
    }
}
