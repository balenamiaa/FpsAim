using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FpsAim;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static partial class Interception
{
    public const int InterceptionMaxKeyboard = 10;
    public const int InterceptionMaxMouse = 10;
    public const int InterceptionMaxDevice = InterceptionMaxKeyboard + InterceptionMaxMouse;

    public const ushort InterceptionKeyDown = 0x00;
    public const ushort InterceptionKeyUp = 0x01;
    public const ushort InterceptionKeyE0 = 0x02;
    public const ushort InterceptionKeyE1 = 0x04;
    public const ushort InterceptionKeyTermsrvSetLed = 0x08;
    public const ushort InterceptionKeyTermsrvShadow = 0x10;
    public const ushort InterceptionKeyTermsrvVkpacket = 0x20;

    public const ushort InterceptionFilterKeyNone = 0x0000;
    public const ushort InterceptionFilterKeyAll = 0xFFFF;
    public const ushort InterceptionFilterKeyDown = InterceptionKeyUp;
    public const ushort InterceptionFilterKeyUp = InterceptionKeyUp << 1;
    public const ushort InterceptionFilterKeyE0 = InterceptionKeyE0 << 1;
    public const ushort InterceptionFilterKeyE1 = InterceptionKeyE1 << 1;
    public const ushort InterceptionFilterKeyTermsrvSetLed = InterceptionKeyTermsrvSetLed << 1;
    public const ushort InterceptionFilterKeyTermsrvShadow = InterceptionKeyTermsrvShadow << 1;
    public const ushort InterceptionFilterKeyTermsrvVkpacket = InterceptionKeyTermsrvVkpacket << 1;

    public const ushort InterceptionMouseLeftButtonDown = 0x001;
    public const ushort InterceptionMouseLeftButtonUp = 0x002;
    public const ushort InterceptionMouseRightButtonDown = 0x004;
    public const ushort InterceptionMouseRightButtonUp = 0x008;
    public const ushort InterceptionMouseMiddleButtonDown = 0x010;
    public const ushort InterceptionMouseMiddleButtonUp = 0x020;

    public const ushort InterceptionMouseButton1Down = InterceptionMouseLeftButtonDown;
    public const ushort InterceptionMouseButton1Up = InterceptionMouseLeftButtonUp;
    public const ushort InterceptionMouseButton2Down = InterceptionMouseRightButtonDown;
    public const ushort InterceptionMouseButton2Up = InterceptionMouseRightButtonUp;
    public const ushort InterceptionMouseButton3Down = InterceptionMouseMiddleButtonDown;
    public const ushort InterceptionMouseButton3Up = InterceptionMouseMiddleButtonUp;

    public const ushort InterceptionMouseButton4Down = 0x040;
    public const ushort InterceptionMouseButton4Up = 0x080;
    public const ushort InterceptionMouseButton5Down = 0x100;
    public const ushort InterceptionMouseButton5Up = 0x200;

    public const ushort InterceptionMouseWheel = 0x400;
    public const ushort InterceptionMouseHwheel = 0x800;

    public const ushort InterceptionFilterMouseNone = 0x0000;
    public const ushort InterceptionFilterMouseAll = 0xFFFF;

    public const ushort InterceptionFilterMouseLeftButtonDown = InterceptionMouseLeftButtonDown;
    public const ushort InterceptionFilterMouseLeftButtonUp = InterceptionMouseLeftButtonUp;
    public const ushort InterceptionFilterMouseRightButtonDown = InterceptionMouseRightButtonDown;
    public const ushort InterceptionFilterMouseRightButtonUp = InterceptionMouseRightButtonUp;

    public const ushort InterceptionFilterMouseMiddleButtonDown =
        InterceptionMouseMiddleButtonDown;

    public const ushort InterceptionFilterMouseMiddleButtonUp = InterceptionMouseMiddleButtonUp;

    public const ushort InterceptionFilterMouseButton1Down = InterceptionMouseButton1Down;
    public const ushort InterceptionFilterMouseButton1Up = InterceptionMouseButton1Up;
    public const ushort InterceptionFilterMouseButton2Down = InterceptionMouseButton2Down;
    public const ushort InterceptionFilterMouseButton2Up = InterceptionMouseButton2Up;
    public const ushort InterceptionFilterMouseButton3Down = InterceptionMouseButton3Down;
    public const ushort InterceptionFilterMouseButton3Up = InterceptionMouseButton3Up;

    public const ushort InterceptionFilterMouseButton4Down = InterceptionMouseButton4Down;
    public const ushort InterceptionFilterMouseButton4Up = InterceptionMouseButton4Up;
    public const ushort InterceptionFilterMouseButton5Down = InterceptionMouseButton5Down;
    public const ushort InterceptionFilterMouseButton5Up = InterceptionMouseButton5Up;

    public const ushort InterceptionFilterMouseWheel = InterceptionMouseWheel;
    public const ushort InterceptionFilterMouseHwheel = InterceptionMouseHwheel;

    public const ushort InterceptionFilterMouseMove = 0x1000;

    public const ushort InterceptionMouseMoveRelative = 0x000;
    public const ushort InterceptionMouseMoveAbsolute = 0x001;
    public const ushort InterceptionMouseVirtualDesktop = 0x002;
    public const ushort InterceptionMouseAttributesChanged = 0x004;
    public const ushort InterceptionMouseMoveNocoalesce = 0x008;
    public const ushort InterceptionMouseTermsrvSrcShadow = 0x100;

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial IntPtr interception_create_context();

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void interception_destroy_context(IntPtr context);

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int interception_get_precedence(IntPtr context, int device);

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void interception_set_precedence(IntPtr context, int device, int precedence);

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial ushort interception_get_filter(IntPtr context, int device);

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void interception_set_filter(IntPtr context, IntPtr predicate, ushort filter);

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int interception_wait(IntPtr context);

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int interception_wait_with_timeout(IntPtr context, ulong milliseconds);

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int interception_send(IntPtr context, int device,
        in InterceptionMouseStroke stroke, uint nstroke);

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int interception_receive(IntPtr context, int device,
        out InterceptionMouseStroke stroke, uint nstroke);

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint interception_get_hardware_id(IntPtr context, int device, IntPtr hardwareIdBuffer,
        uint bufferSize);

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int interception_is_invalid(int device);

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int interception_is_keyboard(int device);

    [LibraryImport("interception")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int interception_is_mouse(int device);

    [StructLayout(LayoutKind.Sequential)]
    public struct InterceptionMouseStroke
    {
        public ushort state;
        public ushort flags;
        public short rolling;
        public int x;
        public int y;
        public uint information;
    }
}