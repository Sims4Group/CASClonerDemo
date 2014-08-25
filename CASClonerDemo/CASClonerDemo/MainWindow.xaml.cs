using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using s4pi.Interfaces;
using s4pi.Package;
using CASClonerDemo.Core;
using Microsoft.Win32;
using s4pi.WrapperDealer;
using s4pi.ImageResource;
using System.IO;
using System.Threading;
using KUtility;
using System.Security.Cryptography;
using System.Diagnostics;

namespace CASClonerDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IPackage fullPack;
        private IPackage thumPack;
        private IPackage result;
        private System.Windows.Data.CollectionViewSource caspCollection;
        private bool isReplace = true;
        private CASPItem selectedItem;
        private byte[] ddsData;
        private string caspItemName;

        public MainWindow()
        {
            InitializeComponent();
            this.SearchBox.InputFinished += SerachBox_KeyDown;
            
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {


            string fullbuildPath = TS4Registry.CASDemoFullBuildPath;
            string thumbnailPath = TS4Registry.CASDemoThumPath;

            if (string.IsNullOrEmpty(fullbuildPath))
            {
                OpenFileDialog open = new OpenFileDialog() { Filter = "DBPF Package File|*.package", Multiselect = false, Title = "Please select your FullBuildFolder" };
                if (open.ShowDialog() == true)
                {
                    fullbuildPath = open.FileName;
                    string rootDir = System.IO.Path.GetDirectoryName(fullbuildPath);
                    thumbnailPath = System.IO.Path.Combine(rootDir, "CASDemoThumbnails.package");
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
            catch
            {
                //MessageBox.Show(ex.Message);
                Environment.ExitCode = 0;
                Application.Current.Shutdown();
                return;
            }

            this.caspCollection = new CollectionViewSource();
            List<CASPItem> caspList  = new List<CASPItem>();

            var rlist = fullPack.GetResourceList.Where(tgi => tgi.ResourceType == 0x034AEECB).ToArray();
            foreach (var entry in rlist)
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

        private void SerachBox_KeyDown(object sender, EventArgs e)
        {
            this.caspCollection.Dispatcher.Invoke(new Action(() =>
            {
                this.caspCollection.View.Filter = item =>
                    {
                        CASPItem casp = item as CASPItem;
                        string name = casp.Name.ToLower();
                        var searchItems = SearchBox.Text.ToLower().Split(new char[] { ' ', '+', ',' });
                        foreach (var str in searchItems)
                        {
                            if (!name.Contains(str)) return false;
                        }
                        return true;
                    };
            }));
        }

        private void CASPItemListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.selectedItem != null)
            {
                this.selectedItem.Clear(); // clear old data;
            }
            if (this.CASPItemListView.SelectedItem != null)
                this.selectedItem = (CASPItem) this.CASPItemListView.SelectedItem;
            if (this.selectedItem == null)
                return;
            CASPItem selectedCASP = this.selectedItem as CASPItem;
            Debug.WriteLine("0x" + selectedCASP.Instance.ToString("X8"));
            bool isReplace = this.ckbReplacement.IsChecked == true;
            Thread thread = new Thread(new ThreadStart(() =>
             {
                 string userName = Environment.UserName;
                 this.caspItemName = userName + "_" + selectedCASP.Name;
                 result = CloneEngine.CloneCAS(selectedCASP.CASP, this.fullPack, isReplace, this.caspItemName);

                 this.Dispatcher.Invoke(new Action(() =>
                 {
                     BitmapImage dds = this.selectedItem.getBitmap(fullPack);
                     this.DDSPreviewBefore.Source = dds;
                     this.DDSPreviewAfter.Source = dds;
                 }));

             }));

            thread.Start();
            
            //string userName = Environment.UserName;
            //this.caspItemName = userName + "_" + selectedCASP.Name;
            //result = CloneEngine.CloneCAS(selectedCASP.CASP, this.fullPack, !isReplace , name: this.caspItemName);


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
                    this.DDSPreviewAfter.Dispatcher.Invoke(new Action(() =>
                    {
                        this.DDSPreviewAfter.Source = CASPItem.getBitmapFromStream(ms);
                    }));

                    // replace the DDS RLE image
                    if (this.result != null)
                    {
                        Thread thread = new Thread(new ThreadStart(() =>
                        {
                            using (MemoryStream ms2 = new MemoryStream(this.ddsData))
                            {
                                ms2.Position = 0;
                                var rle = new RLEResource(1, null);
                                rle.ImportToRLE(ms2);
                                var rleInstance = result.Find(tgi => tgi.Instance == FNV64.GetHash(caspItemName));
                                result.DeleteResource(rleInstance);
                                result.AddResource(rleInstance, rle.Stream, true);
                            }
                        }));


                        thread.Start();
                        //using (MemoryStream ms2 = new MemoryStream(this.ddsData))
                        //{
                        //    ms2.Position = 0;
                        //    var rle = new RLEResource(1, null);
                        //    rle.ImportToRLE(ms2);
                        //    var rleInstance = result.Find(tgi => tgi.Instance == FNV64.GetHash(caspItemName));
                        //    result.DeleteResource(rleInstance);
                        //    result.AddResource(rleInstance, rle.Stream, true);
                        //}
                    }
                }
            }
        }

        private void Wizard_Finished(object sender, RoutedEventArgs e)
        {
            SaveFileDialog save = new SaveFileDialog() { Filter = "DBPF Package|*.package" };
            if(save.ShowDialog() == true && result != null)
            {
                result.SaveAs(save.FileName);
                MessageBox.Show("The package has been saved!");
                this.Close();
            }
        }

        private void Wizard_Cancelled(object sender, RoutedEventArgs e)
        {
            this.Close();
        }



    }
}
