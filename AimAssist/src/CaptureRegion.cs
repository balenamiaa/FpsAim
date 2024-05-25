using System.Runtime.InteropServices;

namespace AimAssist;

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureRegion
{
    public Int2 CaptureSize;
    public Int2 Unused;
}


