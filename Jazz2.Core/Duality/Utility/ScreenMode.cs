﻿using System;

namespace Duality
{
    /// <summary>
    /// Describes the way a Duality window is set up.
    /// </summary>
    /*public enum ScreenMode
	{
		/// <summary>
		/// Duality runs in windowed mode. The window can be resized by the user.
		/// </summary>
		Window,
		/// <summary>
		/// Duality runs in windowed mode. The window has a fixed size.
		/// </summary>
		FixedWindow,
		/// <summary>
		/// Duality runs in windowed mode. The window is borderless and covers the whole screen.
		/// </summary>
		FullWindow,
		/// <summary>
		/// Duality runs in fullscreen mode, using whatever screen resolution is currently active on the users desktop.
		/// </summary>
		Native,
		/// <summary>
		/// Duality runs in fullscreen mode and changes desktop resolution whenever necesary.
		/// </summary>
		Fullscreen
	}*/

    [Flags]
    public enum ScreenMode
    {
        Window = 0,

        FixedSize = 1 << 0,
        FullWindow = 1 << 1,
        ChangeResolution = 1 << 2,

        Immersive = 1 << 3,
    }
}