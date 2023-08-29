// Copyright Pumpkin Games Ltd. All Rights Reserved.

using System;

namespace NakamaFNAPong;

static class Program
{
    [STAThread]
    static void Main()
    {
        new NakamaMultiplayer.NakamaFNAPongGame().Run();
    }
}
