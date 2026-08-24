using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace KeyFXBoard.App.Services;

public static class IconFactory
{
    private static readonly object Gate = new();
    private static Bitmap? _live;
    private static Bitmap? _muted;

    public static WindowIcon Create(bool muted)
    {
        EnsureBitmaps();
        return new WindowIcon(muted ? _muted! : _live!);
    }

    private static void EnsureBitmaps()
    {
        if (_live is not null && _muted is not null)
        {
            return;
        }

        lock (Gate)
        {
            if (_live is not null && _muted is not null)
            {
                return;
            }

            var source = LoadSource();
            _live = source;
            _muted = DrawMuted(source);
        }
    }

    private static Bitmap LoadSource()
    {
        var assembly = typeof(IconFactory).Assembly.GetName().Name ?? "KeyFXBoard";
        foreach (var uri in new[]
                 {
                     $"avares://{assembly}/Assets/app-icon.png",
                     "avares://KeyFXBoard.App/Assets/app-icon.png",
                     "avares://KeyFXBoard/Assets/app-icon.png"
                 })
        {
            try
            {
                using var stream = AssetLoader.Open(new Uri(uri));
                return new Bitmap(stream);
            }
            catch (Exception)
            {
                // Try the next known assembly name.
            }
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.png");
        if (File.Exists(path))
        {
            return new Bitmap(path);
        }

        return DrawFallback();
    }

    private static Bitmap DrawFallback()
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(64, 64), new Vector(96, 96));
        using (var ctx = bitmap.CreateDrawingContext(true))
        {
            ctx.DrawRectangle(new SolidColorBrush(Color.FromRgb(28, 28, 28)), null, new Rect(0, 0, 64, 64), 12, 12);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromRgb(42, 157, 143)), null, new Rect(18, 22, 28, 20), 4, 4);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }

    private static Bitmap DrawMuted(Bitmap source)
    {
        var size = source.PixelSize;
        var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        using (var ctx = bitmap.CreateDrawingContext(true))
        {
            ctx.DrawImage(source, new Rect(0, 0, size.Width, size.Height));
            ctx.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(140, 28, 28, 28)),
                null,
                new Rect(0, 0, size.Width, size.Height));
            ctx.DrawLine(
                new Pen(Brushes.White, Math.Max(2, size.Width / 16.0)),
                new Point(size.Width * 0.2, size.Height * 0.8),
                new Point(size.Width * 0.8, size.Height * 0.2));
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }
}
