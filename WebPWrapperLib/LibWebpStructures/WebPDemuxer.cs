using System;
using System.Runtime.InteropServices;

namespace WebPWrapper
{
	/// <summary>WebP container demux (opaque object)</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct WebPDemuxer
	{
		public IntPtr demuxer;
	}
}
