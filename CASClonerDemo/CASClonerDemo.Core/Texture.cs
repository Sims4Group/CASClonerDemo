using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace CASClonerDemo.Core
{
    public static class Texture
    {
        public static Bitmap GetOverlay(List<Bitmap> bitmaps)
        {
            if (bitmaps.Count == 0) { return null; }
            Bitmap finalImage = new Bitmap(bitmaps[0].Width, bitmaps[0].Height);
            using (Graphics g = Graphics.FromImage(finalImage))
            {
                //set background color
                g.Clear(Color.Transparent);

                //go through each image and draw it on the final image (Notice the offset; since I want to overlay the images i won't have any offset between the images in the finalImage)
                int offset = 0;
                foreach (Bitmap image in bitmaps)
                {
                    g.DrawImage(image, new Rectangle(offset, 0, image.Width, image.Height));
                }
            }
            //Draw the final image in the pictureBox
            return finalImage;

        }
    }
}
