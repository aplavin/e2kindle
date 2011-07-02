using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace e2Kindle
{
    public static class ImageUtils
    {
        #region ColorMatrix for grayscaling
        private static readonly ColorMatrix GrayscaleColorMatrix = new ColorMatrix(
            new[] 
            {
                new[] {.3f, .3f, .3f, 0, 0},
                new[] {.59f, .59f, .59f, 0, 0},
                new[] {.11f, .11f, .11f, 0, 0},
                new[] {0f, 0, 0, 1, 0},
                new[] {0f, 0, 0, 0, 1}
            });
        #endregion
        public static Bitmap Grayscale(Bitmap original)
        {
            if (original == null) throw new ArgumentNullException("original");

            //create a blank bitmap the same size as original
            var newBitmap = new Bitmap(original.Width, original.Height);

            using (var g = Graphics.FromImage(newBitmap))
            using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(GrayscaleColorMatrix);

                //draw the original image on the new image
                //using the grayscale color matrix
                g.DrawImage(
                    original,
                    new Rectangle(0, 0, original.Width, original.Height),
                    0, 0,
                    original.Width, original.Height,
                    GraphicsUnit.Pixel,
                    attributes);

                return newBitmap;
            }
        }

        public static Bitmap Shrink(Bitmap bitmap, int maxWidth, int maxHeight)
        {
            if (bitmap == null) throw new ArgumentNullException("bitmap");

            int sourceWidth = bitmap.Width;
            int sourceHeight = bitmap.Height;

            if (sourceWidth <= maxWidth && sourceHeight <= maxHeight)
                return bitmap;

            float widthRatio = 1f * maxWidth / sourceWidth;
            float heightRatio = 1f * maxHeight / sourceHeight;

            float ratio = Math.Min(heightRatio, widthRatio);

            int destWidth = (int)(sourceWidth * ratio);
            int destHeight = (int)(sourceHeight * ratio);

            using (var resultBitmap = new Bitmap(destWidth, destHeight))
            using (var g = Graphics.FromImage(resultBitmap))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(bitmap, 0, 0, destWidth, destHeight);

                return resultBitmap;
            }
        }

        #region Parameters for JPEG encoding
        private const int QUALITY = 25;
        private static readonly EncoderParameter QualityParameter = new EncoderParameter(Encoder.Quality, 1L);
        private static readonly EncoderParameters EncoderParameters = new EncoderParameters { Param = new[] { QualityParameter } };
        private static readonly ImageCodecInfo JpegEncoder = ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
        #endregion
        public static byte[] GetJpegData(Bitmap bitmap)
        {
            if (bitmap == null) throw new ArgumentNullException("bitmap");

            // Encoder parameter for image quality
            EncoderParameter qualityParam = new EncoderParameter(Encoder.Quality, 20L);

            // Jpeg image codec
            ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");

            EncoderParameters encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = qualityParam;

            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, jpegCodec, encoderParams);
                return stream.ToArray();
            }

            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, JpegEncoder, EncoderParameters);
                return stream.ToArray();
            }
        }


        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            // Get image codecs for all image formats
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

            // Find the correct image codec
            for (int i = 0; i < codecs.Length; i++)
                if (codecs[i].MimeType == mimeType)
                    return codecs[i];
            return null;
        }
    }
}
