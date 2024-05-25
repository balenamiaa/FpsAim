using System.Runtime.InteropServices;

namespace AimAssist;

[StructLayout(LayoutKind.Sequential)]
internal struct Int2(int x, int y)
{
    public int X = x;
    public int Y = y;
}


