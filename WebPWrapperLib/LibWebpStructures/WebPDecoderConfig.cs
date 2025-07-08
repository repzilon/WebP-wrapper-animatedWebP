using System.Runtime.InteropServices;

namespace WebPWrapper
{
	[StructLayout(LayoutKind.Sequential)]
	internal struct WebPDecoderConfig
	{
		/// <summary>Immutable bit stream features (optional)</summary>
		public WebPBitstreamFeatures input;

		/// <summary>Output buffer (can point to external memory)</summary>
		public WebPDecBuffer output;

		/// <summary>Decoding options</summary>
		public WebPDecoderOptions options;
	}
}
