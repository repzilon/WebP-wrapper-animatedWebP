using System;
using System.Runtime.InteropServices;

namespace WebPWrapper
{
	/// <summary>Main opaque object.</summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct WebPAnimDecoder
	{
		public IntPtr decoder;
	}
}