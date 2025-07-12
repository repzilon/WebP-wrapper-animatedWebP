using System;
using System.Runtime.InteropServices;

namespace WebPWrapper
{
	/// <summary>Global information about the animation</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct WebPAnimInfo
	{
		public UInt32 canvas_width;
		public UInt32 canvas_height;
		public UInt32 loop_count;
		public UInt32 bgcolor;
		public UInt32 frame_count;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad1;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad2;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad3;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad4;
	}
}
