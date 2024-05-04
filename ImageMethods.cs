using System.Drawing;
using System.Windows.Media.Imaging;

namespace MagickCrop;
public class ImageMethods
{
    internal static RotateFlipType GetRotateFlipType(string path)
    {
        using Image img = Image.FromFile(path);
        RotateFlipType rotateFlipType = img.GetRotateFlipType();
        return rotateFlipType;
    }

    internal static void RotateImage(BitmapImage droppedImage, RotateFlipType rotateFlipType)
    {
        // Only consider basic rotation for now
        switch ((int)rotateFlipType)
        {
            case 1:
                droppedImage.Rotation = Rotation.Rotate90;
                break;
            case 2:
                droppedImage.Rotation = Rotation.Rotate180;
                break;
            case 3:
                droppedImage.Rotation = Rotation.Rotate270;
                break;
            default:
                break;
        }
    }
}
