﻿namespace FpsAim;

public interface ISmoothingFunction
{
    float Calculate(float dx, float dy);
}