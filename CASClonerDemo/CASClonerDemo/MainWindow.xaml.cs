using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using s4pi.Interfaces;
using s4pi.Package;
using s4pi.ImageResource;
using CASClonerDemo.Core;
using Microsoft.Win32;
using s4pi.WrapperDealer;
using System.IO;
using System.Threading;
using KUtility;
using System.Drawing;
using System.Security.Cryptography;

namespace CASClonerDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IPackage fullPack;
        private IPackage thumPack;
        private System.Windows.Data.CollectionViewSource caspCollection;
        private bool isReplace = true;
        private CASPItem selectedItem;
        private byte[] ddsData;

        public MainWindow()
        {
            InitializeComponent();
            
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {


            string fullbuildPath = TS4Registry.CASDemoFullBuildPath;
            string thumbnailPath = TS4Registry.CASDemoThumPath;

            if (string.IsNullOrEmpty(fullbuildPath))
            {
                OpenFileDialog open = new OpenFileDialog() { Filter = "DBPF Package File|*.package", Multiselect = false };
                if (open.ShowDialog() == true)
                {
                    fullbuildPath = open.FileName;
                }
                else
                {
                    Environment.ExitCode = 0;
                    this.Close();
                }
            }
            try
            {
                fullPack = Package.OpenPackage(1, fullbuildPath, false);
                thumPack = Package.OpenPackage(1, thumbnailPath, false);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            this.caspCollection = new CollectionViewSource();
            List<CASPItem> caspList  = new List<CASPItem>();

            foreach (var entry in fullPack.GetResourceList.Where(tgi => tgi.ResourceType == 0x034AEECB))
            {
                caspList.Add(new CASPItem(WrapperDealer.GetResource(1, fullPack, entry).Stream, entry, thumPack));
            }

            this.caspCollection.Source = caspList;



            LoadImageFinished();
        }

        
        private void LoadImageFinished()
        {
            this.CASPItemListView.Dispatcher.Invoke((Action)(() =>
            {
                this.CASPItemListView.ItemsSource = caspCollection.View;
            }));
        }

        private void SerachBox_KeyUp(object sender, KeyEventArgs e)
        {
            this.caspCollection.View.Filter = item =>
                {
                    CASPItem casp = item as CASPItem;
                    return casp.Name.ToLower().Contains(SerachBox.Text.ToLower());
                };
        }

        private void CASPItemListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.selectedItem != null)
            {
                this.selectedItem.Clear(); // clear old data;
            }
            this.selectedItem = this.CASPItemListView.SelectedItem as CASPItem;
            BitmapImage dds = this.selectedItem.getBitmap(fullPack);
            this.DDSPreviewBefore.Source = dds;
            this.DDSPreviewAfter.Source = dds;
        }

        private void DDSExportButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog save = new SaveFileDialog() { Filter = "DXT5 Image|*.dds" };
            if(save.ShowDialog() == true)
            {
                using(FileStream fs = new FileStream(save.FileName, FileMode.Create))
                {
                    selectedItem.ExportDDS().CopyTo(fs);
                }
            }
        }

        private void DDSImportButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog() { Filter = "DXT5 Image|*.dds" };
            if (open.ShowDialog() == true)
            {
                using (FileStream fs = new FileStream(open.FileName, FileMode.Open))
                {
                    BinaryReader r = new BinaryReader(fs);
                    this.ddsData = r.ReadBytes((int)fs.Length);
                    DDSImage dds = new DDSImage(this.ddsData);
                    var image = dds.images[0];
                    MemoryStream ms = new MemoryStream();

                    image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    this.DDSPreviewAfter.Source = CASPItem.getBitmapFromStream(ms);

                }
            }
        }

        private void Wizard_Finished(object sender, RoutedEventArgs e)
        {
            SaveFileDialog save = new SaveFileDialog() { Filter = "DBPF Package|*.package" };
            if(save.ShowDialog() == true)
            {
                //if (!isReplace)
                this.selectedItem.Name = isReplace ? this.selectedItem.Name: System.IO.Path.GetFileNameWithoutExtension(save.FileName);
                this.selectedItem.Instance = isReplace ? this.selectedItem.Instance : FNV64.GetHash(this.selectedItem.Name + "NEW");
                using(IPackage newPack = Package.NewPackage(0))
                {
                    newPack.AddResource(new TGIBlock(1, null, this.selectedItem.ResourceType, this.selectedItem.ResourceGroup, this.selectedItem.Instance), this.selectedItem.UnParse(), true); // add casp
                    RLEResource rle = new RLEResource(1, null);
                    rle.ImportToRLE(new MemoryStream(this.ddsData));
                    newPack.AddResource(new TGIBlock(1, null, 0x3453CF95, 0x0, FNV64.GetHash(this.selectedItem.Name)), rle.Stream, true); // add rle
                    newPack.SaveAs(save.FileName);
                }
            }
        }

        private void Wizard_Cancelled(object sender, RoutedEventArgs e)
        {
            this.Close();
        }



    }
}
