using System;
using System.Runtime.InteropServices;

namespace WebPWrapper
{
	/// <summary>Output buffer</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct WebPDecBuffer
	{
		/// <summary>Color space</summary>
		public WEBP_CSP_MODE colorspace;
		/// <summary>Width of image</summary>
		public int width;
		/// <summary>Height of image</summary>
		public int height;
		/// <summary>If non-zero, 'internal_memory' pointer is not used. If value is '2' or more, the external memory is considered 'slow' and multiple read/write will be avoided</summary>
		public int is_external_memory;
		/// <summary>Output buffer parameters</summary>
		public RGBA_YUVA_Buffer u;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad1;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad2;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad3;
		/// <summary>Padding for later use</summary>
		private readonly UInt32 pad4;
		/// <summary>Internally allocated memory (only when is_external_memory is 0). Should not be used externally, but accessed via WebPRGBABuffer</summary>
		public IntPtr private_memory;
	}
}