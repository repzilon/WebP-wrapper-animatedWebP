using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WebPWrapper
{
	internal static class UnsafeNativeMethods
	{
		#region | Windows functions |
		private static readonly bool IsNetCore3CompatRuntime;
		internal static void CopyMemory(IntPtr dest, IntPtr src, uint count)
		{
			if (IsNetCore3CompatRuntime) CopyMemory_Core(dest, src, count);
			else CopyMemory_Framework(dest, src, count);
		}

		[DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
		internal static extern void CopyMemory_Framework(IntPtr dest, IntPtr src, uint count);

		[DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
		internal static extern void CopyMemory_Core(IntPtr dest, IntPtr src, uint count);


		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = false)]
		private static extern IntPtr LoadLibrary(string lpFileName);

		static UnsafeNativeMethods()
		{
			string path = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), IntPtr.Size == 4 ? "x86" : "x64");
			string[] files = new string[]
			{
				Path.Combine(path, "libsharpyuv.dll"),
				Path.Combine(path, "libwebp.dll"),
				Path.Combine(path, "libwebpdecoder.dll"),
				Path.Combine(path, "libwebpdemux.dll"),
			};
			foreach (string f in files) {
				if (File.Exists(f))
					LoadLibrary(f);
			}

			// FIX incompatible entry-points for NET Framework/core <= 2 and later NET(core) versions:
			//     CopyMemory vs. RtlMoveMemory
			// -> WORKAROUND: detect, if entrypoint is CopyMemory or RtlMoveMemory
			// see https://github.com/dotnet/runtime/issues/12496
			Nullable<IntPtr> memorySource = null;
			try {
				const int size = 200;
				memorySource = Marshal.AllocHGlobal(size);
				Marshal.WriteInt32(memorySource.Value, 42);
				IntPtr memoryTarget = Marshal.AllocHGlobal(size);
				CopyMemory_Framework(memoryTarget, memorySource.Value, size);
				if (Marshal.ReadInt32(memoryTarget) == 42) IsNetCore3CompatRuntime = false;
				else IsNetCore3CompatRuntime = true;
			} catch (EntryPointNotFoundException) {
				IsNetCore3CompatRuntime = true;
			} finally {
				if (memorySource.HasValue) {
					Marshal.FreeHGlobal(memorySource.Value);
				}
			}
		}
		#endregion

		#region | Import libwebp functions |
		private static readonly int WEBP_DECODER_ABI_VERSION = 0x0209;

		/// <summary>This function will initialize the configuration according to a predefined set of parameters (referred to by 'preset') and a given quality factor</summary>
		/// <param name="config">The WebPConfig structure</param>
		/// <param name="preset">Type of image</param>
		/// <param name="quality">Quality of compression</param>
		/// <returns>0 if error</returns>
		internal static int WebPConfigInit(ref WebPConfig config, WebPPreset preset, float quality)
		{
			return WebPConfigInitInternal(ref config, preset, quality, WEBP_DECODER_ABI_VERSION);
		}
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPConfigInitInternal")]
		private static extern int WebPConfigInitInternal(ref WebPConfig config, WebPPreset preset, float quality, int WEBP_DECODER_ABI_VERSION);

		/// <summary>Get info of WepP image</summary>
		/// <param name="rawWebP">Bytes[] of WebP image</param>
		/// <param name="data_size">Size of rawWebP</param>
		/// <param name="features">Features of WebP image</param>
		/// <returns>VP8StatusCode</returns>
		internal static VP8StatusCode WebPGetFeatures(IntPtr rawWebP, int data_size, ref WebPBitstreamFeatures features)
		{
			return WebPGetFeaturesInternal(rawWebP, (UIntPtr)data_size, ref features, WEBP_DECODER_ABI_VERSION);
		}

		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPGetFeaturesInternal")]
		private static extern VP8StatusCode WebPGetFeaturesInternal([In] IntPtr rawWebP, UIntPtr data_size, ref WebPBitstreamFeatures features, int WEBP_DECODER_ABI_VERSION);

		/// <summary>Activate the lossless compression mode with the desired efficiency</summary>
		/// <param name="config">The WebPConfig struct</param>
		/// <param name="level">between 0 (fastest, lowest compression) and 9 (slower, best compression)</param>
		/// <returns>0 in case of parameter error</returns>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPConfigLosslessPreset")]
		internal static extern int WebPConfigLosslessPreset(ref WebPConfig config, int level);

		/// <summary>Check that configuration is non-NULL and all configuration parameters are within their valid ranges</summary>
		/// <param name="config">The WebPConfig structure</param>
		/// <returns>1 if configuration is OK</returns>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPValidateConfig")]
		internal static extern int WebPValidateConfig(ref WebPConfig config);

		/// <summary>Initialize the WebPPicture structure checking the DLL version</summary>
		/// <param name="wpic">The WebPPicture structure</param>
		/// <returns>1 if not error</returns>
		internal static int WebPPictureInitInternal(ref WebPPicture wpic)
		{
			return WebPPictureInitInternal(ref wpic, WEBP_DECODER_ABI_VERSION);
		}

		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureInitInternal")]
		private static extern int WebPPictureInitInternal(ref WebPPicture wpic, int WEBP_DECODER_ABI_VERSION);

		/// <summary>Color space conversion function to import RGB samples</summary>
		/// <param name="wpic">The WebPPicture structure</param>
		/// <param name="bgr">Point to BGR data</param>
		/// <param name="stride">stride of BGR data</param>
		/// <returns>Returns 0 in case of memory error.</returns>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureImportBGR")]
		internal static extern int WebPPictureImportBGR(ref WebPPicture wpic, IntPtr bgr, int stride);

		/// <summary>Color-space conversion function to import RGB samples</summary>
		/// <param name="wpic">The WebPPicture structure</param>
		/// <param name="bgra">Point to BGRA data</param>
		/// <param name="stride">stride of BGRA data</param>
		/// <returns>Returns 0 in case of memory error.</returns>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureImportBGRA")]
		internal static extern int WebPPictureImportBGRA(ref WebPPicture wpic, IntPtr bgra, int stride);

		/// <summary>Color-space conversion function to import RGB samples</summary>
		/// <param name="wpic">The WebPPicture structure</param>
		/// <param name="bgr">Point to BGR data</param>
		/// <param name="stride">stride of BGR data</param>
		/// <returns>Returns 0 in case of memory error.</returns>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureImportBGRX")]
		internal static extern int WebPPictureImportBGRX(ref WebPPicture wpic, IntPtr bgr, int stride);

		/// <summary>The writer type for output compress data</summary>
		/// <param name="data">Data returned</param>
		/// <param name="data_size">Size of data returned</param>
		/// <param name="wpic">Picture structure</param>
		/// <returns></returns>
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		internal delegate int WebPMemoryWrite([In] IntPtr data, UIntPtr data_size, ref WebPPicture wpic);

		/// <summary>Compress to WebP format</summary>
		/// <param name="config">The configuration structure for compression parameters</param>
		/// <param name="picture">'picture' hold the source samples in both YUV(A) or ARGB input</param>
		/// <returns>Returns 0 in case of error, 1 otherwise. In case of error, picture->error_code is updated accordingly.</returns>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncode")]
		internal static extern int WebPEncode(ref WebPConfig config, ref WebPPicture picture);

		/// <summary>Release the memory allocated by WebPPictureAlloc() or WebPPictureImport*()
		/// Note that this function does _not_ free the memory used by the 'picture' object itself.
		/// Besides memory (which is reclaimed) all other fields of 'picture' are preserved</summary>
		/// <param name="wpic">Picture structure</param>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureFree")]
		internal static extern void WebPPictureFree(ref WebPPicture wpic);

		/// <summary>Validate the WebP image header and retrieve the image height and width. Pointers *width and *height can be passed NULL if deemed irrelevant</summary>
		/// <param name="data">Pointer to WebP image data</param>
		/// <param name="data_size">This is the size of the memory block pointed to by data containing the image data</param>
		/// <param name="width">The range is limited currently from 1 to 16383</param>
		/// <param name="height">The range is limited currently from 1 to 16383</param>
		/// <returns>1 if success, otherwise error code returned in the case of (a) formatting error(s).</returns>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPGetInfo")]
		internal static extern int WebPGetInfo([In] IntPtr data, UIntPtr data_size, out int width, out int height);

		/// <summary>Decode WEBP image pointed to by *data and returns BGR samples into a preallocated buffer</summary>
		/// <param name="data">Pointer to WebP image data</param>
		/// <param name="data_size">This is the size of the memory block pointed to by data containing the image data</param>
		/// <param name="output_buffer">Pointer to decoded WebP image</param>
		/// <param name="output_buffer_size">Size of allocated buffer</param>
		/// <param name="output_stride">Specifies the distance between scan lines</param>
		internal static void WebPDecodeBGRInto(IntPtr data, int data_size, IntPtr output_buffer, int output_buffer_size, int output_stride)
		{
			if (WebPDecodeBGRInto(data, (UIntPtr)data_size, output_buffer, output_buffer_size, output_stride) == IntPtr.Zero)
				throw new InvalidOperationException("Can not decode WebP");
		}

		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecodeBGRInto")]
		private static extern IntPtr WebPDecodeBGRInto([In] IntPtr data, UIntPtr data_size, IntPtr output_buffer, int output_buffer_size, int output_stride);

		/// <summary>Decode WEBP image pointed to by *data and returns BGRA samples into a preallocated buffer</summary>
		/// <param name="data">Pointer to WebP image data</param>
		/// <param name="data_size">This is the size of the memory block pointed to by data containing the image data</param>
		/// <param name="output_buffer">Pointer to decoded WebP image</param>
		/// <param name="output_buffer_size">Size of allocated buffer</param>
		/// <param name="output_stride">Specifies the distance between scan lines</param>
		internal static void WebPDecodeBGRAInto(IntPtr data, int data_size, IntPtr output_buffer, int output_buffer_size, int output_stride)
		{
			if (WebPDecodeBGRAInto(data, (UIntPtr)data_size, output_buffer, output_buffer_size, output_stride) == IntPtr.Zero)
				throw new InvalidOperationException("Can not decode WebP");
		}

		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecodeBGRAInto")]
		private static extern IntPtr WebPDecodeBGRAInto([In] IntPtr data, UIntPtr data_size, IntPtr output_buffer, int output_buffer_size, int output_stride);


		/// <summary>Decode WEBP image pointed to by *data and returns ARGB samples into a preallocated buffer</summary>
		/// <param name="data">Pointer to WebP image data</param>
		/// <param name="data_size">This is the size of the memory block pointed to by data containing the image data</param>
		/// <param name="output_buffer">Pointer to decoded WebP image</param>
		/// <param name="output_buffer_size">Size of allocated buffer</param>
		/// <param name="output_stride">Specifies the distance between scan lines</param>
		internal static void WebPDecodeARGBInto(IntPtr data, int data_size, IntPtr output_buffer, int output_buffer_size, int output_stride)
		{
			if (WebPDecodeARGBInto(data, (UIntPtr)data_size, output_buffer, output_buffer_size, output_stride) == IntPtr.Zero)
				throw new InvalidOperationException("Can not decode WebP");
		}

		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecodeARGBInto")]
		private static extern IntPtr WebPDecodeARGBInto([In] IntPtr data, UIntPtr data_size, IntPtr output_buffer, int output_buffer_size, int output_stride);

		/// <summary>Initialize the configuration as empty. This function must always be called first, unless WebPGetFeatures() is to be called</summary>
		/// <param name="webPDecoderConfig">Configuration structure</param>
		/// <returns>False in case of mismatched version.</returns>
		internal static int WebPInitDecoderConfig(ref WebPDecoderConfig webPDecoderConfig)
		{
			return WebPInitDecoderConfigInternal(ref webPDecoderConfig, WEBP_DECODER_ABI_VERSION);
		}

		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPInitDecoderConfigInternal")]
		private static extern int WebPInitDecoderConfigInternal(ref WebPDecoderConfig webPDecoderConfig, int WEBP_DECODER_ABI_VERSION);

		/// <summary>Decodes the full data at once, taking configuration into account</summary>
		/// <param name="data">WebP raw data to decode</param>
		/// <param name="data_size">Size of WebP data </param>
		/// <param name="webPDecoderConfig">Configuration structure</param>
		/// <returns>VP8_STATUS_OK if the decoding was successful</returns>
		internal static VP8StatusCode WebPDecode(IntPtr data, int data_size, ref WebPDecoderConfig webPDecoderConfig)
		{
			return WebPDecode(data, (UIntPtr)data_size, ref webPDecoderConfig);
		}
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecode")]
		private static extern VP8StatusCode WebPDecode(IntPtr data, UIntPtr data_size, ref WebPDecoderConfig config);

		/// <summary>Free any memory associated with the buffer. Must always be called last. Doesn't free the 'buffer' structure itself</summary>
		/// <param name="buffer">WebPDecBuffer</param>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPFreeDecBuffer")]
		internal static extern void WebPFreeDecBuffer(ref WebPDecBuffer buffer);

		/// <summary>Lossy encoding images</summary>
		/// <param name="bgr">Pointer to BGR image data</param>
		/// <param name="width">The range is limited currently from 1 to 16383</param>
		/// <param name="height">The range is limited currently from 1 to 16383</param>
		/// <param name="stride">Specifies the distance between scanlines</param>
		/// <param name="quality_factor">Ranges from 0 (lower quality) to 100 (highest quality). Controls the loss and quality during compression</param>
		/// <param name="output">output_buffer with WebP image</param>
		/// <returns>Size of WebP Image or 0 if an error occurred</returns>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeBGR")]
		internal static extern int WebPEncodeBGR([In] IntPtr bgr, int width, int height, int stride, float quality_factor, out IntPtr output);

		/// <summary>Lossy encoding images</summary>
		/// <param name="bgra">Pointer to BGRA image data</param>
		/// <param name="width">The range is limited currently from 1 to 16383</param>
		/// <param name="height">The range is limited currently from 1 to 16383</param>
		/// <param name="stride">Specifies the distance between scan lines</param>
		/// <param name="quality_factor">Ranges from 0 (lower quality) to 100 (highest quality). Controls the loss and quality during compression</param>
		/// <param name="output">output_buffer with WebP image</param>
		/// <returns>Size of WebP Image or 0 if an error occurred</returns>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeBGRA")]
		internal static extern int WebPEncodeBGRA([In] IntPtr bgra, int width, int height, int stride, float quality_factor, out IntPtr output);

		/// <summary>Lossless encoding images pointed to by *data in WebP format</summary>
		/// <param name="bgr">Pointer to BGR image data</param>
		/// <param name="width">The range is limited currently from 1 to 16383</param>
		/// <param name="height">The range is limited currently from 1 to 16383</param>
		/// <param name="stride">Specifies the distance between scan lines</param>
		/// <param name="output">output_buffer with WebP image</param>
		/// <returns>Size of WebP Image or 0 if an error occurred</returns>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeLosslessBGR")]
		internal static extern int WebPEncodeLosslessBGR([In] IntPtr bgr, int width, int height, int stride, out IntPtr output);

		/// <summary>Lossless encoding images pointed to by *data in WebP format</summary>
		/// <param name="bgra">Pointer to BGRA image data</param>
		/// <param name="width">The range is limited currently from 1 to 16383</param>
		/// <param name="height">The range is limited currently from 1 to 16383</param>
		/// <param name="stride">Specifies the distance between scan lines</param>
		/// <param name="output">output_buffer with WebP image</param>
		/// <returns>Size of WebP Image or 0 if an error occurred</returns>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeLosslessBGRA")]
		internal static extern int WebPEncodeLosslessBGRA([In] IntPtr bgra, int width, int height, int stride, out IntPtr output);

		/// <summary>Releases memory returned by the WebPEncode</summary>
		/// <param name="p">Pointer to memory</param>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPFree")]
		internal static extern void WebPFree(IntPtr p);

		/// <summary>Get the WebP version library</summary>
		/// <returns>8bits for each of major/minor/revision packet in integer. E.g: v2.5.7 is 0x020507</returns>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPGetDecoderVersion")]
		internal static extern int WebPGetDecoderVersion();

		/// <summary>Compute PSNR, SSIM or LSIM distortion metric between two pictures</summary>
		/// <param name="srcPicture">Picture to measure</param>
		/// <param name="refPicture">Reference picture</param>
		/// <param name="metric_type">0 = PSNR, 1 = SSIM, 2 = LSIM</param>
		/// <param name="pResult">dB in the Y/U/V/Alpha/All order</param>
		/// <returns>False in case of error (the two pictures don't have same dimension, ...)</returns>
		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPPictureDistortion")]
		internal static extern int WebPPictureDistortion(ref WebPPicture srcPicture, ref WebPPicture refPicture, int metric_type, IntPtr pResult);

		[DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPMalloc")]
		internal static extern IntPtr WebPMalloc(int size);
		#endregion

		#region | Import libwebpdemux functions |
#if NET46 || NETCOREAPP
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		private static void ValidatePlatform()
		{
			if (IntPtr.Size != 4 && IntPtr.Size != 8)
				throw new InvalidOperationException("Invalid platform. Can not find proper function");
		}

		/*
        * from WebPAnimDecoder API
        */
		private static readonly int WEBP_DEMUX_ABI_VERSION = 0x0107;

		/// <summary>Should always be called, to initialize a fresh WebPAnimDecoderOptions
		/// structure before modification. Returns false in case of version mismatch.
		/// WebPAnimDecoderOptionsInit() must have succeeded before using the
		/// 'dec_options' object.</summary>
		/// <param name="dec_options">(in/out) options used for decoding animation</param>
		/// <returns>true/false - success/error</returns>
		internal static bool WebPAnimDecoderOptionsInit(ref WebPAnimDecoderOptions dec_options)
		{
			ValidatePlatform();

			return WebPAnimDecoderOptionsInitInternal(ref dec_options, WEBP_DEMUX_ABI_VERSION) == 1;
		}
		[DllImport("libwebpdemux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPAnimDecoderOptionsInitInternal")]
		private static extern int WebPAnimDecoderOptionsInitInternal(ref WebPAnimDecoderOptions dec_options, int WEBP_DEMUX_ABI_VERSION);


		/// <summary>
		/// Creates and initializes a WebPAnimDecoder object.
		/// </summary>
		/// <param name="webp_data">(in) WebP bitstream. This should remain unchanged during the 
		///     lifetime of the output WebPAnimDecoder object.</param>
		/// <param name="dec_options">(in) decoding options. Can be passed NULL to choose 
		///     reasonable defaults (in particular, color mode MODE_RGBA 
		///     will be picked).</param>
		/// <returns>A pointer to the newly created WebPAnimDecoder object, or NULL in case of
		///     parsing error, invalid option or memory error.</returns>
		internal static WebPAnimDecoder WebPAnimDecoderNew(ref WebPData webp_data, ref WebPAnimDecoderOptions dec_options)
		{
			//ValidatePlatform();

			IntPtr ptr = WebPAnimDecoderNewInternal(ref webp_data, ref dec_options, WEBP_DEMUX_ABI_VERSION);
			WebPAnimDecoder decoder = new WebPAnimDecoder() { decoder = ptr };
			return decoder;

		}
		[DllImport("libwebpdemux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPAnimDecoderNewInternal")]
		private static extern IntPtr WebPAnimDecoderNewInternal(ref WebPData webp_data, ref WebPAnimDecoderOptions dec_options, int WEBP_DEMUX_ABI_VERSION);


		/// <summary>Get global information about the animation.</summary>
		/// <param name="dec">(in) decoder instance to get information from.</param>
		/// <param name="info">(out) global information fetched from the animation.</param>
		/// <returns>True on success.</returns>
		internal static bool WebPAnimDecoderGetInfo(IntPtr dec, out WebPAnimInfo info)
		{
			//ValidatePlatform();

			return WebPAnimDecoderGetInfoInternal(dec, out info) == 1;
		}
		[DllImport("libwebpdemux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPAnimDecoderGetInfo")]
		private static extern int WebPAnimDecoderGetInfoInternal(IntPtr dec, out WebPAnimInfo info);


		/// <summary>Check if there are more frames left to decode.</summary>
		/// <param name="dec">(in) decoder instance to be checked.</param>
		/// <returns>
		/// True if 'dec' is not NULL and some frames are yet to be decoded.
		/// Otherwise, returns false.
		/// </returns>
		internal static bool WebPAnimDecoderHasMoreFrames(IntPtr dec)
		{
			//ValidatePlatform();

			return WebPAnimDecoderHasMoreFramesInternal(dec) == 1;
		}
		[DllImport("libwebpdemux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPAnimDecoderHasMoreFrames")]
		private static extern int WebPAnimDecoderHasMoreFramesInternal(IntPtr dec);


		/// <summary>
		/// Fetch the next frame from 'dec' based on options supplied to
		/// WebPAnimDecoderNew(). This will be a fully reconstructed canvas of size
		/// 'canvas_width * 4 * canvas_height', and not just the frame sub-rectangle. The
		/// returned buffer 'buf' is valid only until the next call to
		/// WebPAnimDecoderGetNext(), WebPAnimDecoderReset() or WebPAnimDecoderDelete().
		/// </summary>
		/// <param name="dec">(in/out) decoder instance from which the next frame is to be fetched.</param>
		/// <param name="buf">(out) decoded frame.</param>
		/// <param name="timestamp">(out) timestamp of the frame in milliseconds.</param>
		/// <returns>
		/// False if any of the arguments are NULL, or if there is a parsing or
		/// decoding error, or if there are no more frames. Otherwise, returns true.
		/// </returns>
		internal static bool WebPAnimDecoderGetNext(IntPtr dec, ref IntPtr buf, ref int timestamp)
		{
			//ValidatePlatform();

			return WebPAnimDecoderGetNextInternal(dec, ref buf, ref timestamp) == 1;
		}
		[DllImport("libwebpdemux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPAnimDecoderGetNext")]
		private static extern int WebPAnimDecoderGetNextInternal(IntPtr dec, ref IntPtr buf, ref int timestamp);


		/// <summary>
		/// Resets the WebPAnimDecoder object, so that next call to
		/// WebPAnimDecoderGetNext() will restart decoding from 1st frame. This would be
		/// helpful when all frames need to be decoded multiple times (e.g.
		/// info.loop_count times) without destroying and recreating the 'dec' object.
		/// </summary>
		/// <param name="dec">(in/out) decoder instance to be reset</param>
		internal static void WebPAnimDecoderReset(IntPtr dec)
		{
			//ValidatePlatform();

			WebPAnimDecoderResetInternal(dec);
		}
		[DllImport("libwebpdemux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPAnimDecoderReset")]
		private static extern void WebPAnimDecoderResetInternal(IntPtr dec);


		/// <summary>Deletes the WebPAnimDecoder object.</summary>
		/// <param name="decoder">(in/out) decoder instance to be deleted</param>
		internal static void WebPAnimDecoderDelete(IntPtr decoder)
		{
			//ValidatePlatform();

			WebPAnimDecoderDeleteInternal(decoder);
		}
		[DllImport("libwebpdemux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPAnimDecoderDelete")]
		private static extern void WebPAnimDecoderDeleteInternal(IntPtr dec);

		/// <summary>
		/// Grab the internal demuxer object.
		/// Getting the demuxer object can be useful if one wants to use operations only
		/// available through demuxer; e.g. to get XMP/EXIF/ICC metadata. The returned
		/// demuxer object is owned by 'dec' and is valid only until the next call to
		/// WebPAnimDecoderDelete().
		/// </summary>
		/// <param name="dec">(in) decoder instance from which the demuxer object is to be fetched</param>
		/// <returns></returns>
		internal static WebPDemuxer WebPAnimDecoderGetDemuxer(WebPAnimDecoder dec)
		{
			//ValidatePlatform();

			IntPtr ptr = WebPAnimDecoderGetDemuxerInternal(dec.decoder);
			WebPDemuxer demuxer = new WebPDemuxer() { demuxer = ptr };
			return demuxer;
		}
		[DllImport("libwebpdemux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPAnimDecoderGetDemuxer")]
		private static extern IntPtr WebPAnimDecoderGetDemuxerInternal(IntPtr dec);

		/// <summary>
		/// Retrieves frame 'frame_number' from 'dmux'.
		/// 'iter->fragment' points to the frame on return from this function.
		/// Setting 'frame_number' equal to 0 will return the last frame of the image.
		/// Returns false if 'dmux' is NULL or frame 'frame_number' is not present.
		/// Call WebPDemuxReleaseIterator() when use of the iterator is complete.
		/// NOTE: 'dmux' must persist for the lifetime of 'iter'.
		/// </summary>
		/// <param name="decoder"></param>
		/// <param name="frame"></param>
		/// <param name="iter"></param>
		/// <returns>true/false - success/error</returns>
		internal static bool WebPDemuxGetFrame(WebPDemuxer dmux, int frameNumber, out WebPIterator iter)
		{
			return WebPDemuxGetFrameInternal(dmux.demuxer, frameNumber, out iter) == 1;
		}
		[DllImport("libwebpdemux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDemuxGetFrame")]
		private static extern int WebPDemuxGetFrameInternal(IntPtr dmux, int frameNumber, out WebPIterator iter);

		/// <summary>
		/// Releases any memory associated with 'iter'.
		/// Must be called before any subsequent calls to WebPDemuxGetChunk() on the same
		/// iter. Also, must be called before destroying the associated WebPDemuxer with
		/// WebPDemuxDelete().
		/// </summary>
		/// <param name="iter">iterator to release</param>
		internal static void WebPDemuxReleaseIterator(WebPIterator iter)
		{
			WebPDemuxReleaseIteratorInternal(iter);
		}
		[DllImport("libwebpdemux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDemuxReleaseIterator")]
		private static extern int WebPDemuxReleaseIteratorInternal(WebPIterator iter);

		[DllImport("libwebpmux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPNewInternal")]
		internal static extern IntPtr WebPNewInternal(int version);

		[DllImport("libwebpmux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPMuxSetImage")]
		internal static extern WebPMuxError WebPMuxSetImage(IntPtr mux, ref WebPData bitstream, int copy_data);

		[DllImport("libwebpmux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPMuxSetChunk")]
		internal static extern WebPMuxError WebPMuxSetChunk(IntPtr mux, string fourcc, ref WebPData chunk_data, int copy_data);

		[DllImport("libwebpmux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPMuxAssemble")]
		internal static extern WebPMuxError WebPMuxAssemble(IntPtr mux, ref WebPData output_data);

		[DllImport("libwebpmux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPMuxDelete")]
		internal static extern void WebPMuxDelete(IntPtr mux);

		// TODO: this was custom patch in libwebp but not necessary if Marshal.FreeHGlobal works too
		[DllImport("libwebpmux.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDataClearExternal")]
		internal static extern void WebPDataClear(ref WebPData mux);
		#endregion
	}
}