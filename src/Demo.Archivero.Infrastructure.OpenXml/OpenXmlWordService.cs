using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Demo.Archivero.Application.Interfaces.Infrastructure;

namespace Demo.Archivero.Infrastructure.OpenXml;

public class OpenXmlWordService : IOpenXmlWordService
{
    public MemoryStream CreateDocument(string title,string content)
    {
        var stream = new MemoryStream();

        // Key detail: WordprocessingDocument writes its final package data on Dispose(), so you must close/dispose it before reading from the stream — and then reset Position = 0.
        using (var wordDocument = WordprocessingDocument.Create(
            stream, WordprocessingDocumentType.Document, autoSave: true))
        {
            MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            // ---- Title paragraph: centered, 36pt, blue ----
            Paragraph titlePara = body.AppendChild(new Paragraph());
            ParagraphProperties titleParaProps = titlePara.AppendChild(new ParagraphProperties());
            titleParaProps.Justification = new Justification { Val = JustificationValues.Center };

            Run titleRun = titlePara.AppendChild(new Run());
            RunProperties titleRunProps = titleRun.AppendChild(new RunProperties());
            titleRunProps.FontSize = new FontSize { Val = "72" };      // 36pt -> half-points = 72
            titleRunProps.Color = new Color { Val = "0000FF" };        // blue
            titleRun.AppendChild(new Text(title));

            // ---- 2 empty lines as spacing ----
            body.AppendChild(new Paragraph());
            body.AppendChild(new Paragraph());

            // ---- Content paragraph: left-aligned, 16pt, black ----
            Paragraph contentPara = body.AppendChild(new Paragraph());
            ParagraphProperties contentParaProps = contentPara.AppendChild(new ParagraphProperties());
            contentParaProps.Justification = new Justification { Val = JustificationValues.Left };

            Run contentRun = contentPara.AppendChild(new Run());
            RunProperties contentRunProps = contentRun.AppendChild(new RunProperties());
            contentRunProps.FontSize = new FontSize { Val = "32" };    // 16pt -> half-points = 32
            contentRunProps.Color = new Color { Val = "000000" };      // black
            contentRun.AppendChild(new Text(content));

            mainPart.Document.Save();
        } // Disposing here finalizes the OPC package into the stream

        stream.Position = 0; // Reset for reading
        return stream;
    }


}