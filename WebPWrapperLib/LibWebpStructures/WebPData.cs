using System;
using System.Runtime.InteropServices;

namespace WebPWrapper
{
	/// <summary>
	/// Data type used to describe 'raw' data, e.g., chunk data
	/// (ICC profile, metadata) and WebP compressed image data.
	/// 'bytes' memory must be allocated using WebPMalloc() and such.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct WebPData
	{
		public IntPtr data;
		public UInt64 size;
	}
}