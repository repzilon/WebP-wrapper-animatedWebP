# WebP-wrapper
Wrapper for libwebp in C#. The most complete wrapper in pure managed C#.

Exposes Simple Decoding and Encoding API, Advanced Decoding and Encoding API (with compression statistics), Get version library and WebPGetFeatures (info on any WebP file). Exposed get PSNR, SSIM or LSIM distortion metrics.

It also provides methods for handling animated WebP files.

The wrapper is in safe managed code in one class. No need for external dll except libwebp (included v1.3.0). The wrapper works in 32, 64 bit or ANY (auto switch to the appropriate library).

The code is commented and includes simple examples for using the wrapper.

## Decompress Functions:
Load WebP image for WebP file
```C#
Bitmap bmp = WebP.Load("test.webp");
```

Decode WebP filename to bitmap and load in PictureBox container
```C#
byte[] rawWebP = File.ReadAllBytes("test.webp");
this.pictureBox.Image = WebP.Decode(rawWebP);
```

Advanced decode WebP filename to bitmap and load in PictureBox container
```C#
byte[] rawWebP = File.ReadAllBytes("test.webp");
WebPDecoderOptions decoderOptions = new WebPDecoderOptions();
decoderOptions.use_threads = 1;     //Use multhreading
decoderOptions.flip = 1;   			//Flip the image
this.pictureBox.Image = WebP.Decode(rawWebP, decoderOptions);
```

Get thumbnail with 200x150 pixels in fast/low quality mode
```C#
this.pictureBox.Image = WebP.GetThumbnailFast(rawWebP, 200, 150);
```

Get thumbnail with 200x150 pixels in slow/high quality mode
```C#
this.pictureBox.Image = WebP.GetThumbnailQuality(rawWebP, 200, 150);
```


## Compress Functions:
Save bitmap to WebP file
```C#
webp.Save(bmp, 80, "test.webp");
```

Encode to memory buffer in lossy mode with quality 75 and save to file
```C#
byte[] rawWebP = File.ReadAllBytes("test.jpg");
rawWebP = WebP.EncodeLossy(bmp, 75);
File.WriteAllBytes("test.webp", rawWebP); 
```

Encode to memory buffer in lossy mode with quality 75 and speed 9. Save to file
```C#
byte[] rawWebP = File.ReadAllBytes("test.jpg");
using (WebP webp = new WebP())
  rawWebP = webp.EncodeLossy(bmp, 75, 9);
File.WriteAllBytes("test.webp", rawWebP); 
```

Encode to memory buffer in lossy mode with quality 75, speed 9 and get information. Save to file
```C#
byte[] rawWebP = File.ReadAllBytes("test.jpg");
WebPAuxStats stats;
using (WebP webp = new WebP())
  rawWebP = webp.EncodeLossy(bmp, 75, 9, out stats);
File.WriteAllBytes("test.webp", rawWebP); 
```

Encode to memory buffer in lossless mode and save to file
```C#
byte[] rawWebP = File.ReadAllBytes("test.jpg");
rawWebP = WebP.EncodeLossless(bmp);
File.WriteAllBytes("test.webp", rawWebP); 
```

Encode to memory buffer in lossless mode with speed 9 and save to file
```C#
byte[] rawWebP = File.ReadAllBytes("test.jpg");
using (WebP webp = new WebP())
  rawWebP = webp.EncodeLossless(bmp, 9);
File.WriteAllBytes("test.webp", rawWebP); 
```

Encode to memory buffer in near lossless mode with quality 40 and speed 9 and save to file
```C#
byte[] rawWebP = File.ReadAllBytes("test.jpg");
using (WebP webp = new WebP())
  rawWebP = webp.EncodeNearLossless(bmp, 40, 9);
File.WriteAllBytes("test.webp", rawWebP); 
```

## Animated WebP Functions:

Load frames of an animated WebP file
```C#
var frames = webp.AnimLoad(strFilepath);
// or
var frames = webp.AnimDecode(byteArray);
```

Support for optional frame ranges (by indices) when decoding animated WebP files (see `webp.AnimDecode(..)`)
```C#
var frames = webp.AnimDecode(byteArray, 0, 1);
```

Get animated WebP's (compressed) frame data
```C#
_ = webp.AnimInit(strFilepath, out uint frameCount);
var frameData = webp.AnimGetFrame(intFrameNumber);
```

and decode at a later time
```C#
var bmp = webp.Decode(frameData.Data);
```

## Animated WebP Functions:

Load frames of an animated WebP file
```C#
var frames = webp.AnimLoad(strFilepath);
// or
var frames = webp.AnimDecode(byteArray);
```

Support for optional frame ranges (by indices) when decoding animated WebP files (see `webp.AnimDecode(..)`)
```C#
var frames = webp.AnimDecode(byteArray, 0, 1);
```

Get animated WebP's (compressed) frame data
```C#
_ = webp.AnimInit(strFilepath, out uint frameCount);
var frameData = webp.AnimGetFrame(intFrameNumber);
```

and decode at a later time
```C#
var bmp = webp.Decode(frameData.Data);
```

## Other Functions:	
Get version of libwebp.dll
```C#
string version = "libwebp.dll v" + WebP.GetVersion();
```

Get info from WebP file
```C#
byte[] rawWebp = File.ReadAllBytes(pathFileName);
var info = WebP.GetInfo(pathFileName);
MessageBox.Show("Width: " + info.Width + "\n" +
				"Height: " + info.Height + "\n" +
				"Has alpha: " + info.HasAlpha + "\n" +
				"Is animation: " + info.IsAnimated + "\n" +
				"Format: " + info.Format);
```

Get PSNR, SSIM or LSIM distortion metric between two pictures
```C#
int metric = 0;  //0 = PSNR, 1= SSIM, 2=LSIM
Bitmap bmp1 = Bitmap.FromFile("image1.png");
Bitmap bmp2 = Bitmap.FromFile("image2.png");
var result = WebP.GetPictureDistortion(source, reference, metric);
MessageBox.Show("Red: " + result[0] + dB\n" +
                "Green: " + result[1] + "dB\n" +
                "Blue: " + result[2] + "dB\n" +
                "Alpha: " + result[3] + "dB\n" +
                "All: " + result[4] + "dB\n");
```


## Thanks to jzern@google.com
Without his help this wrapper would not have been possible.


[1]: https://github.com/thomas694/WebP-wrapper-animatedWebP
