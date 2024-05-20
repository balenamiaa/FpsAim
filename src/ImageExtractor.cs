using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

namespace FpsAim;

public class ImageExtractor
{
    private readonly string _outputDirectory;

    public ImageExtractor(string outputDirectory)
    {
        _outputDirectory = outputDirectory;

        if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);
    }

    public void BeginLoop()
    {
        var width = 640;
        var height = 640;
        var screenCapturer = new ScreenCapturer(0, width, height);
        var lastTime = new Stopwatch();
        lastTime.Start();
        while (true)
            unsafe
            {
                if (!IsButtonHeldMouse5()) continue;
                if (lastTime.ElapsedMilliseconds <= 500) continue;

                var frame = screenCapturer.CaptureFrame();
                if (frame is null) continue;

                var path = Path.Combine(_outputDirectory, $"{Guid.NewGuid()}.png");
                using var bitmap = new Bitmap(640, 640, PixelFormat.Format24bppRgb);
                var bmpData = bitmap.LockBits(new Rectangle(0, 0, 640, 640), ImageLockMode.WriteOnly,
                    bitmap.PixelFormat);
                var pBmp = (byte*)bmpData.Scan0;
                Parallel.For(0, height, y =>
                {
                    for (var x = 0; x < width; x++)
                    {
                        var pixelR = frame.Value.Span[y * width + x];
                        var pixelG = frame.Value.Span[y * width + x + width * height];
                        var pixelB = frame.Value.Span[y * width + x + 2 * width * height];
                        pBmp[y * bmpData.Stride + x * 3] = (byte)(pixelB * 255);
                        pBmp[y * bmpData.Stride + x * 3 + 1] = (byte)(pixelG * 255);
                        pBmp[y * bmpData.Stride + x * 3 + 2] = (byte)(pixelR * 255);
                    }
                });

                bitmap.UnlockBits(bmpData);
                bitmap.Save(path, ImageFormat.Png);
                lastTime.Restart();
            }
    }

    private static bool IsButtonHeldMouse5()
    {
        return (MouseMover.GetAsyncKeyState(0x06) & 0x8000) != 0;
    }
}