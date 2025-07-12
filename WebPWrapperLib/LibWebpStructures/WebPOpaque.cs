using System;
using System.Runtime.InteropServices;

namespace WebPWrapper
{
	/// <summary>WebP library opaque object</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct WebPOpaque
	{
		public IntPtr library;
	}
}
