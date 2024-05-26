using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AimAssist;

[StructLayout(LayoutKind.Sequential)]
unsafe internal struct CaptureRegion
{
    public UInt2 CaptureSize;
    private fixed byte __unused_align[8];
}


