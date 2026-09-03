using MudBlazor;

namespace Elevkollen.Layout;

/// <summary>
/// Appens enda färgtema. Båda lägena delar samma mint/blågröna släkt och undviker
/// mättade signalfärger, så att skärmen går att titta på länge.
/// Ljust läge är luftig mint med mörkgrön text i stället för svart på vitt;
/// mörkt läge är blågrönt snarare än svart. MudBlazors standardpaletter används inte.
/// </summary>
internal static class AppTheme
{
    // Delade accentfärger. Samma nyanser i båda lägena håller ikoner och knappar igenkännbara.
    private const string Mint = "#3FA894";
    private const string MintSoft = "#5FBFAC";
    private const string Teal = "#4E8D9C";
    private const string Sage = "#7FA98F";

    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Mint,
            PrimaryContrastText = "#FFFFFF",
            PrimaryDarken = "#2E8F7D",
            PrimaryLighten = "#7FCFBE",
            Secondary = Teal,
            SecondaryContrastText = "#FFFFFF",
            SecondaryDarken = "#3D7686",
            SecondaryLighten = "#82B4C0",
            Tertiary = Sage,
            TertiaryContrastText = "#FFFFFF",

            Black = "#1B302A",
            White = "#FFFFFF",

            // Ljus, luftig mint. Aldrig rent vitt — en svag grön underton vilar ögat.
            Background = "#F4FAF7",
            BackgroundGray = "#E9F3EF",
            Surface = "#FFFFFF",

            AppbarBackground = "#E4F1EC",
            AppbarText = "#2A4A42",
            DrawerBackground = "#F0F8F4",
            DrawerText = "#3A5B53",
            DrawerIcon = "#4C7168",

            // Mjuk mörkgrön text i stället för svart, så kontrasten inte blir hård.
            TextPrimary = "#27473F",
            TextSecondary = "#5F8177",
            TextDisabled = "#27473F5E",

            ActionDefault = "#5F8177",
            ActionDisabled = "#27473F40",
            ActionDisabledBackground = "#27473F1A",

            Info = "#4E8D9C",
            InfoContrastText = "#FFFFFF",
            Success = "#3FA07E",
            SuccessContrastText = "#FFFFFF",
            Warning = "#C79A4E",
            WarningContrastText = "#FFFFFF",
            Error = "#C4707A",
            ErrorContrastText = "#FFFFFF",
            Dark = "#2C4A44",
            DarkContrastText = "#FFFFFF",

            LinesDefault = "#D5E6DF",
            LinesInputs = "#BCD6CC",
            TableLines = "#DFEDE7",
            TableStriped = "#F4FAF7",
            TableHover = "#E9F3EF",
            Divider = "#DFEDE7",
            DividerLight = "#EDF6F2",

            GrayDefault = "#8CAAA1",
            GrayLight = "#E6F0EC",
            GrayLighter = "#F3F9F6",
            GrayDark = "#5F8177",
            GrayDarker = "#3A5B53",

            OverlayLight = "#F4FAF7B3",
            OverlayDark = "#1B302A66",
            HoverOpacity = 0.05,
        },
        PaletteDark = new PaletteDark
        {
            Primary = MintSoft,
            PrimaryContrastText = "#0F1C1A",
            PrimaryDarken = "#49A893",
            PrimaryLighten = "#88D6C6",
            Secondary = "#6BA7B4",
            SecondaryContrastText = "#0F1C1A",
            SecondaryDarken = "#55909D",
            SecondaryLighten = "#94C4CE",
            Tertiary = "#8FBBA1",
            TertiaryContrastText = "#0F1C1A",

            Black = "#0C1614",
            White = "#F2F7F5",

            // Blågrön mörkerton, inte svart. Mjukare för ögat i mörka rum.
            Background = "#162322",
            BackgroundGray = "#111C1B",
            Surface = "#1E2E2C",

            AppbarBackground = "#1A2827",
            AppbarText = "#C3D7D1",
            DrawerBackground = "#162322",
            DrawerText = "#A9C2BB",
            DrawerIcon = "#8FAAA3",

            TextPrimary = "#DCE9E4",
            TextSecondary = "#9BB4AD",
            TextDisabled = "#DCE9E459",

            ActionDefault = "#9BB4AD",
            ActionDisabled = "#DCE9E440",
            ActionDisabledBackground = "#DCE9E41F",

            Info = "#6BA7B4",
            InfoContrastText = "#0F1C1A",
            Success = "#59B893",
            SuccessContrastText = "#0F1C1A",
            Warning = "#D9AE68",
            WarningContrastText = "#0F1C1A",
            Error = "#D98A93",
            ErrorContrastText = "#0F1C1A",
            Dark = "#0F1C1A",
            DarkContrastText = "#DCE9E4",

            LinesDefault = "#2C403D",
            LinesInputs = "#3A524E",
            TableLines = "#2C403D",
            TableStriped = "#1A2A28",
            TableHover = "#243634",
            Divider = "#283B38",
            DividerLight = "#22332F",

            GrayDefault = "#7D958F",
            GrayLight = "#2A3D3A",
            GrayLighter = "#22332F",
            GrayDark = "#5E7873",
            GrayDarker = "#425853",

            OverlayLight = "#1E2E2C80",
            OverlayDark = "#0C161499",
            HoverOpacity = 0.08,
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { LetterSpacing = "normal" },
            H5 = new H5Typography { FontWeight = "500" },
            H6 = new H6Typography { FontWeight = "500" },
            Subtitle1 = new Subtitle1Typography { FontWeight = "500" },
            Button = new ButtonTypography { FontWeight = "500", TextTransform = "none" },
        },
    };
}
