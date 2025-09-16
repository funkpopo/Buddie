using System.IO;
using System.Windows.Media.Imaging;

namespace Buddie.Services
{
    public class ImageService : IImageService
    {
        public BitmapImage CreateBitmapImage(byte[] imageBytes, int? decodePixelWidth = null)
        {
            var bmp = new BitmapImage();
            using (var stream = new MemoryStream(imageBytes))
            {
                bmp.BeginInit();
                bmp.StreamSource = stream;
                if (decodePixelWidth.HasValue)
                {
                    bmp.DecodePixelWidth = decodePixelWidth.Value;
                }
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
            }
            return bmp;
        }

        public string ToBase64(byte[] imageBytes) => System.Convert.ToBase64String(imageBytes);
    }
}

