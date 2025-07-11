/////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Wrapper for WebP format in C#. (MIT) Jose M. Piñeiro and others
/////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Decode Functions:
// Bitmap Load(string pathFileName) - Load a WebP file in bitmap.
// Bitmap Decode(byte[] rawWebP) - Decode WebP data (rawWebP) to bitmap.
// Bitmap Decode(byte[] rawWebP, WebPDecoderOptions options) - Decode WebP data (rawWebP) to bitmap using 'options'.
// Bitmap GetThumbnailFast(byte[] rawWebP, int width, int height) - Get a thumbnail from WebP data (rawWebP) with dimensions 'width x height'. Fast mode.
// Bitmap GetThumbnailQuality(byte[] rawWebP, int width, int height) - Fast get a thumbnail from WebP data (rawWebP) with dimensions 'width x height'. Quality mode.
//
// Encode Functions:
// Save(Bitmap pixelMap, string pathFileName, int quality) - Save bitmap with quality lost to WebP file. Optionally select 'quality'.
// byte[] EncodeLossy(Bitmap pixelMap, int quality) - Encode bitmap with quality lost to WebP byte array. Optionally select 'quality'.
// byte[] EncodeLossy(Bitmap pixelMap, int quality, int speed, bool info) - Encode bitmap with quality lost to WebP byte array. Select 'quality', 'speed' and optionally select 'info'.
// byte[] EncodeLossless(Bitmap pixelMap) - Encode bitmap without quality lost to WebP byte array.
// byte[] EncodeLossless(Bitmap pixelMap, int speed, bool info = false) - Encode bitmap without quality lost to WebP byte array. Select 'speed'.
// byte[] EncodeNearLossless(Bitmap pixelMap, int quality, int speed = 9, bool info = false) - Encode bitmap with a near lossless method to WebP byte array. Select 'quality', 'speed' and optionally select 'info'.
//
// Another functions:
// string GetVersion() - Get the library version
// GetInfo(byte[] rawWebP, out int width, out int height, out bool has_alpha, out bool has_animation, out string format) - Get information of WEBP data
// float[] PictureDistortion(Bitmap source, Bitmap reference, int metric_type) - Get PSNR, SSIM or LSIM distortion metric between two pictures
////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace WebPWrapper
{
	public sealed class WebP : IDisposable
	{
		private const int WEBP_MAX_DIMENSION = 16383;

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
			GCHandle pinnedWebP = GCHandle.Alloc(rawWebP, GCHandleType.Pinned);

			try {
				//Get image width and height
				int imgWidth, imgHeight;
				bool hasAlpha, hasAnimation;
				string format;
				GetInfo(rawWebP, out imgWidth, out imgHeight, out hasAlpha, out hasAnimation, out format);

				//Create a BitmapData and Lock all pixels to be written
				if (hasAlpha)
					pixelMap = new Bitmap(imgWidth, imgHeight, PixelFormat.Format32bppArgb);
				else
					pixelMap = new Bitmap(imgWidth, imgHeight, PixelFormat.Format24bppRgb);
				bmpData = pixelMap.LockBits(new Rectangle(0, 0, imgWidth, imgHeight), ImageLockMode.WriteOnly, pixelMap.PixelFormat);

				//Uncompress the image
				int outputSize = bmpData.Stride * imgHeight;
				IntPtr ptrData = pinnedWebP.AddrOfPinnedObject();
				if (pixelMap.PixelFormat == PixelFormat.Format24bppRgb)
					UnsafeNativeMethods.WebPDecodeBGRInto(ptrData, rawWebP.Length, bmpData.Scan0, outputSize, bmpData.Stride);
				else
					UnsafeNativeMethods.WebPDecodeBGRAInto(ptrData, rawWebP.Length, bmpData.Scan0, outputSize, bmpData.Stride);

				return pixelMap;
			} finally {
				//Unlock the pixels
				if (bmpData != null)
					pixelMap.UnlockBits(bmpData);

				//Free memory
				if (pinnedWebP.IsAllocated)
					pinnedWebP.Free();
			}
		}

		/// <summary>Decode a WebP image</summary>
		/// <param name="rawWebP">the data to uncompress</param>
		/// <param name="options">Options for advanced decode</param>
		/// <returns>Bitmap with the WebP image</returns>
		public Bitmap Decode(byte[] rawWebP, WebPDecoderOptions options, PixelFormat pixelFormat = PixelFormat.DontCare)
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
				int height;
				int width;
				if (options.use_scaling == 0) {
					result = UnsafeNativeMethods.WebPGetFeatures(ptrRawWebP, rawWebP.Length, ref config.input);
					if (result != VP8StatusCode.VP8_STATUS_OK)
						throw new Exception("Failed WebPGetFeatures with error " + result);

					//Test cropping values
					if (options.use_cropping == 1) {
						if (options.crop_left + options.crop_width > config.input.Width || options.crop_top + options.crop_height > config.input.Height)
							throw new Exception("Crop options exceeded WebP image dimensions");
						width = options.crop_width;
						height = options.crop_height;
					}
				} else {
					width = options.scaled_width;
					height = options.scaled_height;
				}

				config.options.bypass_filtering = options.bypass_filtering;
				config.options.no_fancy_upsampling = options.no_fancy_upsampling;
				config.options.use_cropping = options.use_cropping;
				config.options.crop_left = options.crop_left;
				config.options.crop_top = options.crop_top;
				config.options.crop_width = options.crop_width;
				config.options.crop_height = options.crop_height;
				config.options.use_scaling = options.use_scaling;
				config.options.scaled_width = options.scaled_width;
				config.options.scaled_height = options.scaled_height;
				config.options.use_threads = options.use_threads;
				config.options.dithering_strength = options.dithering_strength;
				config.options.flip = options.flip;
				config.options.alpha_dithering_strength = options.alpha_dithering_strength;

				//Create a BitmapData and Lock all pixels to be written
				if (config.input.Has_alpha == 1) {
					config.output.colorspace = WEBP_CSP_MODE.MODE_bgrA;
					pixelMap = new Bitmap(config.input.Width, config.input.Height, PixelFormat.Format32bppArgb);
				} else {
					config.output.colorspace = WEBP_CSP_MODE.MODE_BGR;
					pixelMap = new Bitmap(config.input.Width, config.input.Height, PixelFormat.Format24bppRgb);
				}
				bmpData = pixelMap.LockBits(new Rectangle(0, 0, pixelMap.Width, pixelMap.Height), ImageLockMode.WriteOnly, pixelMap.PixelFormat);

				// Specify the output format
				config.output.u.RGBA.rgba = bmpData.Scan0;
				config.output.u.RGBA.stride = bmpData.Stride;
				config.output.u.RGBA.size = (UIntPtr)(pixelMap.Height * bmpData.Stride);
				config.output.height = pixelMap.Height;
				config.output.width = pixelMap.Width;
				config.output.is_external_memory = 1;

				// Decode
				result = UnsafeNativeMethods.WebPDecode(ptrRawWebP, rawWebP.Length, ref config);
				if (result != VP8StatusCode.VP8_STATUS_OK) {
					throw new Exception("Failed WebPDecode with error " + result);
				}
				UnsafeNativeMethods.WebPFreeDecBuffer(ref config.output);

				return pixelMap;
			} finally {
				//Unlock the pixels
				if (bmpData != null)
					pixelMap.UnlockBits(bmpData);

				//Free memory
				if (pinnedWebP.IsAllocated)
					pinnedWebP.Free();
			}
		}

		/// <summary>Get Thumbnail from webP in mode faster/low quality</summary>
		/// <param name="rawWebP">The data to uncompress</param>
		/// <param name="width">Wanted width of thumbnail</param>
		/// <param name="height">Wanted height of thumbnail</param>
		/// <returns>Bitmap with the WebP thumbnail in 24bpp</returns>
		public Bitmap GetThumbnailFast(byte[] rawWebP, int width, int height)
		{
			GCHandle pinnedWebP = GCHandle.Alloc(rawWebP, GCHandleType.Pinned);
			Bitmap pixelMap = null;
			BitmapData bmpData = null;

			try {
				WebPDecoderConfig config = new WebPDecoderConfig();
				if (UnsafeNativeMethods.WebPInitDecoderConfig(ref config) == 0)
					throw new Exception("WebPInitDecoderConfig failed. Wrong version?");

				// Set up decode options
				config.options.bypass_filtering = 1;
				config.options.no_fancy_upsampling = 1;
				config.options.use_threads = 1;
				config.options.use_scaling = 1;
				config.options.scaled_width = width;
				config.options.scaled_height = height;

				// Create a BitmapData and Lock all pixels to be written
				pixelMap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
				bmpData = pixelMap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, pixelMap.PixelFormat);

				// Specify the output format
				config.output.colorspace = WEBP_CSP_MODE.MODE_BGR;
				config.output.u.RGBA.rgba = bmpData.Scan0;
				config.output.u.RGBA.stride = bmpData.Stride;
				config.output.u.RGBA.size = (UIntPtr)(height * bmpData.Stride);
				config.output.height = height;
				config.output.width = width;
				config.output.is_external_memory = 1;

				// Decode
				IntPtr ptrRawWebP = pinnedWebP.AddrOfPinnedObject();
				VP8StatusCode result = UnsafeNativeMethods.WebPDecode(ptrRawWebP, rawWebP.Length, ref config);
				if (result != VP8StatusCode.VP8_STATUS_OK)
					throw new Exception("Failed WebPDecode with error " + result);

				UnsafeNativeMethods.WebPFreeDecBuffer(ref config.output);

				return pixelMap;
			} finally {
				//Unlock the pixels
				if (bmpData != null)
					pixelMap.UnlockBits(bmpData);

				//Free memory
				if (pinnedWebP.IsAllocated)
					pinnedWebP.Free();
			}
		}

		/// <summary>Thumbnail from webP in mode slow/high quality</summary>
		/// <param name="rawWebP">The data to uncompress</param>
		/// <param name="width">Wanted width of thumbnail</param>
		/// <param name="height">Wanted height of thumbnail</param>
		/// <returns>Bitmap with the WebP thumbnail</returns>
		public Bitmap GetThumbnailQuality(byte[] rawWebP, int width, int height)
		{
			GCHandle pinnedWebP = GCHandle.Alloc(rawWebP, GCHandleType.Pinned);
			Bitmap pixelMap = null;
			BitmapData bmpData = null;

			try {
				WebPDecoderConfig config = new WebPDecoderConfig();
				if (UnsafeNativeMethods.WebPInitDecoderConfig(ref config) == 0)
					throw new Exception("WebPInitDecoderConfig failed. Wrong version?");

				IntPtr ptrRawWebP = pinnedWebP.AddrOfPinnedObject();
				VP8StatusCode result = UnsafeNativeMethods.WebPGetFeatures(ptrRawWebP, rawWebP.Length, ref config.input);
				if (result != VP8StatusCode.VP8_STATUS_OK)
					throw new Exception("Failed WebPGetFeatures with error " + result);

				// Set up decode options
				config.options.bypass_filtering = 0;
				config.options.no_fancy_upsampling = 0;
				config.options.use_threads = 1;
				config.options.use_scaling = 1;
				config.options.scaled_width = width;
				config.options.scaled_height = height;

				//Create a BitmapData and Lock all pixels to be written
				if (config.input.Has_alpha == 1) {
					config.output.colorspace = WEBP_CSP_MODE.MODE_bgrA;
					pixelMap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
				} else {
					config.output.colorspace = WEBP_CSP_MODE.MODE_BGR;
					pixelMap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
				}
				bmpData = pixelMap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, pixelMap.PixelFormat);

				// Specify the output format
				config.output.u.RGBA.rgba = bmpData.Scan0;
				config.output.u.RGBA.stride = bmpData.Stride;
				config.output.u.RGBA.size = (UIntPtr)(height * bmpData.Stride);
				config.output.height = height;
				config.output.width = width;
				config.output.is_external_memory = 1;

				// Decode
				result = UnsafeNativeMethods.WebPDecode(ptrRawWebP, rawWebP.Length, ref config);
				if (result != VP8StatusCode.VP8_STATUS_OK)
					throw new Exception("Failed WebPDecode with error " + result);

				UnsafeNativeMethods.WebPFreeDecBuffer(ref config.output);

				return pixelMap;
			} finally {
				//Unlock the pixels
				if (bmpData != null)
					pixelMap.UnlockBits(bmpData);

				//Free memory
				if (pinnedWebP.IsAllocated)
					pinnedWebP.Free();
			}
		}
		#endregion

		#region | Public Encode Functions |
		/// <summary>Save bitmap to file in WebP format</summary>
		/// <param name="pixelMap">Bitmap with the WebP image</param>
		/// <param name="pathFileName">The file to write</param>
		/// <param name="quality">Between 0 (lower quality, lowest file size) and 100 (highest quality, higher file size)</param>
		public void Save(Bitmap pixelMap, string pathFileName, int quality = 75)
		{
			//Encode in webP format
			byte[] rawWebP = EncodeLossy(pixelMap, quality);

			//Write webP file
			File.WriteAllBytes(pathFileName, rawWebP);
		}

		/// <summary>Lossy encoding bitmap to WebP (Simple encoding API)</summary>
		/// <param name="pixelMap">Bitmap with the image</param>
		/// <param name="quality">Between 0 (lower quality, lowest file size) and 100 (highest quality, higher file size)</param>
		/// <returns>Compressed data</returns>
		public byte[] EncodeLossy(Bitmap pixelMap, int quality = 75)
		{
			//test bmp
			if (pixelMap.Width == 0 || pixelMap.Height == 0)
				throw new ArgumentException("Bitmap contains no data.", "pixelMap");
			if (pixelMap.Width > WEBP_MAX_DIMENSION || pixelMap.Height > WEBP_MAX_DIMENSION)
				throw new NotSupportedException("Bitmap's dimension is too large. Max is " + WEBP_MAX_DIMENSION + "x" + WEBP_MAX_DIMENSION + " pixels.");
			if (pixelMap.PixelFormat != PixelFormat.Format24bppRgb && pixelMap.PixelFormat != PixelFormat.Format32bppArgb)
				throw new NotSupportedException("Only support Format24bppRgb and Format32bppArgb pixelFormat.");

			BitmapData bmpData = null;
			IntPtr unmanagedData = IntPtr.Zero;

			try {
				int size;

				//Get bmp data
				bmpData = pixelMap.LockBits(new Rectangle(0, 0, pixelMap.Width, pixelMap.Height), ImageLockMode.ReadOnly, pixelMap.PixelFormat);

				//Compress the bmp data
				if (pixelMap.PixelFormat == PixelFormat.Format24bppRgb)
					size = UnsafeNativeMethods.WebPEncodeBGR(bmpData.Scan0, pixelMap.Width, pixelMap.Height, bmpData.Stride, quality, out unmanagedData);
				else
					size = UnsafeNativeMethods.WebPEncodeBGRA(bmpData.Scan0, pixelMap.Width, pixelMap.Height, bmpData.Stride, quality, out unmanagedData);
				if (size == 0)
					throw new Exception("Can´t encode WebP");

				//Copy image compress data to output array
				byte[] rawWebP = new byte[size];
				Marshal.Copy(unmanagedData, rawWebP, 0, size);

				return rawWebP;
			} finally {
				//Unlock the pixels
				if (bmpData != null)
					pixelMap.UnlockBits(bmpData);

				//Free memory
				if (unmanagedData != IntPtr.Zero)
					UnsafeNativeMethods.WebPFree(unmanagedData);
			}
		}

		/// <summary>Lossy encoding bitmap to WebP (Advanced encoding API)</summary>
		/// <param name="pixelMap">Bitmap with the image</param>
		/// <param name="quality">Between 0 (lower quality, lowest file size) and 100 (highest quality, higher file size)</param>
		/// <param name="speed">Between 0 (fastest, lowest compression) and 9 (slower, best compression)</param>
		/// <returns>Compressed data</returns>
		public byte[] EncodeLossy(Bitmap pixelMap, int quality, int speed, bool info = false)
		{
			//Initialize configuration structure
			WebPConfig config = new WebPConfig();

			//Set compression parameters
			if (UnsafeNativeMethods.WebPConfigInit(ref config, WebPPreset.WEBP_PRESET_DEFAULT, 75) == 0)
				throw new Exception("Can´t configure preset");

			// Add additional tuning:
			config.method = speed;
			if (config.method > 6)
				config.method = 6;
			config.quality = quality;
			config.autofilter = 1;
			config.pass = speed + 1;
			config.segments = 4;
			config.partitions = 3;
			config.thread_level = 1;
			config.alpha_quality = quality;
			config.alpha_filtering = 2;
			config.use_sharp_yuv = 1;

			if (UnsafeNativeMethods.WebPGetDecoderVersion() > 1082)     //Old version does not support preprocessing 4
			{
				config.preprocessing = 4;
				config.use_sharp_yuv = 1;
			} else
				config.preprocessing = 3;

			return AdvancedEncode(pixelMap, config, info);
		}

		/// <summary>Lossless encoding bitmap to WebP (Simple encoding API)</summary>
		/// <param name="pixelMap">Bitmap with the image</param>
		/// <returns>Compressed data</returns>
		public byte[] EncodeLossless(Bitmap pixelMap)
		{
			//test bmp
			if (pixelMap.Width == 0 || pixelMap.Height == 0)
				throw new ArgumentException("Bitmap contains no data.", "pixelMap");
			if (pixelMap.Width > WEBP_MAX_DIMENSION || pixelMap.Height > WEBP_MAX_DIMENSION)
				throw new NotSupportedException("Bitmap's dimension is too large. Max is " + WEBP_MAX_DIMENSION + "x" + WEBP_MAX_DIMENSION + " pixels.");
			if (pixelMap.PixelFormat != PixelFormat.Format24bppRgb && pixelMap.PixelFormat != PixelFormat.Format32bppArgb)
				throw new NotSupportedException("Only support Format24bppRgb and Format32bppArgb pixelFormat.");

			BitmapData bmpData = null;
			IntPtr unmanagedData = IntPtr.Zero;
			try {
				//Get bmp data
				bmpData = pixelMap.LockBits(new Rectangle(0, 0, pixelMap.Width, pixelMap.Height), ImageLockMode.ReadOnly, pixelMap.PixelFormat);

				//Compress the bmp data
				int size;
				if (pixelMap.PixelFormat == PixelFormat.Format24bppRgb)
					size = UnsafeNativeMethods.WebPEncodeLosslessBGR(bmpData.Scan0, pixelMap.Width, pixelMap.Height, bmpData.Stride, out unmanagedData);
				else
					size = UnsafeNativeMethods.WebPEncodeLosslessBGRA(bmpData.Scan0, pixelMap.Width, pixelMap.Height, bmpData.Stride, out unmanagedData);

				//Copy image compress data to output array
				byte[] rawWebP = new byte[size];
				Marshal.Copy(unmanagedData, rawWebP, 0, size);

				return rawWebP;
			} finally {
				//Unlock the pixels
				if (bmpData != null)
					pixelMap.UnlockBits(bmpData);

				//Free memory
				if (unmanagedData != IntPtr.Zero)
					UnsafeNativeMethods.WebPFree(unmanagedData);
			}
		}

		/// <summary>Lossless encoding image in bitmap (Advanced encoding API)</summary>
		/// <param name="pixelMap">Bitmap with the image</param>
		/// <param name="speed">Between 0 (fastest, lowest compression) and 9 (slower, best compression)</param>
		/// <returns>Compressed data</returns>
		public byte[] EncodeLossless(Bitmap pixelMap, int speed)
		{
			//Initialize configuration structure
			WebPConfig config = new WebPConfig();

			//Set compression parameters
			if (UnsafeNativeMethods.WebPConfigInit(ref config, WebPPreset.WEBP_PRESET_DEFAULT, (speed + 1) * 10) == 0)
				throw new Exception("Can´t config preset");

			//Old version of DLL does not support info and WebPConfigLosslessPreset
			if (UnsafeNativeMethods.WebPGetDecoderVersion() > 1082) {
				if (UnsafeNativeMethods.WebPConfigLosslessPreset(ref config, speed) == 0)
					throw new Exception("Can´t configure lossless preset");
			} else {
				config.lossless = 1;
				config.method = speed;
				if (config.method > 6)
					config.method = 6;
				config.quality = (speed + 1) * 10;
			}
			config.pass = speed + 1;
			config.thread_level = 1;
			config.alpha_filtering = 2;
			config.use_sharp_yuv = 1;
			config.exact = 0;

			return AdvancedEncode(pixelMap, config, false);
		}

		/// <summary>Near lossless encoding image in bitmap</summary>
		/// <param name="pixelMap">Bitmap with the image</param>
		/// <param name="quality">Between 0 (lower quality, lowest file size) and 100 (highest quality, higher file size)</param>
		/// <param name="speed">Between 0 (fastest, lowest compression) and 9 (slower, best compression)</param>
		/// <returns>Compress data</returns>
		public byte[] EncodeNearLossless(Bitmap pixelMap, int quality, int speed = 9)
		{
			//test DLL version
			if (UnsafeNativeMethods.WebPGetDecoderVersion() <= 1082)
				throw new Exception("This DLL version not support EncodeNearLossless");

			//Initialize config struct
			WebPConfig config = new WebPConfig();

			//Set compression parameters
			if (UnsafeNativeMethods.WebPConfigInit(ref config, WebPPreset.WEBP_PRESET_DEFAULT, (speed + 1) * 10) == 0)
				throw new Exception("Can´t configure preset");
			if (UnsafeNativeMethods.WebPConfigLosslessPreset(ref config, speed) == 0)
				throw new Exception("Can´t configure lossless preset");
			config.pass = speed + 1;
			config.near_lossless = quality;
			config.thread_level = 1;
			config.alpha_filtering = 2;
			config.use_sharp_yuv = 1;
			config.exact = 0;

			return AdvancedEncode(pixelMap, config, false);
		}

		public void EncodeWithMeta(Bitmap pixelMap, string path, byte[] rawXmp,
		int quality = 85, int speed = 4, bool multithread = false, int alphaQuality = 100)
		{
			IntPtr mux = UnsafeNativeMethods.WebPNewInternal(0x0108); // TODO: hardcoded libwebp ABI version
			var config = new WebPConfig();
			if (UnsafeNativeMethods.WebPConfigInit(ref config, WebPPreset.WEBP_PRESET_DEFAULT, quality) == 0)
				throw new Exception("Can´t configure preset");
			config.method = Math.Max(Math.Min(speed, 6), 0); // 0 is fastest
			config.thread_level = multithread ? 1 : 0;
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
			byte[] rawWebP = File.ReadAllBytes(pathFileName);

			return AnimDecode(rawWebP);
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
				if (bmpData != null)
					bitmap.UnlockBits(bmpData);

				if (pinnedWebP.IsAllocated)
					pinnedWebP.Free();
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
			byte[] rawWebP = File.ReadAllBytes(pathFileName);

			return AnimInit(rawWebP, out frameCount);
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
		public string GetVersion()
		{
			uint v = (uint)UnsafeNativeMethods.WebPGetDecoderVersion();
			var revision = v % 256;
			var minor = (v >> 8) % 256;
			var major = (v >> 16) % 256;
			return major + "." + minor + "." + revision;
		}

		/// <summary>Get info of WEBP data</summary>
		/// <param name="rawWebP">The data of WebP</param>
		/// <param name="width">width of image</param>
		/// <param name="height">height of image</param>
		/// <param name="has_alpha">Image has alpha channel</param>
		/// <param name="has_animation">Image is a animation</param>
		/// <param name="format">Format of image: 0 = undefined (/mixed), 1 = lossy, 2 = lossless</param>
		public void GetInfo(byte[] rawWebP, out int width, out int height, out bool has_alpha, out bool has_animation, out string format)
		{
			VP8StatusCode result;
			GCHandle pinnedWebP = GCHandle.Alloc(rawWebP, GCHandleType.Pinned);

			try {
				IntPtr ptrRawWebP = pinnedWebP.AddrOfPinnedObject();

				WebPBitstreamFeatures features = new WebPBitstreamFeatures();
				result = UnsafeNativeMethods.WebPGetFeatures(ptrRawWebP, rawWebP.Length, ref features);

				if (result != 0)
					throw new Exception(result.ToString());

				width = features.Width;
				height = features.Height;
				if (features.Has_alpha == 1) has_alpha = true; else has_alpha = false;
				if (features.Has_animation == 1) has_animation = true; else has_animation = false;
				switch (features.Format) {
					case 1:
						format = "lossy";
						break;
					case 2:
						format = "lossless";
						break;
					default:
						format = "undefined";
						break;
				}
			} finally {
				//Free memory
				if (pinnedWebP.IsAllocated)
					pinnedWebP.Free();
			}
		}

		/// <summary>Compute PSNR, SSIM or LSIM distortion metric between two pictures. Warning: this function is rather CPU-intensive</summary>
		/// <param name="source">Picture to measure</param>
		/// <param name="reference">Reference picture</param>
		/// <param name="metric_type">0 = PSNR, 1 = SSIM, 2 = LSIM</param>
		/// <returns>dB in the Y/U/V/Alpha/All order</returns>
		public float[] GetPictureDistortion(Bitmap source, Bitmap reference, int metric_type)
		{
			WebPPicture wpicSource = new WebPPicture();
			WebPPicture wpicReference = new WebPPicture();
			BitmapData sourceBmpData = null;
			BitmapData referenceBmpData = null;
			float[] result = new float[5];
			GCHandle pinnedResult = GCHandle.Alloc(result, GCHandleType.Pinned);

			try {
				if (source == null)
					throw new Exception("Source picture is void");
				if (reference == null)
					throw new Exception("Reference picture is void");
				if (metric_type > 2)
					throw new Exception("Bad metric_type. Use 0 = PSNR, 1 = SSIM, 2 = LSIM");
				if (source.Width != reference.Width || source.Height != reference.Height)
					throw new Exception("Source and Reference pictures have different dimensions");

				// Setup the source picture data, allocating the bitmap, width and height
				sourceBmpData = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, source.PixelFormat);
				wpicSource = new WebPPicture();
				if (UnsafeNativeMethods.WebPPictureInitInternal(ref wpicSource) != 1)
					throw new Exception("Can´t initialize WebPPictureInit");
				wpicSource.width = (int)source.Width;
				wpicSource.height = (int)source.Height;

				//Put the source bitmap componets in wpic
				if (sourceBmpData.PixelFormat == PixelFormat.Format32bppArgb) {
					wpicSource.use_argb = 1;
					if (UnsafeNativeMethods.WebPPictureImportBGRA(ref wpicSource, sourceBmpData.Scan0, sourceBmpData.Stride) != 1)
						throw new Exception("Can´t allocate memory in WebPPictureImportBGR");
				} else {
					wpicSource.use_argb = 0;
					if (UnsafeNativeMethods.WebPPictureImportBGR(ref wpicSource, sourceBmpData.Scan0, sourceBmpData.Stride) != 1)
						throw new Exception("Can´t allocate memory in WebPPictureImportBGR");
				}

				// Setup the reference picture data, allocating the bitmap, width and height
				referenceBmpData = reference.LockBits(new Rectangle(0, 0, reference.Width, reference.Height), ImageLockMode.ReadOnly, reference.PixelFormat);
				wpicReference = new WebPPicture();
				if (UnsafeNativeMethods.WebPPictureInitInternal(ref wpicReference) != 1)
					throw new Exception("Can´t initialize WebPPictureInit");
				wpicReference.width = (int)reference.Width;
				wpicReference.height = (int)reference.Height;
				wpicReference.use_argb = 1;

				//Put the source bitmap contents in WebPPicture instance
				if (sourceBmpData.PixelFormat == PixelFormat.Format32bppArgb) {
					wpicSource.use_argb = 1;
					if (UnsafeNativeMethods.WebPPictureImportBGRA(ref wpicReference, referenceBmpData.Scan0, referenceBmpData.Stride) != 1)
						throw new Exception("Can´t allocate memory in WebPPictureImportBGR");
				} else {
					wpicSource.use_argb = 0;
					if (UnsafeNativeMethods.WebPPictureImportBGR(ref wpicReference, referenceBmpData.Scan0, referenceBmpData.Stride) != 1)
						throw new Exception("Can´t allocate memory in WebPPictureImportBGR");
				}

				//Measure
				IntPtr ptrResult = pinnedResult.AddrOfPinnedObject();
				if (UnsafeNativeMethods.WebPPictureDistortion(ref wpicSource, ref wpicReference, metric_type, ptrResult) != 1)
					throw new Exception("Can´t measure.");
				return result;
			} finally {
				//Unlock the pixels
				if (sourceBmpData != null)
					source.UnlockBits(sourceBmpData);
				if (referenceBmpData != null)
					reference.UnlockBits(referenceBmpData);

				//Free memory
				if (wpicSource.argb != IntPtr.Zero)
					UnsafeNativeMethods.WebPPictureFree(ref wpicSource);
				if (wpicReference.argb != IntPtr.Zero)
					UnsafeNativeMethods.WebPPictureFree(ref wpicReference);
				//Free memory
				if (pinnedResult.IsAllocated)
					pinnedResult.Free();
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
			byte[] rawWebP = null;
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
			int dataWebpSize;
			try {
				//Validate the configuration
				if (UnsafeNativeMethods.WebPValidateConfig(ref config) != 1)
					throw new Exception("Bad configuration parameters");

				//test bmp
				if (pixelMap.Width == 0 || pixelMap.Height == 0)
					throw new ArgumentException("Bitmap contains no data.", "pixelMap");
				if (pixelMap.Width > WEBP_MAX_DIMENSION || pixelMap.Height > WEBP_MAX_DIMENSION)
					throw new NotSupportedException("Bitmap's dimension is too large. Max is " + WEBP_MAX_DIMENSION + "x" + WEBP_MAX_DIMENSION + " pixels.");
				if (pixelMap.PixelFormat != PixelFormat.Format24bppRgb && pixelMap.PixelFormat != PixelFormat.Format32bppArgb)
					throw new NotSupportedException("Only support Format24bppRgb and Format32bppArgb pixelFormat.");

				// Setup the input data, allocating a the bitmap, width and height
				bmpData = pixelMap.LockBits(new Rectangle(0, 0, pixelMap.Width, pixelMap.Height), ImageLockMode.ReadOnly, pixelMap.PixelFormat);
				if (UnsafeNativeMethods.WebPPictureInitInternal(ref wpic) != 1)
					throw new Exception("Can´t initialize WebPPictureInit");
				wpic.width = (int)pixelMap.Width;
				wpic.height = (int)pixelMap.Height;
				wpic.use_argb = 1;

				if (pixelMap.PixelFormat == PixelFormat.Format32bppArgb) {
					//Put the bitmap componets in wpic
					int result = UnsafeNativeMethods.WebPPictureImportBGRA(ref wpic, bmpData.Scan0, bmpData.Stride);
					if (result != 1)
						throw new Exception("Can´t allocate memory in WebPPictureImportBGRA");
					wpic.colorspace = (uint)WEBP_CSP_MODE.MODE_bgrA;
					dataWebpSize = pixelMap.Width * pixelMap.Height * 32;
					dataWebp = new byte[pixelMap.Width * pixelMap.Height * 32];                //Memory for WebP output
				} else {
					//Put the bitmap contents in WebPPicture instance
					int result = UnsafeNativeMethods.WebPPictureImportBGR(ref wpic, bmpData.Scan0, bmpData.Stride);
					if (result != 1)
						throw new Exception("Can´t allocate memory in WebPPictureImportBGR");
					dataWebpSize = pixelMap.Width * pixelMap.Height * 24;
				}

				//Set up statistics of compression
				if (info) {
					stats = new WebPAuxStats();
					ptrStats = Marshal.AllocHGlobal(Marshal.SizeOf(stats));
					Marshal.StructureToPtr(stats, ptrStats, false);
					wpic.stats = ptrStats;
				}

#if UNSAFE
				//Memory for WebP output
				if (dataWebpSize > 2147483591)
					dataWebpSize = 2147483591;

				dataWebpPtr = Marshal.AllocHGlobal(dataWebpSize); // TODO: shouldn't we allocate less? how to know?
                var initPtr = (byte*)dataWebpPtr.ToPointer();
#else
				dataWebp = new byte[pixelMap.Width * pixelMap.Height * 32];
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
#if UNSAFE
                var size = (int)(wpic.custom_ptr - initPtr);
				rawWebP = new byte[size];
				Marshal.Copy(dataWebpPtr, rawWebP, 0, size); // TODO: directly pass unmanaged pointer to metadata encode
#else
				int size = (int)((long)wpic.custom_ptr - (long)initPtr);
				rawWebP = new byte[size];
				Array.Copy(dataWebp, rawWebP, size);
#endif

				//Remove compression data
#if UNSAFE
				Marshal.FreeHGlobal(dataWebpPtr);
                dataWebpPtr = IntPtr.Zero;
#else
				pinnedArrayHandle.Free();
				dataWebp = null;
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
				//Free temporal compress memory
#if UNSAFE
				if (dataWebpPtr != IntPtr.Zero) {
					Marshal.FreeHGlobal(dataWebpPtr);
				}
#else
				if (pinnedArrayHandle.IsAllocated) {
					pinnedArrayHandle.Free();
				}
#endif

				//Free statistics memory
				if (ptrStats != IntPtr.Zero) {
					Marshal.FreeHGlobal(ptrStats);
				}

				//Unlock the pixels
				if (bmpData != null) {
					pixelMap.UnlockBits(bmpData);
				}

				//Free memory
				if (wpic.argb != IntPtr.Zero) {
					UnsafeNativeMethods.WebPPictureFree(ref wpic);
				}
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

		/// <summary>The writer type for output compress data</summary>
		/// <param name="data">Data returned</param>
		/// <param name="data_size">Size of data returned</param>
		/// <param name="wpic">Picture structure</param>
		/// <returns></returns>
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
#if UNSAFE
		private unsafe delegate int WebPMemoryWrite([In()] byte* data, UIntPtr data_size, ref WebPPicture wpic);
#else
		private delegate int MyWriterDelegate([In] IntPtr data, UIntPtr data_size, ref WebPPicture wpic);
#endif


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
#endregion

		#region | Destruction |
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
#endregion
	}
}
