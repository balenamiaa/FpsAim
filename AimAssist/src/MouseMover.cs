using System.Runtime.InteropServices;

namespace AimAssist;

internal readonly partial struct MouseMover() : IDisposable
{
    [LibraryImport("interception.dll")]
    private static partial nint interception_create_context();

    [LibraryImport("interception.dll")]
    private static partial void interception_destroy_context(nint context);

    [LibraryImport("interception.dll")]
    private static partial int interception_is_mouse(int device);

    [LibraryImport("interception.dll")]
    private static partial int interception_send(nint context, int device, MouseStroke[] strokes, uint nStrokes);

    private const int InterceptionMouseLeftButtonDown = 0x001;
    private const int InterceptionMouseLeftButtonUp = 0x002;
    private const int InterceptionMouseMoveRelative = 0x000;

    private readonly nint _context = interception_create_context();
    private readonly int _mouseDevice = GetMouseDevice();

    public void MoveRelative(int dx, int dy)
    {
        var stroke = new MouseStroke
        {
            State = 0,
            Flags = InterceptionMouseMoveRelative,
            X = dx,
            Y = dy
        };

        interception_send(_context, _mouseDevice, [stroke], 1);
    }

    private static int GetMouseDevice()
    {
        for (int i = 1; i < 21; i++)
        {
            if (interception_is_mouse(i) > 0)
                return i;
        }

        throw new InvalidOperationException("No mouse device found.");
    }

    public void Dispose()
    {
        interception_destroy_context(_context);
    }
}
