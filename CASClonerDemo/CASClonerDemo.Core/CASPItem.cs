using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using s4pi.ImageResource;
using s4pi.Interfaces;
using System.Security.Cryptography;
using System.IO;
using s4pi.WrapperDealer;
using System.Windows.Media.Imaging;
using System.Reflection;
using System.Windows.Media;

namespace CASClonerDemo.Core
{
    public class CASPItem
    {
        CASPartResource.CASPartResourceTS4 casp;
        RLEResource rle;
        
        public CASPItem(Stream s, IResourceIndexEntry entry, IPackage pack) 
        { 
            this.casp = new CASPartResource.CASPartResourceTS4(1, s);
            var thumb= pack.Find(tgi => GetThumbnail(tgi, entry.Instance));
            ThumbnailImage = new BitmapImage();
            ThumbnailImage.BeginInit();
            Stream thumbnailStream = (thumb == null) ? Assembly.GetExecutingAssembly().GetManifestResourceStream(@"CASClonerDemo.Core.Bad.bmp") : (new s4pi.ImageResource.ThumbnailResource(1, WrapperDealer.GetResource(1, pack, thumb).Stream).ToImageStream());
            ThumbnailImage.StreamSource = thumbnailStream;
            ThumbnailImage.EndInit();

            this.ResourceGroup = entry.ResourceGroup;
            this.Instance = entry.Instance;
        }

        public string Name { get { return casp.Name; } set { casp.Name = value; } }
        public BitmapImage ThumbnailImage { get; private set; }
        public IResourceKey RLETGI
        {
            get
            {
                return new TGIBlock(1, null, 0x3453CF95U, 0U, FNV64.GetHash(this.Name));
            }
        }

        public void LoadDDS(Stream s) { this.rle.ImportToRLE(s); }
        public Stream ExportDDS() { return this.rle.ToDDS(); }
        public ulong Instance { get; set; }
        public uint ResourceGroup { get; set; }
        public uint ResourceType { get { return 0x034AEECB; } }
        public Stream UnParse() { return this.casp.Stream; }
        public override string ToString()
        {
            return this.Name;
        }

        private static bool GetThumbnail(IResourceIndexEntry entry, ulong instance)
        {
            if (entry.Instance != instance) return false;
            if (entry.ResourceType != 0x3C1AF1F2 && entry.ResourceType != 0x5B282D45 && entry.ResourceType != 0xCD9DE247) return false;
            if ((entry.ResourceGroup & 0x2) == 0) return false;
            return true;
        }

        public BitmapImage getBitmap(IPackage pack)
        {

            ulong instance = FNV64.GetHash(this.Name);

            var rleEntry = pack.Find(item => item.Instance == instance && item.ResourceType == 0x3453CF95);
            if (rleEntry == null) return null;
            

            using(MemoryStream ms = WrapperDealer.GetResource(1, pack, rleEntry).Stream as MemoryStream)
            {
                rle = new RLEResource(1, ms);
            }

            var dds = new KUtility.DDSImage((rle.ToDDS() as MemoryStream).ToArray());
            MemoryStream imageStream = new MemoryStream();
            dds.images[0].Save(imageStream,System.Drawing.Imaging.ImageFormat.Png);
            return getBitmapFromStream(imageStream);
        }

        public static BitmapImage getBitmapFromStream(Stream s)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = s;
            image.EndInit();
            return image;
        }

        public void Clear()
        {
            this.rle = null;
        }
    }
}
