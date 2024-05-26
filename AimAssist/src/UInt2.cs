using System.Runtime.InteropServices;

namespace AimAssist;

[StructLayout(LayoutKind.Sequential)]
internal struct UInt2(uint x, uint y)
{
    public uint X = x;
    public uint Y = y;
}


