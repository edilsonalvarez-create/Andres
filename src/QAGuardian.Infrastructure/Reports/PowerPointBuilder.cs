using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace QAGuardian.Infrastructure.Reports;

/// <summary>Sección de una presentación: un slide con encabezado y líneas de contenido.</summary>
public record PptxSection(string Heading, IReadOnlyList<string> Lines);

/// <summary>
/// Genera presentaciones .pptx (OpenXML puro, sin plantillas externas):
/// slide de título + un slide por sección. Formato 16:9.
/// </summary>
public static class PowerPointBuilder
{
    private const long SlideWidth = 12192000;   // EMU, 16:9
    private const long SlideHeight = 6858000;

    public static byte[] Build(string title, string subtitle, IReadOnlyList<PptxSection> sections)
    {
        using var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation();

            var masterPart = presentationPart.AddNewPart<SlideMasterPart>("rIdMaster");
            masterPart.SlideMaster = BuildMaster();
            var layoutPart = masterPart.AddNewPart<SlideLayoutPart>("rIdLayout");
            layoutPart.SlideLayout = BuildLayout();
            var themePart = masterPart.AddNewPart<ThemePart>("rIdTheme");
            themePart.Theme = BuildTheme();

            presentationPart.Presentation.SlideMasterIdList = new P.SlideMasterIdList(
                new P.SlideMasterId { Id = (UInt32Value)2147483648U, RelationshipId = "rIdMaster" });
            presentationPart.Presentation.SlideSize = new P.SlideSize
            {
                Cx = (Int32Value)(int)SlideWidth,
                Cy = (Int32Value)(int)SlideHeight
            };
            presentationPart.Presentation.NotesSize = new P.NotesSize { Cx = 6858000, Cy = 9144000 };

            var slideIdList = new P.SlideIdList();
            uint slideId = 256;
            var relIndex = 1;

            void AddSlide(P.Slide slide)
            {
                var relId = $"rIdSlide{relIndex++}";
                var slidePart = presentationPart.AddNewPart<SlidePart>(relId);
                slidePart.Slide = slide;
                slidePart.AddPart(layoutPart);
                slideIdList.Append(new P.SlideId { Id = (UInt32Value)slideId++, RelationshipId = relId });
            }

            AddSlide(TitleSlide(title, subtitle));
            foreach (var section in sections)
                AddSlide(ContentSlide(section.Heading, section.Lines));

            presentationPart.Presentation.SlideIdList = slideIdList;
            presentationPart.Presentation.Save();
        }
        return stream.ToArray();
    }

    // ─────────────────────────── Slides ───────────────────────────

    private static P.Slide TitleSlide(string title, string subtitle) => Slide(
        TextShape(2, "Titulo", 900000, 2300000, SlideWidth - 1800000, 1200000, [(title, 40, true)]),
        TextShape(3, "Subtitulo", 900000, 3600000, SlideWidth - 1800000, 900000, [(subtitle, 18, false)]));

    private static P.Slide ContentSlide(string heading, IReadOnlyList<string> lines)
    {
        var paragraphs = lines.Select(l => (l, 14, false)).ToList();
        return Slide(
            TextShape(2, "Encabezado", 700000, 400000, SlideWidth - 1400000, 800000, [(heading, 28, true)]),
            TextShape(3, "Contenido", 700000, 1400000, SlideWidth - 1400000, SlideHeight - 1800000, paragraphs));
    }

    private static P.Slide Slide(params P.Shape[] shapes)
    {
        var tree = new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = (UInt32Value)1U, Name = "" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new D.TransformGroup()));
        foreach (var shape in shapes) tree.Append(shape);
        return new P.Slide(
            new P.CommonSlideData(tree),
            new P.ColorMapOverride(new D.MasterColorMapping()));
    }

    private static P.Shape TextShape(uint id, string name, long x, long y, long cx, long cy,
        IReadOnlyList<(string Text, int Size, bool Bold)> paragraphs)
    {
        var body = new P.TextBody(
            new D.BodyProperties { Wrap = D.TextWrappingValues.Square },
            new D.ListStyle());
        foreach (var (text, size, bold) in paragraphs)
            body.Append(new D.Paragraph(new D.Run(
                new D.RunProperties { FontSize = size * 100, Bold = bold },
                new D.Text(text))));

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = (UInt32Value)id, Name = name },
                new P.NonVisualShapeDrawingProperties(new D.ShapeLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new D.Transform2D(
                    new D.Offset { X = x, Y = y },
                    new D.Extents { Cx = cx, Cy = cy }),
                new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle }),
            body);
    }

    // ─────────────────────────── Master, layout y tema ───────────────────────────

    private static P.SlideMaster BuildMaster()
    {
        var master = new P.SlideMaster(
            new P.CommonSlideData(new P.ShapeTree(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = (UInt32Value)1U, Name = "" },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.GroupShapeProperties(new D.TransformGroup()))),
            new P.ColorMap
            {
                Background1 = D.ColorSchemeIndexValues.Light1,
                Text1 = D.ColorSchemeIndexValues.Dark1,
                Background2 = D.ColorSchemeIndexValues.Light2,
                Text2 = D.ColorSchemeIndexValues.Dark2,
                Accent1 = D.ColorSchemeIndexValues.Accent1,
                Accent2 = D.ColorSchemeIndexValues.Accent2,
                Accent3 = D.ColorSchemeIndexValues.Accent3,
                Accent4 = D.ColorSchemeIndexValues.Accent4,
                Accent5 = D.ColorSchemeIndexValues.Accent5,
                Accent6 = D.ColorSchemeIndexValues.Accent6,
                Hyperlink = D.ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = D.ColorSchemeIndexValues.FollowedHyperlink
            });
        master.SlideLayoutIdList = new P.SlideLayoutIdList(
            new P.SlideLayoutId { Id = (UInt32Value)2147483649U, RelationshipId = "rIdLayout" });
        return master;
    }

    private static P.SlideLayout BuildLayout() => new(
        new P.CommonSlideData(new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = (UInt32Value)1U, Name = "" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new D.TransformGroup()))),
        new P.ColorMapOverride(new D.MasterColorMapping()));

    private static D.Theme BuildTheme() => new(
        new D.ThemeElements(
            new D.ColorScheme(
                new D.Dark1Color(new D.SystemColor { Val = D.SystemColorValues.WindowText, LastColor = "000000" }),
                new D.Light1Color(new D.SystemColor { Val = D.SystemColorValues.Window, LastColor = "FFFFFF" }),
                new D.Dark2Color(new D.RgbColorModelHex { Val = "16213E" }),
                new D.Light2Color(new D.RgbColorModelHex { Val = "EEF1F6" }),
                new D.Accent1Color(new D.RgbColorModelHex { Val = "3F51B5" }),
                new D.Accent2Color(new D.RgbColorModelHex { Val = "C62828" }),
                new D.Accent3Color(new D.RgbColorModelHex { Val = "2E7D32" }),
                new D.Accent4Color(new D.RgbColorModelHex { Val = "F9A825" }),
                new D.Accent5Color(new D.RgbColorModelHex { Val = "6A1B9A" }),
                new D.Accent6Color(new D.RgbColorModelHex { Val = "00838F" }),
                new D.Hyperlink(new D.RgbColorModelHex { Val = "0563C1" }),
                new D.FollowedHyperlinkColor(new D.RgbColorModelHex { Val = "954F72" }))
            { Name = "QA Guardian" },
            new D.FontScheme(
                new D.MajorFont(
                    new D.LatinFont { Typeface = "Segoe UI" },
                    new D.EastAsianFont { Typeface = "" },
                    new D.ComplexScriptFont { Typeface = "" }),
                new D.MinorFont(
                    new D.LatinFont { Typeface = "Segoe UI" },
                    new D.EastAsianFont { Typeface = "" },
                    new D.ComplexScriptFont { Typeface = "" }))
            { Name = "QA Guardian" },
            new D.FormatScheme(
                new D.FillStyleList(
                    new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }),
                    new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }),
                    new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor })),
                new D.LineStyleList(
                    new D.Outline(new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor })) { Width = 9525 },
                    new D.Outline(new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor })) { Width = 19050 },
                    new D.Outline(new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor })) { Width = 28575 }),
                new D.EffectStyleList(
                    new D.EffectStyle(new D.EffectList()),
                    new D.EffectStyle(new D.EffectList()),
                    new D.EffectStyle(new D.EffectList())),
                new D.BackgroundFillStyleList(
                    new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }),
                    new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }),
                    new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor })))
            { Name = "QA Guardian" }))
    { Name = "QA Guardian" };
}
