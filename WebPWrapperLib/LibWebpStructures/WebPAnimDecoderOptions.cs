using System;
using System.Runtime.InteropServices;

namespace WebPWrapper
{
	/// <summary>Anim decoder options</summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct WebPAnimDecoderOptions
	{
		/// <summary>Output colorspace. Only the following modes are supported:
		/// MODE_RGBA, MODE_BGRA, MODE_rgbA and MODE_bgrA.</summary>
		public WEBP_CSP_MODE color_mode;
		/// <summary>If true, use multi-threaded decoding</summary>
		public int use_threads;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad1;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad2;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad3;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad4;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad5;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad6;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad7;
	};
}