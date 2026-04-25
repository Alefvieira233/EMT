using System.IO;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using FerramentaEMT.Models.PF;

namespace FerramentaEMT.Services.PF
{
    internal static class PfRebarShapePreviewService
    {
        public static BitmapImage LoadPreview(Document doc, PfRebarShapeOption option, int sizePx = 220)
        {
            if (doc == null || option == null || option.IsAutomatic || option.ElementIdValue <= 0)
                return null;

            RebarShape shape = doc.GetElement(new ElementId(option.ElementIdValue)) as RebarShape;
            if (shape == null)
                return null;

            try
            {
                using (System.Drawing.Bitmap bitmap = shape.GetPreviewImage(new System.Drawing.Size(sizePx, sizePx)))
                {
                    if (bitmap == null)
                        return null;

                    using (MemoryStream stream = new MemoryStream())
                    {
                        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        stream.Position = 0;

                        BitmapImage image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                        image.Freeze();
                        return image;
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
