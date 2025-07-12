using System;
using System.Runtime.InteropServices;

namespace WebPWrapper
{
	/// <summary>Main opaque object.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct WebPAnimDecoder
	{
		public IntPtr decoder;
	}
}
