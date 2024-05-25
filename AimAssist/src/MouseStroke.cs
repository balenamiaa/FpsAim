using System.Runtime.InteropServices;

namespace AimAssist;

[StructLayout(LayoutKind.Sequential)]
internal struct MouseStroke
{
    public ushort State;
    public ushort Flags;
    public short Rolling;
    public int X;
    public int Y;
    public uint Information;
}
