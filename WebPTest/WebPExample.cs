// clsWebP, by Jose M. Piñeiro
// Website: https://github.com/JosePineiro/WebP-wapper
// Version: 1.0.0.9 (May 23, 2020)

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WebPWrapper;

namespace WebPTest
{
	public partial class WebPExample : Form
	{
		#region | Constructors |
		public WebPExample()
		{
			InitializeComponent();
		}

		private void WebPExample_Load(object sender, EventArgs e)
		{
			try {
				//Inform of execution mode and libWebP version
				this.Text = Application.ProductName + (IntPtr.Size == 8 ? " x64 v" : " x86 v") + Application.ProductVersion +
				 " (libwebp v" + WebP.GetVersion() + ")";
			} catch (Exception ex) {
				ErrorBox(ex, "WebPExample_Load");
			}
		}
		#endregion

		#region << Events >>
		/// <summary>
		/// Test for load from file function
		/// </summary>
		private void ButtonLoad_Click(object sender, EventArgs e)
		{
			try {
				using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
					openFileDialog.Filter = "Image files (*.webp, *.png, *.tif, *.tiff)|*.webp;*.png;*.tif;*.tiff";
					openFileDialog.FileName = "";
					if (openFileDialog.ShowDialog() == DialogResult.OK) {
						this.buttonSave.Enabled = true;
						string pathFileName = openFileDialog.FileName;

						if (Path.GetExtension(pathFileName) == ".webp") {
							byte[] bytes = File.ReadAllBytes(pathFileName);
							var info = WebP.GetInfo(bytes);
							if (!info.IsAnimated) {
								pictureBox.Image = WebP.Decode(bytes);
							} else {
								var frames     = WebP.AnimDecode(bytes, 0, 1);
								var enumerator = frames.GetEnumerator();
								enumerator.MoveNext();
								pictureBox.Image = enumerator.Current.Data;
								enumerator.Dispose();
							}
						} else {
							pictureBox.Image = Image.FromFile(pathFileName);
						}
					}
				}
			} catch (Exception ex) {
				ErrorBox(ex, "ButtonLoad_Click");
			}
		}

