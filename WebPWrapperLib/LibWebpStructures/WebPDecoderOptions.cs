using System;
using System.Runtime.InteropServices;

namespace WebPWrapper
{
	/// <summary>Decoding options</summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct WebPDecoderOptions
	{
		/// <summary>If true, skip the in-loop filtering</summary>
		public int bypass_filtering;

		/// <summary>If true, use faster point-wise up-sampler</summary>
		public int no_fancy_upsampling;

		/// <summary>If true, cropping is applied _first_</summary>
		public int use_cropping;

		/// <summary>Left position for cropping. Will be snapped to even values</summary>
		public int crop_left;

		/// <summary>Top position for cropping. Will be snapped to even values</summary>
		public int crop_top;

		/// <summary>Width of the cropping area</summary>
		public int crop_width;

		/// <summary>Height of the cropping area</summary>
		public int crop_height;

		/// <summary>If true, scaling is applied _afterward_</summary>
		public int use_scaling;

		/// <summary>Final width</summary>
		public int scaled_width;

		/// <summary>Final height</summary>
		public int scaled_height;

		/// <summary>If true, use multi-threaded decoding</summary>
		public int use_threads;

		/// <summary>Dithering strength (0=Off, 100=full)</summary>
		public int dithering_strength;

		/// <summary>Flip output vertically</summary>
		public int flip;

		/// <summary>Alpha dithering strength in [0..100]</summary>
		public int alpha_dithering_strength;

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
	}
}
