// File: Win32.cs
// Created by NoTimeForHero, 2023
// Distributed under the Apache License 2.0

using System;
using System.Runtime.InteropServices;

namespace Loader.External
{
    internal static class Win32
    {
        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32")]
        public static extern bool AllocConsole();
    }
}