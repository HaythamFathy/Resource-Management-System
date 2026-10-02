using System.IO.Compression;
using System.Text;

namespace Resource_Management_System.Services
{
    /// <summary>
    /// Generates a minimal, valid .xlsx (OOXML) file with a single right-to-left
    /// sheet, without requiring any third-party Excel library. Headers are
    /// rendered bold; numeric values are written as real numbers so Excel can
    /// sum/format them, everything else is written as inline text.
    /// </summary>
    public static class SimpleXlsxWriter
    {
        public static byte[] Create(string sheetName, string[] headers, IEnumerable<object?[]> rows)
        {
            using var mem = new MemoryStream();
            using (var archive = new ZipArchive(mem, ZipArchiveMode.Create, true))
            {
                WriteEntry(archive, "[Content_Types].xml", ContentTypesXml());
                WriteEntry(archive, "_rels/.rels", RootRelsXml());
                WriteEntry(archive, "xl/workbook.xml", WorkbookXml(SanitizeSheetName(sheetName)));
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml());
                WriteEntry(archive, "xl/styles.xml", StylesXml());
                WriteEntry(archive, "xl/worksheets/sheet1.xml", SheetXml(headers, rows));
            }
            return mem.ToArray();
        }

        private static void WriteEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        private static string SanitizeSheetName(string name)
        {
            var invalid = new[] { '\\', '/', '*', '?', ':', '[', ']' };
            foreach (var c in invalid) name = name.Replace(c, ' ');
            return name.Length > 31 ? name.Substring(0, 31) : name;
        }

        private static string Esc(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private static string ColumnLetter(int index) // 0-based
        {
            var dividend = index + 1;
            var name = string.Empty;
            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                name = Convert.ToChar('A' + modulo) + name;
                dividend = (dividend - modulo - 1) / 26;
            }
            return name;
        }

        private static string ContentTypesXml() => """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
</Types>
""";

        private static string RootRelsXml() => """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>
""";

        private static string WorkbookXml(string sheetName) => $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="{Esc(sheetName)}" sheetId="1" r:id="rId1"/>
  </sheets>
</workbook>
""";

        private static string WorkbookRelsXml() => """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>
""";

        private static string StylesXml() => """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="2">
    <font><sz val="11"/><name val="Calibri"/></font>
    <font><b/><sz val="11"/><name val="Calibri"/></font>
  </fonts>
  <fills count="2">
    <fill><patternFill patternType="none"/></fill>
    <fill><patternFill patternType="gray125"/></fill>
  </fills>
  <borders count="1">
    <border><left/><right/><top/><bottom/><diagonal/></border>
  </borders>
  <cellStyleXfs count="1">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
  </cellStyleXfs>
  <cellXfs count="2">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
    <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>
  </cellXfs>
</styleSheet>
""";

        private static string SheetXml(string[] headers, IEnumerable<object?[]> rows)
        {
            var sb = new StringBuilder();
            sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""").Append('\n');
            sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""").Append('\n');
            sb.Append("""<sheetViews><sheetView rightToLeft="1" workbookViewId="0"/></sheetViews>""").Append('\n');
            sb.Append("<sheetData>");

            int rowIndex = 1;

            // Header row (bold, style index 1)
            sb.Append($"<row r=\"{rowIndex}\">");
            for (int c = 0; c < headers.Length; c++)
            {
                var cellRef = $"{ColumnLetter(c)}{rowIndex}";
                sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\" s=\"1\"><is><t xml:space=\"preserve\">{Esc(headers[c])}</t></is></c>");
            }
            sb.Append("</row>");
            rowIndex++;

            // Data rows
            foreach (var row in rows)
            {
                sb.Append($"<row r=\"{rowIndex}\">");
                for (int c = 0; c < row.Length; c++)
                {
                    var cellRef = $"{ColumnLetter(c)}{rowIndex}";
                    var value = row[c];

                    if (value is int || value is long || value is short)
                    {
                        sb.Append($"<c r=\"{cellRef}\"><v>{value}</v></c>");
                    }
                    else if (value is double || value is float || value is decimal)
                    {
                        sb.Append($"<c r=\"{cellRef}\"><v>{Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)}</v></c>");
                    }
                    else
                    {
                        var text = value?.ToString() ?? string.Empty;
                        sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(text)}</t></is></c>");
                    }
                }
                sb.Append("</row>");
                rowIndex++;
            }

            sb.Append("</sheetData>");
            sb.Append("</worksheet>");
            return sb.ToString();
        }
    }
}
