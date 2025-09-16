using System.Drawing;
using System.Threading.Tasks;
using System.Windows;

namespace Buddie.Services
{
    public interface IScreenService
    {
        Rectangle GetCurrentWindowScreen(Window? window);
        Rectangle GetPrimaryScreenBounds();
        Task<byte[]?> CaptureScreenAsync(Rectangle screenBounds);
    }
}

