using System;
using System.Runtime.InteropServices;

namespace WebPWrapper
{
	/// <summary>Frame iteration</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct WebPIterator
	{
		public int frame_num;
		/// <summary>equivalent to WEBP_FF_FRAME_COUNT</summary>
		public int num_frames;
		/// <summary>offset relative to the canvas</summary>
		public int x_offset, y_offset;
		/// <summary>dimensions of this frame</summary>
		public int width, height;
		/// <summary>display duration in milliseconds</summary>
		public int duration;
		/// <summary>dispose method for the frame</summary>
		public WebPMuxAnimDispose dispose_method;
		/// <summary>true if 'fragment' contains a full frame. partial images
		/// may still be decoded with the WebP incremental decoder</summary>
		public int complete;
		/// <summary>The frame given by 'frame_num'. Note for historical
		/// reasons this is called a fragment</summary>
		public WebPData fragment;
		/// <summary>True if the frame contains transparency</summary>
		public int has_alpha;
		/// <summary>Blend operation for the frame</summary>
		public WebPMuxAnimBlend blend_method;

		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad1;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad2;

		/// <summary>for internal use only</summary>
		private IntPtr private_;
	};
}
