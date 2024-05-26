using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

namespace AimAssist;


internal class ImageExtractorModule(string outputDirectory, uint monitorIndex, int width, int height)
{

    private readonly ScreenCapturer _screenCapturer = new(0, monitorIndex, (uint)width, (uint)height);
    public async Task StartLoop()
    {
        if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);

        var lastTime = Stopwatch.StartNew();
        while (true)
        {
            if (!MouseInterop.IsButtonDown(MouseButton.XButton1)) goto loopEnd;
            if (lastTime.ElapsedMilliseconds <= 500) goto loopEnd;
            var frame = _screenCapturer.CaptureFrame();

            if (frame is ScreenCaptureOutputAvailable screenCaptureOutput)
                unsafe
                {
                    var data = screenCaptureOutput.GetCpuTensor();

                    var path = Path.Combine(outputDirectory, $"{Guid.NewGuid()}.png");
                    using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                    var bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                        bitmap.PixelFormat);
                    var pBmp = (byte*)bmpData.Scan0;

                    Parallel.For(0, height, y =>
                        {
                            for (var x = 0; x < width; x++)
                            {
                                var pixelR = data.Span[y * width + x];
                                var pixelG = data.Span[y * width + x + width * height];
                                var pixelB = data.Span[y * width + x + 2 * width * height];
                                pBmp[y * bmpData.Stride + x * 3] = (byte)(pixelB * 255);
                                pBmp[y * bmpData.Stride + x * 3 + 1] = (byte)(pixelG * 255);
                                pBmp[y * bmpData.Stride + x * 3 + 2] = (byte)(pixelR * 255);
                            }
                        });

                    bitmap.UnlockBits(bmpData);
                    bitmap.Save(path, ImageFormat.Png);

                    lastTime.Restart();
                }

            loopEnd:
            await Task.Delay(1);
        }
    }
}