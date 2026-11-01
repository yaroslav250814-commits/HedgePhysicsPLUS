using UnityEngine;

// Colours follow the supplied SA2 praise reference; keyed by name so scoring rules stay independent.
public static class TechniquePraisePalette
{
    static readonly Gradient PerfectRainbow = CreateRainbow();

    public static void Apply(AdventureTextGradient text, string praise)
    {
        switch (praise)
        {
            case "Good!": text.SetPalette(Rgb(153,255,255), Rgb(34,187,255)); break;
            case "Great!": text.SetPalette(Rgb(153,255,255), Rgb(0,238,238)); break;
            case "Nice!": text.SetPalette(Rgb(153,255,221), Rgb(85,255,153)); break;
            case "Jammin'!": text.SetPalette(Rgb(255,255,153), Rgb(238,255,0)); break;
            case "Cool!": text.SetPalette(Rgb(255,255,153), Rgb(255,204,0)); break;
            case "Radical!": text.SetPalette(Rgb(255,221,153), Rgb(255,170,0)); break;
            case "Tight!": text.SetPalette(Rgb(255,187,119), Rgb(255,102,0)); break;
            case "Awesome!": text.SetPalette(Rgb(255,170,136), Rgb(255,102,34)); break;
            case "Extreme!": text.SetPalette(Rgb(255,153,221), Rgb(255,0,153)); break;
            case "Perfect!": text.SetPalette(Color.white, Color.white, PerfectRainbow); break;
            default: text.SetPalette(Rgb(255,241,100), Rgb(255,65,0)); break;
        }
    }

    static Color Rgb(byte r, byte g, byte b) => new Color32(r,g,b,255);

    static Gradient CreateRainbow()
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[]
        {
            new GradientColorKey(Rgb(204,255,255),0),
            new GradientColorKey(Rgb(153,255,170),.18f),
            new GradientColorKey(Rgb(255,255,102),.35f),
            new GradientColorKey(Rgb(255,153,85),.52f),
            new GradientColorKey(Rgb(255,34,255),.72f),
            new GradientColorKey(Rgb(0,255,255),1)
        },new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)});
        return gradient;
    }
}
