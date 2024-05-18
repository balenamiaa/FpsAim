using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using SharpGen.Runtime;

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
        var screenCapturer = new ScreenCapturer(0);
        var lastTime = new Stopwatch();
        lastTime.Start();
        while (true)
            unsafe
            {
                if (!IsButtonHeldMouse5()) continue;
                if (lastTime.ElapsedMilliseconds <= 500) continue;

                var frame = screenCapturer.CaptureFrame();
                if (frame.Length == 0) continue;

                var path = Path.Combine(_outputDirectory, $"{Guid.NewGuid()}.png");
                using var bitmap = new Bitmap(640, 640, PixelFormat.Format32bppArgb);
                var bmpData = bitmap.LockBits(new Rectangle(0, 0, 640, 640), ImageLockMode.WriteOnly,
                    bitmap.PixelFormat);
                Buffer.MemoryCopy(frame.GetPointerUnsafe(), bmpData.Scan0.ToPointer(), frame.Length, frame.Length);

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