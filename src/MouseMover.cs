using System.Runtime.InteropServices;

namespace FpsAim;

public sealed partial class MouseMover : IDisposable
{
    private readonly IntPtr _context = Interception.interception_create_context();
    private readonly int _device = GetMouseDevice();

    public void Dispose()
    {
        Interception.interception_destroy_context(_context);
    }

    private static int GetMouseDevice()
    {
        var device = 12;
        while (true)
        {
            if (device >= 30) throw new Exception("No mouse device found");

            if (Interception.interception_is_mouse(device) != 0) return device;

            device++;
        }
    }

    public void MoveRelative(int dx, int dy)
    {
        var stroke = new Interception.InterceptionMouseStroke
        {
            state = 0,
            flags = Interception.InterceptionMouseMoveRelative,
            rolling = 0,
            x = dx,
            y = dy,
            information = 0
        };

        var result = Interception.interception_send(_context, _device, stroke, 1);

        if (result != 1) throw new Exception("Mouse movement error");
    }

    public static bool IsMouse5Down()
    {
        return GetAsyncKeyState(0x06) != 0;
    }

    public static bool IsMouse4Down()
    {
        return GetAsyncKeyState(0x05) != 0;
    }


    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int vKey);
}