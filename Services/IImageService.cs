using System.Windows.Media.Imaging;

namespace Buddie.Services
{
    public interface IImageService
    {
        BitmapImage CreateBitmapImage(byte[] imageBytes, int? decodePixelWidth = null);
        string ToBase64(byte[] imageBytes);
    }
}

