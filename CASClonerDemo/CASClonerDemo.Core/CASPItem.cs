﻿using System;
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
using System.Drawing;

namespace CASClonerDemo.Core
{
    public class CASPItem
    {
        public CASPartResource.CASPartResourceTS4 CASP { get; set; }
        RLEResource rle;
        private IResourceIndexEntry entry;
        
        IPackage pack;

        public CASPItem(Stream s, IResourceIndexEntry entry, IPackage pack) 
        { 
            this.CASP = new CASPartResource.CASPartResourceTS4(1, s);
            this.pack = pack;
            this.entry = entry;
            this.ResourceGroup = entry.ResourceGroup;
            this.Instance = entry.Instance;
        }

        public string Name { get { return CASP.Name; } set { CASP.Name = value; } }
        private BitmapImage thumbnail_image;
        public BitmapImage ThumbnailImage {
            get
            {
                if (this.thumbnail_image == null)
                {
                    var thumb = pack.Find(tgi => GetThumbnail(tgi, entry.Instance));
                    this.thumbnail_image = new BitmapImage();
                    this.thumbnail_image.BeginInit();
                    Stream thumbnailStream;
                    if (thumb != null)
                    {
                        s4pi.ImageResource.ThumbnailResource thumbnail = new s4pi.ImageResource.ThumbnailResource(1, WrapperDealer.GetResource(1, pack, thumb).Stream);
                        thumbnail.TransformToPNG();
                        thumbnailStream = thumbnail.ToImageStream();
                    }
                    else
                    {
                        thumbnailStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(@"CASClonerDemo.Core.Bad.bmp");
                    }
                    //Stream thumbnailStream = (thumb == null) ? Assembly.GetExecutingAssembly().GetManifestResourceStream(@"CASClonerDemo.Core.Bad.bmp") : (new s4pi.ImageResource.ThumbnailResource(1, WrapperDealer.GetResource(1, pack, thumb).Stream).ToImageStream());
                    this.thumbnail_image.StreamSource = thumbnailStream;
                    this.thumbnail_image.EndInit();
                    
                }
                return this.thumbnail_image;
            }
        }
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
        public Stream UnParse() { return this.CASP.Stream; }
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

        public Bitmap getBitmap(IPackage pack)
        {

            ulong instance = FNV64.GetHash(this.Name);

            var rleEntry = pack.Find(item => item.Instance == instance && item.ResourceType == 0x3453CF95);
            if (rleEntry == null) return null;
            

            using(MemoryStream ms = WrapperDealer.GetResource(1, pack, rleEntry).Stream as MemoryStream)
            {
                rle = new RLEResource(1, ms);
            }

            var dds = new KUtility.DDSImage((rle.ToDDS() as MemoryStream).ToArray());
            //MemoryStream imageStream = new MemoryStream();
            //dds.images[0].Save(imageStream,System.Drawing.Imaging.ImageFormat.Png);
            //return getBitmapFromStream(imageStream);
            return dds.images[0];
        }

        public static Bitmap getBitMapFromRLE(RLEResource rle)
        {
            var dds = new KUtility.DDSImage((rle.ToDDS() as MemoryStream).ToArray());
            return dds.images[0];
        }


       

        //public static BitmapImage getBitmapFromStream(Stream s)
        //{
        //    var image = new BitmapImage();
        //    image.BeginInit();
        //    image.StreamSource = s;
        //    image.EndInit();
        //    return image;
        //}

        public void Clear()
        {
            this.rle = null;
        }
    }
}
