namespace MagickCrop.Models;

public record AspectRatioItem
{
    public string Name { get; set; } = string.Empty;
    public string ToolTip { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double RatioValue { get; set; } = 0;
    public AspectRatio AspectRatioEnum { get; set; }

    public static List<AspectRatioItem> GetStandardAspectRatios() =>
        [
            new AspectRatioItem()
            {
                ToolTip = "8.5 tall by 11 wide",
                Symbol = "DocumentLandscape24",
                Text = "Letter Landscape",
                RatioValue = 8.5 / 11,
                AspectRatioEnum = AspectRatio.LetterLandscape
            },
            new AspectRatioItem
            {
                ToolTip = "11 tall by 8.5 wide",
                Symbol = "Document24",
                Text = "Letter Portrait",
                RatioValue = 11 / 8.5,
                AspectRatioEnum = AspectRatio.LetterPortrait
            },
            new AspectRatioItem
            {
                ToolTip = "1 by 1",
                Symbol = "RatioOneToOne24",
                Text = "Square",
                RatioValue = 1,
                AspectRatioEnum = AspectRatio.Square
            },
            new AspectRatioItem
            {
                ToolTip = "210 tall by 297 wide",
                Symbol = "DocumentLandscape24",
                Text = "A4 Landscape",
                RatioValue = 210.0 / 297.0,
                AspectRatioEnum = AspectRatio.A4Landscape
            },
            new AspectRatioItem
            {
                ToolTip = "297 tall by 210 wide",
                Symbol = "Document24",
                Text = "A4 Portrait",
                RatioValue = 297.0 / 210.0,
                AspectRatioEnum = AspectRatio.A4Portrait
            },
            new AspectRatioItem
            {
                ToolTip = "6.14 tall by 2.61 wide",
                Symbol = "AlignEndHorizontal20",
                Text = "US Bill Portrait",
                RatioValue = 6.14 / 2.61,
                AspectRatioEnum = AspectRatio.UsDollarBillPortrait
            },
            new AspectRatioItem
            {
                ToolTip = "2.61 tall by 6.14 wide",
                Symbol = "AlignEndVertical20",
                Text = "US Bill Landscape",
                RatioValue = 2.61 / 6.14,
                AspectRatioEnum = AspectRatio.UsDollarBillLandscape
            },
            new AspectRatioItem
            {
                ToolTip = "Whatever aspect ratio you choose",
                Symbol = "TableEdit24",
                Text = "Custom...",
                RatioValue = 0, // Custom aspect ratio to be defined by user
                AspectRatioEnum = AspectRatio.Custom
            }
        ];
}
