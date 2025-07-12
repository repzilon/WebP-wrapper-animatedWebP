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
	internal struct WebPData
	{
		public IntPtr data;
		public UInt64 size;

		public WebPData(IntPtr data, UInt64 size)
		{
			this.data = data;
			this.size = size;
		}

		public static WebPData Create(byte[] managed, out GCHandle pinned)
		{
			pinned = GCHandle.Alloc(managed, GCHandleType.Pinned);
			return new WebPData(pinned.AddrOfPinnedObject(), Convert.ToUInt64(managed.Length));
		}
	}
}
