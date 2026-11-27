using System;
using System.Collections.Generic;

namespace DeckTooSmall;

// How to squeeze one window onto the screen: shrink it by Scale, then move it
// by X and Y. A point on the window at (px, py) is shown at (X + px * Scale,
// Y + py * Scale).
internal readonly record struct FitTransform(double Scale, double X, double Y)
{
    public static readonly FitTransform None = new(1, 0, 0);

    // Takes the rectangle a window covers and the screen size.
    public static FitTransform For(double left, double top, double width, double height,
        double screenWidth, double screenHeight)
    {
        if (width <= 0 || height <= 0 || screenWidth <= 0 || screenHeight <= 0) return None;

        // Never enlarge: windows that already fit keep their size.
        double scale = Math.Min(1, Math.Min(screenWidth / width, screenHeight / height));

        // Shrink around the window's middle, then pull it back inside the screen.
        double newWidth = width * scale;
        double newHeight = height * scale;
        double newLeft = Math.Clamp(left + (width - newWidth) / 2, 0, Math.Max(0, screenWidth - newWidth));
        double newTop = Math.Clamp(top + (height - newHeight) / 2, 0, Math.Max(0, screenHeight - newHeight));

        return new FitTransform(scale, Math.Round(newLeft - left * scale), Math.Round(newTop - top * scale));
    }

    // Resizes something "width" wide that sits at the bottom middle of the screen,
    // never letting it get wider than the screen.
    public static FitTransform BottomCentre(double width, double zoom, double screenWidth, double screenHeight)
    {
        if (width <= 0 || screenWidth <= 0) return None;
        double scale = Math.Min(zoom, screenWidth / width);
        if (Math.Abs(scale - 1) < 0.001) return None;
        return new FitTransform(scale, Math.Round((1 - scale) * screenWidth / 2), Math.Round((1 - scale) * screenHeight));
    }

    // Shrinks by "scale" and puts the window's top left corner at (screenLeft, 0).
    public static FitTransform Pinned(double left, double top, double scale, double screenLeft) =>
        new(scale, Math.Round(screenLeft - left * scale), Math.Round(-top * scale));

    // Lines windows up from the top left, side by side in the order given. If the
    // row is wider than the screen, all of them shrink a bit more.
    public static FitTransform[] Row(IReadOnlyList<(double Left, double Top, double Width, double Height, double Zoom)> windows,
        double screenWidth, double screenHeight)
    {
        var scales = new double[windows.Count];
        double total = 0;
        for (int i = 0; i < windows.Count; i++)
        {
            // First each window on its own: its chosen size, if the screen allows.
            scales[i] = Math.Min(windows[i].Zoom, Math.Min(screenWidth / windows[i].Width, screenHeight / windows[i].Height));
            total += windows[i].Width * scales[i];
        }
        double squeeze = total > screenWidth ? screenWidth / total : 1;

        var result = new FitTransform[windows.Count];
        double x = 0;
        for (int i = 0; i < windows.Count; i++)
        {
            double scale = scales[i] * squeeze;
            result[i] = Pinned(windows[i].Left, windows[i].Top, scale, x);
            x += windows[i].Width * scale;
        }
        return result;
    }

    // Where the mouse appears to be, as far as the unshrunk window is concerned.
    public int UnmapX(int screenX) => (int)Math.Round((screenX - X) / Scale);
    public int UnmapY(int screenY) => (int)Math.Round((screenY - Y) / Scale);
}
