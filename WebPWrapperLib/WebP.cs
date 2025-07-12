/////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Wrapper for WebP format in C#. (MIT) Jose M. Piñeiro and others
/////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Decode Functions:
// Bitmap Load(string pathFileName) - Load a WebP file in bitmap.
// Bitmap Decode(byte[] rawWebP) - Decode WebP data (rawWebP) to bitmap.
// Bitmap Decode(byte[] rawWebP, WebPDecoderOptions options) - Decode WebP data (rawWebP) to bitmap using 'options'.
// Bitmap GetThumbnailFast(byte[] rawWebP, short width, short height) - Get a thumbnail from WebP data (rawWebP) with dimensions 'width x height'. Fast mode.
// Bitmap GetThumbnailQuality(byte[] rawWebP, short width, short height) - Fast get a thumbnail from WebP data (rawWebP) with dimensions 'width x height'. Quality mode.
//
// Encode Functions:
// Save(Bitmap pixelMap, string pathFileName, byte quality) - Save bitmap with quality lost to WebP file. Optionally select 'quality'.
// byte[] EncodeLossy(Bitmap pixelMap, byte quality) - Encode bitmap with quality lost to WebP byte array. Optionally select 'quality'.
// byte[] EncodeLossy(Bitmap pixelMap, byte quality, byte speed, bool info) - Encode bitmap with quality lost to WebP byte array. Select 'quality', 'speed' and optionally select 'info'.
// byte[] EncodeLossless(Bitmap pixelMap) - Encode bitmap without quality lost to WebP byte array.
// byte[] EncodeLossless(Bitmap pixelMap, byte speed, bool info = false) - Encode bitmap without quality lost to WebP byte array. Select 'speed'.
// byte[] EncodeNearLossless(Bitmap pixelMap, byte quality, byte speed = 9, bool info = false) - Encode bitmap with a near lossless method to WebP byte array. Select 'quality', 'speed' and optionally select 'info'.
//
// Another functions:
// Version GetVersion() - Get the library version
// WebPInfo GetInfo(byte[] rawWebP) - Get information of WEBP data
// float[] GetPictureDistortion(Bitmap source, Bitmap reference, DistortionMetric metricType) - Get PSNR, SSIM or LSIM distortion metric between two pictures
////////////////////////////////////////////////////////////////////////////////////////////////////////////
// TODO : throw more specific exceptions according to context (throw new Exception("Too short message"); is not informative)
// TODO : make NuGet package
// NOTE : For non-Windows targets, use SkiaSharp as an alternative to GDI+ which has built-in WebP support.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace WebPWrapper
{
	public sealed class WebP : IDisposable
	{
		private UnsafeNativeMethods.WebPMemoryWrite _myWriterDelegate;

		#region | Public Decode Functions |
		/// <summary>Read a WebP file</summary>
		/// <param name="pathFileName">WebP file to load</param>
		/// <returns>Bitmap with the WebP image</returns>
		public Bitmap Load(string pathFileName)
		{
			return Decode(File.ReadAllBytes(pathFileName));
		}

		/// <summary>Decode a WebP image</summary>
		/// <param name="rawWebP">The data to uncompress</param>
		/// <returns>Bitmap with the WebP image</returns>
		public Bitmap Decode(byte[] rawWebP)
		{
			Bitmap pixelMap = null;
			BitmapData bmpData = null;
			var pinnedWebP = GCHandle.Alloc(rawWebP, GCHandleType.Pinned);

			try {
				//Get image width and height
				var info = GetInfo(rawWebP);

				//Create a BitmapData and Lock all pixels to be written
				pixelMap = new Bitmap(info.Width, info.Height, info.HasAlpha ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb);

				bmpData = LockAllBits(pixelMap, ImageLockMode.WriteOnly);

				//Uncompress the image
				int outputSize = bmpData.Stride * info.Height;
				IntPtr ptrData = pinnedWebP.AddrOfPinnedObject();
				if (pixelMap.PixelFormat == PixelFormat.Format24bppRgb)
					UnsafeNativeMethods.WebPDecodeBGRInto(ptrData, rawWebP.Length, bmpData.Scan0, outputSize, bmpData.Stride);
				else
					UnsafeNativeMethods.WebPDecodeBGRAInto(ptrData, rawWebP.Length, bmpData.Scan0, outputSize, bmpData.Stride);

				return pixelMap;
			} finally {
				UnlockPin(pixelMap, bmpData, pinnedWebP);
			}
		}

		/// <summary>Decode a WebP image</summary>
		/// <param name="rawWebP">the data to uncompress</param>
		/// <param name="options">Options for advanced decode</param>
		/// <returns>Bitmap with the WebP image</returns>
		public Bitmap Decode(byte[] rawWebP, WebPDecoderOptions options)
		{
			GCHandle pinnedWebP = GCHandle.Alloc(rawWebP, GCHandleType.Pinned);
			Bitmap pixelMap = null;
			BitmapData bmpData = null;
			VP8StatusCode result;
			try {
				WebPDecoderConfig config = new WebPDecoderConfig();
				if (UnsafeNativeMethods.WebPInitDecoderConfig(ref config) == 0) {
					throw new Exception("WebPInitDecoderConfig failed. Wrong version?");
				}
				// Read the .webp input file information
				IntPtr ptrRawWebP = pinnedWebP.AddrOfPinnedObject();
#if DEBUG
				int height;
				int width;
#endif
				if (options.use_scaling == 0) {
					result = UnsafeNativeMethods.WebPGetFeatures(ptrRawWebP, rawWebP.Length, ref config.input);
					if (result != VP8StatusCode.VP8_STATUS_OK)
						throw new Exception("Failed WebPGetFeatures with error " + result);

					//Test cropping values
					if (options.use_cropping == 1) {
						if (options.crop_left + options.crop_width > config.input.Width ||
							options.crop_top + options.crop_height > config.input.Height) {
							throw new Exception("Crop options exceeded WebP image dimensions");
						}
#if DEBUG
						width = options.crop_width;
						height = options.crop_height;
#endif
					}
				}
#if DEBUG
				else {
					width = options.scaled_width;
					height = options.scaled_height;
				}
#endif

				// TODO : could we just copy over the full structure?
				config.options.bypass_filtering         = options.bypass_filtering;
				config.options.no_fancy_upsampling      = options.no_fancy_upsampling;
				config.options.use_cropping             = options.use_cropping;
				config.options.crop_left                = options.crop_left;
				config.options.crop_top                 = options.crop_top;
				config.options.crop_width               = options.crop_width;
				config.options.crop_height              = options.crop_height;
				config.options.use_scaling              = options.use_scaling;
				config.options.scaled_width             = options.scaled_width;
				config.options.scaled_height            = options.scaled_height;
				config.options.use_threads              = options.use_threads;
				config.options.dithering_strength       = options.dithering_strength;
				config.options.flip                     = options.flip;
				config.options.alpha_dithering_strength = options.alpha_dithering_strength;

				pixelMap = CoreDecode(rawWebP, config, ptrRawWebP, out bmpData);
				return pixelMap;
			} finally {
				UnlockPin(pixelMap, bmpData, pinnedWebP);
			}
		}

		/// <summary>Get Thumbnail from webP in mode faster/low quality</summary>
		/// <param name="rawWebP">The data to uncompress</param>
		/// <param name="width">Wanted width of thumbnail</param>
		/// <param name="height">Wanted height of thumbnail</param>
		/// <returns>Bitmap with the WebP thumbnail in 24bpp</returns>
		public Bitmap GetThumbnailFast(byte[] rawWebP, short width, short height)
		{
			return GetThumbnail(rawWebP, width, height, false);
		}

		/// <summary>Thumbnail from webP in mode slow/high quality</summary>
		/// <param name="rawWebP">The data to uncompress</param>
		/// <param name="width">Wanted width of thumbnail</param>
		/// <param name="height">Wanted height of thumbnail</param>
		/// <returns>Bitmap with the WebP thumbnail</returns>
		public Bitmap GetThumbnailQuality(byte[] rawWebP, short width, short height)
		{
			return GetThumbnail(rawWebP, width, height, true);
		}
		#endregion

		#region | Public Encode Functions |
		/// <summary>Save bitmap to file in WebP format</summary>
		/// <param name="pixelMap">Bitmap with the WebP image</param>
		/// <param name="pathFileName">The file to write</param>
		/// <param name="quality">Between 0 (lower quality, lowest file size) and 100 (highest quality, higher file size)</param>
		public void Save(Bitmap pixelMap, string pathFileName, byte quality = 75)
		{
			//Encode in webP format and Write webP file
			File.WriteAllBytes(pathFileName, EncodeLossy(pixelMap, quality));
		}

		/// <summary>Lossy encoding bitmap to WebP (Simple encoding API)</summary>
		/// <param name="pixelMap">Bitmap with the image</param>
		/// <param name="quality">Between 0 (lower quality, lowest file size) and 100 (highest quality, higher file size)</param>
		/// <returns>Compressed data</returns>
		public byte[] EncodeLossy(Bitmap pixelMap, byte quality = 75)
		{
			return CoreEncode(pixelMap, quality);
		}

		/// <summary>Lossy encoding bitmap to WebP (Advanced encoding API)</summary>
		/// <param name="pixelMap">Bitmap with the image</param>
		/// <param name="quality">Between 0 (lower quality, lowest file size) and 100 (highest quality, higher file size)</param>
		/// <param name="speed">Between 0 (fastest, lowest compression) and 9 (slower, best compression)</param>
		/// <returns>Compressed data</returns>
		public byte[] EncodeLossy(Bitmap pixelMap, byte quality, byte speed, bool info = false)
		{
			return this.AdvancedEncode(pixelMap,
				InitEncodeConfig(EncodingMode.Lossy, quality, speed), info);
		}

		/// <summary>Lossless encoding bitmap to WebP (Simple encoding API)</summary>
		/// <param name="pixelMap">Bitmap with the image</param>
		/// <returns>Compressed data</returns>
		public byte[] EncodeLossless(Bitmap pixelMap)
		{
			return CoreEncode(pixelMap, null);
		}

		/// <summary>Lossless encoding image in bitmap (Advanced encoding API)</summary>
		/// <param name="pixelMap">Bitmap with the image</param>
		/// <param name="speed">Between 0 (fastest, lowest compression) and 9 (slower, best compression)</param>
		/// <returns>Compressed data</returns>
		public byte[] EncodeLossless(Bitmap pixelMap, byte speed)
		{
			return this.AdvancedEncode(pixelMap,
				InitEncodeConfig(EncodingMode.Lossless, (byte)((speed + 1) * 10), speed), false);
		}

		/// <summary>Near lossless encoding image in bitmap</summary>
		/// <param name="pixelMap">Bitmap with the image</param>
		/// <param name="quality">Between 0 (lower quality, lowest file size) and 100 (highest quality, higher file size)</param>
		/// <param name="speed">Between 0 (fastest, lowest compression) and 9 (slower, best compression)</param>
		/// <returns>Compress data</returns>
		public byte[] EncodeNearLossless(Bitmap pixelMap, byte quality, byte speed = 9)
		{
			return this.AdvancedEncode(pixelMap,
				InitEncodeConfig(EncodingMode.NearLossless, quality, speed), false);
		}

		public void EncodeWithMeta(Bitmap pixelMap, string path, byte[] rawXmp,
		byte quality = 85, byte speed = 4, bool multithread = false, byte alphaQuality = 100)
		{
			IntPtr mux    = UnsafeNativeMethods.WebPNewInternal(0x0108); // TODO: hardcoded libwebp ABI version
			var    config = InitEncodeConfig(quality);
			config.method        = Math.Min(speed, (byte)6); // 0 is fastest
			config.thread_level  = multithread ? 1 : 0;
			config.alpha_quality = alphaQuality;

			var rawWebP = AdvancedEncode(pixelMap, config, false);
			WebPMuxError err;

			// TODO: directly use Ptr from AdvancedEncode instead of managed<>unmanaged back and forth
			var pinnedRawWebP = GCHandle.Alloc(rawWebP, GCHandleType.Pinned);
			IntPtr webpPtr = pinnedRawWebP.AddrOfPinnedObject();
			var webpData = new WebPData()
			{
				size = Convert.ToUInt64(rawWebP.Length),
				data = webpPtr
			};
			err = UnsafeNativeMethods.WebPMuxSetImage(mux, ref webpData, 0);
			if (err != WebPMuxError.WEBP_MUX_OK) throw new Exception("Error: " + err);

			var pinnedRawMeta = GCHandle.Alloc(rawXmp, GCHandleType.Pinned);
			IntPtr metaPtr = pinnedRawMeta.AddrOfPinnedObject();
			var metaWebData = new WebPData()
			{
				size = Convert.ToUInt64(rawXmp.Length),
				data = metaPtr
			};
			err = UnsafeNativeMethods.WebPMuxSetChunk(mux, "XMP ", ref metaWebData, 0);
			if (err != WebPMuxError.WEBP_MUX_OK) throw new Exception("Error: " + err);

			var outputData = new WebPData();
			err = UnsafeNativeMethods.WebPMuxAssemble(mux, ref outputData);
			if (err != WebPMuxError.WEBP_MUX_OK) throw new Exception("Error: " + err);

			int size = Convert.ToInt32(outputData.size);
			var rawOutput = new byte[size];
			Marshal.Copy(outputData.data, rawOutput, 0, size);
			File.WriteAllBytes(path, rawOutput);

			UnsafeNativeMethods.WebPMuxDelete(mux);
			//UnsafeNativeMethods.WebPDataClear(ref outputData);
			Marshal.FreeHGlobal(outputData.data);

			pinnedRawWebP.Free();
			pinnedRawMeta.Free();
		}
		#endregion

		#region | Public AnimDecoder Functions |

		/// <summary>
		/// Holds information about one frame.
		/// </summary>
		/// <remarks>
		/// AnimLoad() / AnimDecode() return a list of FrameData objects.
		/// </remarks>
		public class FrameData
		{
			public Bitmap Bitmap { get; set; }

			public int Duration { get; set; }
		}

		/// <summary>Read and Decode an Animated WebP file</summary>
		/// <param name="pathFileName">Animated WebP file to load</param>
		/// <returns>Bitmaps of the Animated WebP frames</returns>
		public IEnumerable<FrameData> AnimLoad(string pathFileName)
		{
			return AnimDecode(File.ReadAllBytes(pathFileName));
		}

		/// <summary>Decode an Animated WebP image</summary>
		/// <param name="rawWebP">The data to uncompress</param>
		/// <param name="startFrameIdx">OPTIONAL start index (including) for first frame that should be returned</param>
		/// <param name="endFrameIdx">OPTIONAL end index (excluding) for last frame up until that, frames should be returned</param>
		/// <returns>List of FrameData - each containing frame bitmap and duration</returns>
		public IEnumerable<FrameData> AnimDecode(byte[] rawWebP, int startFrameIdx = -1, int endFrameIdx = -1)
		{
			GCHandle pinnedWebP = GCHandle.Alloc(rawWebP, GCHandleType.Pinned);

			Bitmap bitmap = null;
			BitmapData bmpData = null;
			try {
				WebPAnimDecoderOptions dec_options = new WebPAnimDecoderOptions();
				var result = UnsafeNativeMethods.WebPAnimDecoderOptionsInit(ref dec_options);
				dec_options.color_mode = WEBP_CSP_MODE.MODE_BGRA;
				WebPData webp_data = new WebPData
				{
					data = pinnedWebP.AddrOfPinnedObject(),
					size = (ulong)rawWebP.Length
				};
				WebPAnimDecoder dec = UnsafeNativeMethods.WebPAnimDecoderNew(ref webp_data, ref dec_options);
				WebPAnimInfo anim_info = new WebPAnimInfo();
				UnsafeNativeMethods.WebPAnimDecoderGetInfo(dec.decoder, out anim_info);

				Rectangle rect = new Rectangle(0, 0, (int)anim_info.canvas_width, (int)anim_info.canvas_height);

				List<FrameData> frames = new List<FrameData>();
				int oldTimestamp = 0;
				int idx = 0;
				while (UnsafeNativeMethods.WebPAnimDecoderHasMoreFrames(dec.decoder)) {
					IntPtr buf = IntPtr.Zero;
					int timestamp = 0;
					var result2 = UnsafeNativeMethods.WebPAnimDecoderGetNext(dec.decoder, ref buf, ref timestamp);

					if (startFrameIdx == -1 || startFrameIdx <= idx) {

						bitmap = new Bitmap((int)anim_info.canvas_width, (int)anim_info.canvas_height, PixelFormat.Format32bppArgb);
						bmpData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);
						IntPtr startAddress = bmpData.Scan0;
						int pixels = Math.Abs(bmpData.Stride) * bitmap.Height;
						UnsafeNativeMethods.CopyMemory(startAddress, buf, (uint)pixels);
						bitmap.UnlockBits(bmpData);
						bmpData = null;

						frames.Add(new FrameData() { Bitmap = bitmap, Duration = timestamp - oldTimestamp });
					}

					oldTimestamp = timestamp;
					++idx;

					if (endFrameIdx != -1 && idx >= endFrameIdx) {
						break;
					}
				}

				UnsafeNativeMethods.WebPAnimDecoderDelete(dec.decoder);

				return frames;
			} finally {
				UnlockPin(bitmap, bmpData, pinnedWebP);
			}
		}

		/// <summary>
		/// Holds information about one frame in compressed form.
		/// </summary>
		/// <remarks>
		/// AnimGetFrame() returns a FrameDataRaw object.
		/// </remarks>
		public class FrameDataRaw
		{
			public byte[] Data { get; set; }
			public int Size { get; set; }
			public int Duration { get; set; }
		}

		/// <summary>Initialize the library for handling the given WebP file.</summary>
		/// <param name="pathFileName">Animated WebP file to load</param>
		/// <param name="frameCount">Number of frames in the animated WebP</param>
		/// <returns>true on success</returns>
		public bool AnimInit(string pathFileName, out uint frameCount)
		{
			return AnimInit(File.ReadAllBytes(pathFileName), out frameCount);
		}

		/// <summary>Initialize the library for handling the given WebP file.</summary>
		/// <param name="rawWebP">Byte array of an animated WebP</param>
		/// <param name="frameCount">Number of frames in the animated WebP</param>
		/// <returns>true on success</returns>
		public bool AnimInit(byte[] rawWebP, out uint frameCount)
		{
			DisposeOldDecoder();

			_pinnedWebP = GCHandle.Alloc(rawWebP, GCHandleType.Pinned);

			WebPAnimDecoderOptions dec_options = new WebPAnimDecoderOptions();
			var result = UnsafeNativeMethods.WebPAnimDecoderOptionsInit(ref dec_options);
			dec_options.color_mode = WEBP_CSP_MODE.MODE_BGRA;
			WebPData webp_data = new WebPData
			{
				data = _pinnedWebP.AddrOfPinnedObject(),
				size = (ulong)rawWebP.Length
			};
			_webPAnimDecoder = UnsafeNativeMethods.WebPAnimDecoderNew(ref webp_data, ref dec_options);

			WebPAnimInfo anim_info;
			UnsafeNativeMethods.WebPAnimDecoderGetInfo(_webPAnimDecoder.decoder, out anim_info);
			_frameCount = frameCount = anim_info.frame_count;

			return true;
		}

		/// <summary>Gets the raw frame data.</summary>
		/// <param name="frameNumber"></param>
		/// <returns>object with the frame's raw data</returns>
		public FrameDataRaw AnimGetFrame(int frameNumber)
		{
			if (_webPAnimDecoder.decoder == IntPtr.Zero)
				throw new ApplicationException("Decoder has not been initialized.");

			if (frameNumber < 1 || frameNumber > _frameCount)
				throw new ArgumentOutOfRangeException();

			WebPDemuxer webPDemuxer = UnsafeNativeMethods.WebPAnimDecoderGetDemuxer(_webPAnimDecoder);
			WebPIterator iter;
			bool res = UnsafeNativeMethods.WebPDemuxGetFrame(webPDemuxer, frameNumber, out iter);

			int size = (int)iter.fragment.size;
			byte[] bytes = new byte[size];
			Marshal.Copy(iter.fragment.data, bytes, 0, size);

			FrameDataRaw fd = new FrameDataRaw() { Data = bytes, Duration = iter.duration };

			UnsafeNativeMethods.WebPDemuxReleaseIterator(iter);

			return fd;
		}

		#endregion

		#region | Another Public Functions |
		/// <summary>Get the libwebp version</summary>
		/// <returns>Version of library</returns>
		public Version GetVersion()
		{
			var v = UnsafeNativeMethods.WebPGetDecoderVersion();
			return new Version((v >> 16) % 256, (v >> 8) % 256, v % 256);
		}

		/// <summary>Get info of WEBP data</summary>
		/// <param name="rawWebP">The data of WebP</param>
		public WebPInfo GetInfo(byte[] rawWebP)
		{
			var features = new WebPBitstreamFeatures();
			var pinnedWebP = GCHandle.Alloc(rawWebP, GCHandleType.Pinned);

			try {
				var result = UnsafeNativeMethods.WebPGetFeatures(pinnedWebP.AddrOfPinnedObject(), rawWebP.Length, ref features);

				if (result != 0) {
					throw new ExternalException("Unable to get features of WebP image. Status is " + result, (int)result);
				}
				var info = new WebPInfo();
				info.Width = (short)features.Width;
				info.Height = (short)features.Height;
				info.HasAlpha = features.Has_alpha == 1;
				info.IsAnimated = features.Has_animation == 1;
				var fmt = features.Format;
				if (fmt == 1) {
					info.Format = "lossy";
				} else if (fmt == 2) {
					info.Format = "lossless";
				} else {
					info.Format = "undefined";
				}

				return info;
			} finally {
				Unpin(pinnedWebP);
			}
		}

		/// <summary>Compute PSNR, SSIM or LSIM distortion metric between two pictures. Warning: this function is rather CPU-intensive</summary>
		/// <param name="source">Picture to measure</param>
		/// <param name="reference">Reference picture</param>
		/// <param name="metricType">0 = PSNR, 1 = SSIM, 2 = LSIM</param>
		/// <returns>dB in the Y/U/V/Alpha/All order</returns>
		public float[] GetPictureDistortion(Bitmap source, Bitmap reference, DistortionMetric metricType)
		{
			WebPPicture wpicSource = new WebPPicture();
			WebPPicture wpicReference = new WebPPicture();
			BitmapData sourceBmpData = null;
			BitmapData referenceBmpData = null;
			float[] result = new float[5];
			GCHandle pinnedResult = GCHandle.Alloc(result, GCHandleType.Pinned);

			try {
				if (source == null) {
					throw new ArgumentNullException("source", "Source picture is void");
				}
				if (reference == null) {
					throw new ArgumentNullException("reference", "Reference picture is void");
				}
				if (metricType > DistortionMetric.LightweightSimilarity) {
					throw new InvalidEnumArgumentException("metricType", (int)metricType, typeof(DistortionMetric));
				}
				if (source.Width != reference.Width || source.Height != reference.Height) {
					throw new ArgumentException("Source and Reference pictures have different dimensions");
				}

				ImportColorData(source, false, out sourceBmpData, ref wpicSource);

				ImportColorData(reference, false, out referenceBmpData, ref wpicReference);

				//Measure
				IntPtr ptrResult = pinnedResult.AddrOfPinnedObject();
				if (UnsafeNativeMethods.WebPPictureDistortion(ref wpicSource, ref wpicReference, (int)metricType, ptrResult) != 1)
					throw new Exception("Can´t measure.");
				return result;
			} finally {
				UnlockFree(source, sourceBmpData, wpicSource);
				UnlockFree(reference, referenceBmpData, wpicReference);
				Unpin(pinnedResult);
			}
		}
		#endregion

		#region | Private Methods |
		/// <summary>Encoding image  using Advanced encoding API</summary>
		/// <param name="pixelMap">Bitmap with the image</param>
		/// <param name="config">Configuration for encode</param>
		/// <param name="info">True if need encode info.</param>
		/// <returns>Compressed data</returns>
#if UNSAFE
		unsafe
#endif
		private byte[] AdvancedEncode(Bitmap pixelMap, WebPConfig config, bool info)
		{
#if UNSAFE
			IntPtr dataWebpPtr = IntPtr.Zero;
#else
			byte[] dataWebp = null;
#endif
			WebPPicture wpic = new WebPPicture();
			BitmapData bmpData = null;
			WebPAuxStats stats = new WebPAuxStats();
			IntPtr ptrStats = IntPtr.Zero;
#if !UNSAFE
			GCHandle pinnedArrayHandle = new GCHandle();
#endif
			try {
				//Validate the configuration
				if (UnsafeNativeMethods.WebPValidateConfig(ref config) != 1)
					throw new Exception("Bad configuration parameters");

				short w, h;
				TestPixelMapBeforeEncode(pixelMap, out w, out h);

				// Set up the input data, allocating the bitmap, width and height
				ImportColorData(pixelMap, true, out bmpData, ref wpic);

				//Set up statistics of compression
				if (info) {
					ptrStats = Marshal.AllocHGlobal(Marshal.SizeOf(stats));
					Marshal.StructureToPtr(stats, ptrStats, false);
					wpic.stats = ptrStats;
				}

				var dataWebpSize = Math.Max(1024, checked(pixelMap.Width * pixelMap.Height * 2));
#if UNSAFE
				//Memory for WebP output
				dataWebpPtr = Marshal.AllocHGlobal(dataWebpSize);
				var initPtr = (byte*)dataWebpPtr.ToPointer();
#else
				dataWebp = new byte[dataWebpSize];
				pinnedArrayHandle = GCHandle.Alloc(dataWebp, GCHandleType.Pinned);
				IntPtr initPtr = pinnedArrayHandle.AddrOfPinnedObject();
#endif
				wpic.custom_ptr = initPtr;

				//Set up a byte-writing method (write-to-memory, in this case)
				_myWriterDelegate = new UnsafeNativeMethods.WebPMemoryWrite(MyWriter);
				wpic.writer = Marshal.GetFunctionPointerForDelegate(_myWriterDelegate);

				//compress the input samples
				if (UnsafeNativeMethods.WebPEncode(ref config, ref wpic) != 1)
					throw new Exception("Encoding error: " + ((WebPEncodingError)wpic.error_code).ToString());

				//Remove OnCallback
				_myWriterDelegate = null;

				//Unlock the pixels
				pixelMap.UnlockBits(bmpData);
				bmpData = null;

				//Copy webpData to rawWebP
				int size    = checked((int)((long)wpic.custom_ptr - (long)initPtr));
				var rawWebP = new byte[size];
#if DEBUG
				int le = dataWebp.Length;
				if ((le > 4096) && (le > (size * 5))) {
					Console.Error.WriteLine("Buffer overallocation for dataWebp: needed {0:n0} allocated {1:n0}", size, le);
				} else if (le < size) {
					Console.Error.WriteLine("Buffer under allocation for dataWebp: needed {0:n0} allocated {1:n0} for {2}x{3}", size, le, pixelMap.Width, pixelMap.Height);
				}
#endif
#if UNSAFE
				Marshal.Copy(dataWebpPtr, rawWebP, 0, size); // TODO: directly pass unmanaged pointer to metadata encode
#else
				Array.Copy(dataWebp, rawWebP, size);
#endif

				//Remove compression data
#if UNSAFE
				Marshal.FreeHGlobal(dataWebpPtr);
				dataWebpPtr = IntPtr.Zero;
#else
				pinnedArrayHandle.Free();
#endif

				//Show statistics
				if (info) {
					stats = (WebPAuxStats)Marshal.PtrToStructure(ptrStats, typeof(WebPAuxStats));
					//MessageBox.Show(
					Debug.Print("Dimension: " + wpic.width + " x " + wpic.height + " pixels\n" +
								"Output:    " + stats.coded_size + " bytes\n" +
								"PSNR Y:    " + stats.PSNRY + " db\n" +
								"PSNR u:    " + stats.PSNRU + " db\n" +
								"PSNR v:    " + stats.PSNRV + " db\n" +
								"PSNR ALL:  " + stats.PSNRALL + " db\n" +
								"Block intra4:  " + stats.block_count_intra4 + "\n" +
								"Block intra16: " + stats.block_count_intra16 + "\n" +
								"Block skipped: " + stats.block_count_skipped + "\n" +
								"Header size:    " + stats.header_bytes + " bytes\n" +
								"Mode-partition: " + stats.mode_partition_0 + " bytes\n" +
								"Macro-blocks 0: " + stats.segment_size_segments0 + " residuals bytes\n" +
								"Macro-blocks 1: " + stats.segment_size_segments1 + " residuals bytes\n" +
								"Macro-blocks 2: " + stats.segment_size_segments2 + " residuals bytes\n" +
								"Macro-blocks 3: " + stats.segment_size_segments3 + " residuals bytes\n" +
								"Quantizer    0: " + stats.segment_quant_segments0 + " residuals bytes\n" +
								"Quantizer    1: " + stats.segment_quant_segments1 + " residuals bytes\n" +
								"Quantizer    2: " + stats.segment_quant_segments2 + " residuals bytes\n" +
								"Quantizer    3: " + stats.segment_quant_segments3 + " residuals bytes\n" +
								"Filter level 0: " + stats.segment_level_segments0 + " residuals bytes\n" +
								"Filter level 1: " + stats.segment_level_segments1 + " residuals bytes\n" +
								"Filter level 2: " + stats.segment_level_segments2 + " residuals bytes\n" +
								"Filter level 3: " + stats.segment_level_segments3 + " residuals bytes\n", "Compression statistics");
				}
				return rawWebP;
			} finally {
#if UNSAFE
				if (dataWebpPtr != IntPtr.Zero) {
					Marshal.FreeHGlobal(dataWebpPtr);
				}
#else
				Unpin(pinnedArrayHandle);
#endif
				//Free statistics memory
				if (ptrStats != IntPtr.Zero) {
					Marshal.FreeHGlobal(ptrStats);
				}
				UnlockFree(pixelMap, bmpData, wpic);
			}
		}

#if UNSAFE
		unsafe private int MyWriter([In] byte* data, UIntPtr data_size, ref WebPPicture picture)
		{
			var size = (long)data_size;
			Buffer.MemoryCopy(data, picture.custom_ptr, size, size);
			picture.custom_ptr += size;
		}
#else
		private int MyWriter([In] IntPtr data, UIntPtr data_size, ref WebPPicture picture)
		{
			//UnsafeNativeMethods.CopyMemory(picture.custom_ptr, data, (uint)data_size);
			var size = (int)data_size;
			var buffer = new byte[size];
			Marshal.Copy(data, buffer, 0, size);
			Marshal.Copy(buffer, 0, picture.custom_ptr, size);

			//picture.custom_ptr = IntPtr.Add(picture.custom_ptr, (int)data_size);   //Only in .NET > 4.0
			picture.custom_ptr = new IntPtr(picture.custom_ptr.ToInt64() + (int)data_size);
			return 1;
		}
#endif

		private static BitmapData LockAllBits(Bitmap toLock, ImageLockMode mode)
		{
			return toLock.LockBits(new Rectangle(0, 0, toLock.Width, toLock.Height), mode, toLock.PixelFormat);
		}

		private static void ImportColorData(Bitmap source, bool forceArgb, out BitmapData bmpData, ref WebPPicture wpic)
		{
			// Set up the source picture data, allocating the bitmap, width and height
			bmpData = LockAllBits(source, ImageLockMode.ReadOnly);
			if (UnsafeNativeMethods.WebPPictureInitInternal(ref wpic) != 1) {
				throw new Exception("Can´t initialize WebPPictureInit");
			}
			wpic.width  = source.Width;
			wpic.height = source.Height;
			bool blnAlpha = (bmpData.PixelFormat == PixelFormat.Format32bppArgb);
			wpic.use_argb = (forceArgb || blnAlpha) ? 1 : 0;

			//Put the source bitmap components in wpic
			if (bmpData.PixelFormat == PixelFormat.Format32bppArgb) {
				if (UnsafeNativeMethods.WebPPictureImportBGRA(ref wpic, bmpData.Scan0, bmpData.Stride) != 1) {
					throw new InsufficientMemoryException("Can´t allocate memory in WebPPictureImportBGRA");
				}
				if (forceArgb) {
					wpic.colorspace = (uint)WEBP_CSP_MODE.MODE_bgrA; // do we need that?
				}
			} else {
				if (UnsafeNativeMethods.WebPPictureImportBGR(ref wpic, bmpData.Scan0, bmpData.Stride) != 1) {
					throw new InsufficientMemoryException("Can´t allocate memory in WebPPictureImportBGR");
				}
			}
		}

		private static void TestPixelMapBeforeEncode(Bitmap pixelMap, out short w, out short h)
		{
			const int kWebpMaxDimension = 16383;
			//test bmp
			w = (short)pixelMap.Width;
			h = (short)pixelMap.Height;
			if (w == 0 || h == 0) {
				throw new ArgumentException("Bitmap contains no data.", "pixelMap");
			}
			if (w > kWebpMaxDimension || h > kWebpMaxDimension) {
				throw new NotSupportedException("Bitmap dimensions are too large. Max is 16383x16383 pixels.");
			}
			var f = pixelMap.PixelFormat;
			if (f != PixelFormat.Format24bppRgb && f != PixelFormat.Format32bppArgb) {
				throw new NotSupportedException("Supported pixel formats are Format24bppRgb and Format32bppArgb only.");
			}
		}

		private static Bitmap CoreDecode(byte[] rawWebP, WebPDecoderConfig config, IntPtr ptrRawWebP, out BitmapData bmpData)
		{
			//Create a BitmapData and Lock all pixels to be written
			var blnAlpha = config.input.Has_alpha == 1;
			config.output.colorspace = blnAlpha ? WEBP_CSP_MODE.MODE_bgrA : WEBP_CSP_MODE.MODE_BGR;
			var pixelMap = new Bitmap(config.input.Width, config.input.Height,
				blnAlpha ? PixelFormat.Format32bppArgb: PixelFormat.Format24bppRgb);

			bmpData = LockAllBits(pixelMap, ImageLockMode.WriteOnly); // caller have to unlock

			// Specify the output format
			config.output.u.RGBA.rgba        = bmpData.Scan0;
			config.output.u.RGBA.stride      = bmpData.Stride;
			config.output.u.RGBA.size        = (UIntPtr)(pixelMap.Height * bmpData.Stride);
			config.output.height             = pixelMap.Height;
			config.output.width              = pixelMap.Width;
			config.output.is_external_memory = 1;

			// Decode
			try {
				var result = UnsafeNativeMethods.WebPDecode(ptrRawWebP, rawWebP.Length, ref config);
				if (result != VP8StatusCode.VP8_STATUS_OK) {
					throw new Exception("Failed WebPDecode with error " + result);
				}
				return pixelMap;
			} finally {
				UnsafeNativeMethods.WebPFreeDecBuffer(ref config.output);
			}
		}

		private static Bitmap GetThumbnail(byte[] rawWebP, short width, short height, bool fancy)
		{
			var           pinnedWebP = GCHandle.Alloc(rawWebP, GCHandleType.Pinned);
			Bitmap        pixelMap   = null;
			BitmapData    bmpData    = null;
			var           config     = new WebPDecoderConfig();
			VP8StatusCode result;

			try {
				if (UnsafeNativeMethods.WebPInitDecoderConfig(ref config) == 0) {
					throw new Exception("WebPInitDecoderConfig failed. Wrong version?");
				}

				IntPtr ptrRawWebP = pinnedWebP.AddrOfPinnedObject();
				if (fancy) {
					result = UnsafeNativeMethods.WebPGetFeatures(ptrRawWebP, rawWebP.Length, ref config.input);
					if (result != VP8StatusCode.VP8_STATUS_OK) {
						throw new ExternalException("Failed WebPGetFeatures with error " + result, (int)result);
					}
				}

				// Set up decode options
				config.options.bypass_filtering    = fancy ? 0 : 1;
				config.options.no_fancy_upsampling = fancy ? 0 : 1;
				config.options.use_threads         = 1;
				config.options.use_scaling         = 1;
				config.options.scaled_width        = width;
				config.options.scaled_height       = height;

				//Create a BitmapData and Lock all pixels to be written
				bool blnAlpha = fancy && (config.input.Has_alpha == 1);
				config.output.colorspace = blnAlpha ? WEBP_CSP_MODE.MODE_bgrA : WEBP_CSP_MODE.MODE_BGR;
				pixelMap = new Bitmap(width, height, blnAlpha ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb);
				bmpData = LockAllBits(pixelMap, ImageLockMode.WriteOnly);

				// Specify the output format
				if (!fancy) {
					config.output.colorspace = WEBP_CSP_MODE.MODE_BGR;
				}
				config.output.u.RGBA.rgba        = bmpData.Scan0;
				config.output.u.RGBA.stride      = bmpData.Stride;
				config.output.u.RGBA.size        = (UIntPtr)(height * bmpData.Stride);
				config.output.height             = height;
				config.output.width              = width;
				config.output.is_external_memory = 1;

				// Decode
				result = UnsafeNativeMethods.WebPDecode(ptrRawWebP, rawWebP.Length, ref config);
				if (result != VP8StatusCode.VP8_STATUS_OK) {
					throw new ExternalException("Failed WebPDecode with error " + result, (int)result);
				}
				return pixelMap;
			} finally {
				UnsafeNativeMethods.WebPFreeDecBuffer(ref config.output);
				UnlockPin(pixelMap, bmpData, pinnedWebP);
			}
		}

		private static byte[] CoreEncode(Bitmap pixelMap, byte? quality)
		{
			short w, h;
			TestPixelMapBeforeEncode(pixelMap, out w, out h);

			BitmapData bmpData       = null;
			IntPtr     unmanagedData = IntPtr.Zero;

			try {
				//Get bmp data
				bmpData = LockAllBits(pixelMap, ImageLockMode.ReadOnly);

				//Compress the bmp data
				int size;
				var noA = (pixelMap.PixelFormat == PixelFormat.Format24bppRgb);
				var bs0 = bmpData.Scan0;
				var bst = bmpData.Stride;
				if (quality.HasValue) {
					float qf = quality.Value;
					size = noA
						? UnsafeNativeMethods.WebPEncodeBGR(bs0, w,h, bst, qf, out unmanagedData)
						: UnsafeNativeMethods.WebPEncodeBGRA(bs0, w, h, bst, qf, out unmanagedData);
				} else {
					size = noA
						? UnsafeNativeMethods.WebPEncodeLosslessBGR(bs0, w, h, bst, out unmanagedData)
						: UnsafeNativeMethods.WebPEncodeLosslessBGRA(bs0, w, h, bst, out unmanagedData);
				}

				if (size == 0) {
					throw new Exception("Can´t encode WebP");
				}

				//Copy image compress data to output array
				byte[] rawWebP = new byte[size];
				Marshal.Copy(unmanagedData, rawWebP, 0, size);

				return rawWebP;
			} finally {
				UnlockFree(pixelMap, bmpData, unmanagedData);
			}
		}

		private static WebPConfig InitEncodeConfig(byte quality)
		{
			WebPConfig config = new WebPConfig();

			//Set compression parameters
			if (UnsafeNativeMethods.WebPConfigInit(ref config, WebPPreset.WEBP_PRESET_DEFAULT, quality) == 0) {
				throw new Exception("Can´t configure preset");
			}
			return config;
		}

		private static WebPConfig InitEncodeConfig(bool losslessPreset, byte quality, byte speed,
		out bool newerWebpLibrary)
		{
			var config = InitEncodeConfig(quality);
			newerWebpLibrary = UnsafeNativeMethods.WebPGetDecoderVersion() > 1082;
			// Init lossless preset if requested and possible
			if (losslessPreset && newerWebpLibrary && UnsafeNativeMethods.WebPConfigLosslessPreset(ref config, speed) == 0) {
				throw new Exception("Can´t configure lossless preset");
			}
			// Configure common encode options
			config.pass            = speed + 1;
			config.thread_level    = 1;
			config.alpha_filtering = 2;
			if (newerWebpLibrary) {
				config.use_sharp_yuv = 1;
			}
			if (losslessPreset) {
				config.exact = 0;
			}

			return config;
		}

		private static WebPConfig InitEncodeConfig(EncodingMode mode, byte quality, byte speed)
		{
			bool blnNewer;
			var  q2     = (mode == EncodingMode.NearLossless) ? (byte)((speed + 1) * 10) : quality;
			var  config = InitEncodeConfig(mode != EncodingMode.Lossy, q2, speed, out blnNewer);
			if (mode == EncodingMode.Lossy) {
				// Add additional tuning:
				ConfigureMethodAndQuality(ref config, speed, quality);
				config.autofilter    = 1;
				config.segments      = 4;
				config.partitions    = 3;
				config.alpha_quality = quality;
				config.preprocessing = blnNewer ? 4 : 3; //Old version does not support preprocessing 4
			} else if (mode == EncodingMode.Lossless) {
				if (!blnNewer) {
					config.lossless = 1;
					ConfigureMethodAndQuality(ref config, speed, quality);
				}
			} else if (mode == EncodingMode.NearLossless) {
				if (!blnNewer) {
					throw new NotSupportedException("This DLL version not support EncodeNearLossless");
				}
				config.near_lossless = quality;
			}

			return config;
		}

		private static void ConfigureMethodAndQuality(ref WebPConfig config, byte speed, byte quality)
		{
			config.method  = Math.Min(speed, (byte)6);
			config.quality = quality;
		}
		#endregion

		#region | Destruction |
		private bool _disposed;
		private GCHandle _pinnedWebP;
		private WebPAnimDecoder _webPAnimDecoder;
		private uint _frameCount;

		private void DisposeOldDecoder()
		{
			if (_webPAnimDecoder.decoder != IntPtr.Zero) {
				UnsafeNativeMethods.WebPAnimDecoderDelete(_webPAnimDecoder.decoder);
				_webPAnimDecoder.decoder = IntPtr.Zero;
				if (_pinnedWebP.IsAllocated)
					_pinnedWebP.Free();
			}
		}

		private void Dispose(bool disposing)
		{
			if (_disposed)
				return;

			if (disposing) {
				// TODO: dispose managed state (managed objects).
			}

			DisposeOldDecoder();

			_disposed = true;
		}

		/// <summary>Free memory</summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~WebP()
		{
			Dispose(false);
		}

		private static void UnlockPin(Bitmap pixelMap, BitmapData data, GCHandle pinnedWebP)
		{
			//Unlock the pixels
			if (data != null) {
				pixelMap.UnlockBits(data);
			}
			//Free memory
			if (pinnedWebP.IsAllocated) {
				pinnedWebP.Free();
			}
		}

		private static void UnlockFree(Bitmap pixelMap, BitmapData data, IntPtr unmanagedData)
		{
			//Unlock the pixels
			if (data != null) {
				pixelMap.UnlockBits(data);
			}
			//Free memory
			if (unmanagedData != IntPtr.Zero) {
				UnsafeNativeMethods.WebPFree(unmanagedData);
			}
		}

		private static void UnlockFree(Bitmap pixelMap, BitmapData data, WebPPicture wpic)
		{
			//Unlock the pixels
			if (data != null) {
				pixelMap.UnlockBits(data);
			}
			//Free memory
			if (wpic.argb != IntPtr.Zero) {
				UnsafeNativeMethods.WebPPictureFree(ref wpic);
			}
		}

		private static void Unpin(GCHandle pinnedWebP)
		{
			//Free memory
			if (pinnedWebP.IsAllocated) {
				pinnedWebP.Free();
			}
		}
		#endregion
	}
}