		/// <summary>
		/// Test for load thumbnail function
		/// </summary>
		private void ButtonThumbnail_Click(object sender, EventArgs e)
		{
			try {
				using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
					openFileDialog.Filter = "WebP files (*.webp)|*.webp";
					openFileDialog.FileName = "";
					if (openFileDialog.ShowDialog() == DialogResult.OK) {
						this.pictureBox.Image = WebP.GetThumbnailQuality(File.ReadAllBytes(openFileDialog.FileName), 200, 150);
					}
				}
			} catch (Exception ex) {
				ErrorBox(ex, "ButtonThumbnail_Click");
			}
		}

		/// <summary>
		/// Test for advanced decode function
		/// </summary>
		private void ButtonCropFlip_Click(object sender, EventArgs e)
		{
			try {
				using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
					openFileDialog.Filter = "WebP files (*.webp)|*.webp";
					openFileDialog.FileName = "";
					if (openFileDialog.ShowDialog() == DialogResult.OK) {
						string pathFileName = openFileDialog.FileName;

						byte[] rawWebP = File.ReadAllBytes(pathFileName);
						WebPDecoderOptions decoderOptions = new WebPDecoderOptions
						{
							use_cropping = 1,
							crop_top = 10,              //Top beginning of crop area
							crop_left = 10,             //Left beginning of crop area
							crop_height = 250,          //Height of crop area
							crop_width = 300,           //Width of crop area
							use_threads = 1,            //Use multi-threading
							flip = 1                    //Flip the image
						};
						this.pictureBox.Image = WebP.Decode(rawWebP, decoderOptions);
					}
				}
			} catch (Exception ex) {
				ErrorBox(ex, "ButtonCropFlip_Click");
			}
		}

		/// <summary>
		/// Test encode functions
		/// </summary>
		private void ButtonSave_Click(object sender, EventArgs e)
		{
			if (this.pictureBox.Image == null) {
				MessageBox.Show("Please, load an image first.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
			} else {
				try {
					//get the picture box image
					Bitmap bmp = (Bitmap)pictureBox.Image;

					//Test simple encode in lossy mode in memory with quality 75
					var rawWebP = WebP.EncodeLossy(bmp, 75);
					SaveWithSummary(rawWebP, "SimpleLossy.webp", "Simple lossy (quality 75)");

					//Test simple encode in lossless mode in memory
					rawWebP = WebP.EncodeLossless(bmp);
					SaveWithSummary(rawWebP, "SimpleLossless.webp", "Simple lossless");

					//Test encode in lossy mode in memory with quality 75 and speed 9
					WebPAuxStats stats;
					using (WebP webp = new WebP()) {
						rawWebP = webp.EncodeLossy(bmp, 75, 9, out stats);
					}
					ShowCompressionStatistics(stats, bmp);
					SaveWithSummary(rawWebP, "AdvanceLossy.webp", "Advance lossy (quality 75, speed 9)");

					//Test advance encode lossless mode in memory with speed 9
					using (WebP webp = new WebP()) {
						rawWebP = webp.EncodeLossless(bmp, 9);
					}
					SaveWithSummary(rawWebP, "AdvanceLossless.webp", "Advance lossless");

					//Test encode near lossless mode in memory with quality 40 and speed 9
					// quality 100: No-loss (bit-stream same as -lossless).
					// quality 80: Very very high PSNR (around 54dB) and gets an additional 5-10% size reduction over WebP-lossless image.
					// quality 60: Very high PSNR (around 48dB) and gets an additional 20%-25% size reduction over WebP-lossless image.
					// quality 40: High PSNR (around 42dB) and gets an additional 30-35% size reduction over WebP-lossless image.
					// quality 20 (and below): Moderate PSNR (around 36dB) and gets an additional 40-50% size reduction over WebP-lossless image.
					using (WebP webp = new WebP()) {
						rawWebP = webp.EncodeNearLossless(bmp, 40, 9);
					}
					SaveWithSummary(rawWebP, "NearLossless.webp", "Near lossless (quality 40, speed 9)");

					MessageBox.Show("End of Test");
				} catch (Exception ex) {
					ErrorBox(ex, "ButtonSave_Click");
				}
			}
		}

		/// <summary>
		/// Test GetPictureDistortion function
		/// </summary>
		private void ButtonMeasure_Click(object sender, EventArgs e)
		{
			try {
				if (this.pictureBox.Image == null) {
					MessageBox.Show("Please, load an reference image first");
				}
				using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
					openFileDialog.Filter = "WebP images (*.webp)|*.webp";
					openFileDialog.FileName = "";
					if (openFileDialog.ShowDialog() == DialogResult.OK) {
						//Load Bitmaps
						var source = (Bitmap)this.pictureBox.Image;
						var reference = WebP.Load(openFileDialog.FileName);

						//Measure PSNR
						var result = WebP.GetPictureDistortion(source, reference, DistortionMetric.PeakSignalNoiseRatio);
						MessageBox.Show("Red: " + result[0] + "dB.\nGreen: " + result[1] + "dB.\nBlue: " + result[2] + "dB.\nAlpha: " + result[3] + "dB.\nAll: " + result[4] + "dB.", "PSNR");

						//Measure SSIM
						result = WebP.GetPictureDistortion(source, reference, DistortionMetric.StructuralSimilarity);
						MessageBox.Show("Red: " + result[0] + "dB.\nGreen: " + result[1] + "dB.\nBlue: " + result[2] + "dB.\nAlpha: " + result[3] + "dB.\nAll: " + result[4] + "dB.", "SSIM");

						//Measure LSIM
						result = WebP.GetPictureDistortion(source, reference, DistortionMetric.LightweightSimilarity);
						MessageBox.Show("Red: " + result[0] + "dB.\nGreen: " + result[1] + "dB.\nBlue: " + result[2] + "dB.\nAlpha: " + result[3] + "dB.\nAll: " + result[4] + "dB.", "LSIM");
					}
				}
			} catch (Exception ex) {
				ErrorBox(ex, "ButtonMeasure_Click");
			}
		}

		/// <summary>
		/// Test GetInfo function
		/// </summary>
		private void ButtonInfo_Click(object sender, EventArgs e)
		{
			var nl = Environment.NewLine;
			try {
				using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
					openFileDialog.Filter = "WebP images (*.webp)|*.webp";
					openFileDialog.FileName = "";
					if (openFileDialog.ShowDialog() == DialogResult.OK) {
						var info = WebP.GetInfo(File.ReadAllBytes(openFileDialog.FileName));
						MessageBox.Show("Width: " + info.Width + nl +
										"Height: " + info.Height + nl +
										"Has alpha: " + info.HasAlpha + nl +
										"Is animation: " + info.IsAnimated + nl +
										"Format: " + info.Format, "Information");
					}
				}
			} catch (Exception ex) {
				ErrorBox(ex, "ButtonInfo_Click");
			}
		}
		#endregion

		private static void ShowCompressionStatistics(WebPAuxStats stats, Bitmap gdiPlusImage)
		{
			MessageBox.Show("Dimensions: " + gdiPlusImage.Width + " x " + gdiPlusImage.Height + " pixels\n" +
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

		private static void ErrorBox(Exception ex, string methodName)
		{
			MessageBox.Show(ex.Message + Environment.NewLine + "In WebPExample." + methodName, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		private static void SaveWithSummary(byte[] rawWebP, string fileName, string caption)
		{
			string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
			File.WriteAllBytes(filePath, rawWebP);
			MessageBox.Show("Made " + filePath + " of " + rawWebP.Length + " bytes.", caption);
		}
	}
}
