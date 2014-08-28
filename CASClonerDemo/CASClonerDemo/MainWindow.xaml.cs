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
using System.Reflection;
using s4pi.GenericRCOLResource;
using System.Drawing;

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
        private CASPItem selectedItem;
        private byte[] ddsData;
        private string caspItemName;

        public MainWindow()
        {
            InitializeComponent();
            this.SearchBox.InputFinished += SerachBox_KeyDown;
            
        }

        private void LoadAndCheckAssembly()
        {

            string currentPath = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase)).LocalPath;
            string[] assemblyName = new string[] { "s4pi.CASPartResource.dll", "s4pi.DefaultResource.dll", "s4pi.ImageResource.dll", "s4pi.WrapperDealer.dll" };
            foreach(string assembly in assemblyName)
            {
                Assembly.LoadFrom(Path.Combine(currentPath, assembly));
            }
            // Load Default Resource

            //if(AppDomain.CurrentDomain.GetAssemblies().Length != 32)
            //{
            //    MessageBox.Show("We tried to force the system to load all assemblies correctly, yet due to some unknown reasons the problem still can't be solved.");
            //}
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadAndCheckAssembly();

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
                List<CASPItem> caspList = new List<CASPItem>();

                var rlist = fullPack.GetResourceList.Where(tgi => tgi.ResourceType == 0x034AEECB).ToArray();
                foreach (var entry in rlist)
                {
                    caspList.Add(new CASPItem(WrapperDealer.GetResource(1, fullPack, entry).Stream, entry, thumPack));
                }
                this.caspCollection.Source = caspList;
                LoadImageFinished();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
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
                 this.caspItemName = userName + "_" + selectedCASP.Name + "_" + CloneEngine.GetTimestamp(DateTime.Now);
                 result = CloneEngine.CloneCAS(selectedCASP.CASP, this.fullPack, isReplace, this.caspItemName);

                 LoadMeshWithTexture();

             }));

            thread.Start();
            
            //string userName = Environment.UserName;
            //this.caspItemName = userName + "_" + selectedCASP.Name;
            //result = CloneEngine.CloneCAS(selectedCASP.CASP, this.fullPack, !isReplace , name: this.caspItemName);

        }


        private void LoadMeshWithTexture()
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                removeBodyMesh();
                Bitmap img = this.selectedItem.getBitmap(fullPack);
                var rlist = result.GetResourceList;
                var geom = result.GetResourceList.Any(tgi => tgi.ResourceType == 0x015A1849) ? (GenericRCOLResource)WrapperDealer.GetResource(0, result,
                    rlist.Where(x => x.ResourceType == 0x015A1849).OrderByDescending(x => x.Memsize).FirstOrDefault()) : null;
                if (geom == null) // might be a makeup or tatoo
                {
                    var names = caspItemName.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                    bool isMale = true;
                    if (names.Length > 1)
                    {
                        isMale = names[1][1] == 'm';
                    }
                    loadBodyMesh(isMale, img);
                    //this._3dPreview.AddBodyMesh(new GenericRCOLResource(1, null), img);
                }
                else
                {
                    LoadMeshWithTexture(geom, img);
                }
                //this.DDSPreviewBefore.Source = dds;
                //this.DDSPreviewAfter.Source = dds;
            }));
        }


        private void loadBodyMesh(bool isMale, Bitmap img)
        {

            var bodyTexture = (RLEResource)WrapperDealer.GetResource(0, fullPack, fullPack.GetResourceList.FirstOrDefault(x => x.ResourceType == 0x3453CF95 && x.Instance == 0xC7B7131033261079));
            var bodyTextureBitmap = CASPItem.getBitMapFromRLE(bodyTexture);
            List<Bitmap> images = new List<Bitmap>() { bodyTextureBitmap, img };
            var overlay = Texture.GetOverlay(images);
            var headMesh = (GenericRCOLResource)WrapperDealer.GetResource(0, fullPack,
                fullPack.GetResourceList.Where(x => x.ResourceType == 0x015A1849 && x.Instance == (isMale ? 0xc7b7131033261079U: 0x3e68f8b6f44da2aaU)).OrderByDescending(x => x.Memsize).FirstOrDefault());
            //var headDDS = 
            if (headMesh != null) this._3dPreview.AddBodyMesh(headMesh, overlay);

            var bodyTopMesh = (GenericRCOLResource)WrapperDealer.GetResource(0, fullPack,
                fullPack.GetResourceList.Where(x => x.ResourceType == 0x015A1849 && x.Instance == (isMale ? 0xfacb14f02cd72951U : 0x7379b6313337dbfaU)).OrderByDescending(x => x.Memsize).FirstOrDefault());

            if (bodyTopMesh != null) this._3dPreview.AddBodyMesh(bodyTopMesh, overlay);

            var bodyBottomMesh = (GenericRCOLResource)WrapperDealer.GetResource(0, fullPack,
                fullPack.GetResourceList.Where(x => x.ResourceType == 0x015A1849 && x.Instance == (isMale ? 0x1656f10b0b390821U: 0x562009b9d4fbc1c2U)).OrderByDescending(x => x.Memsize).FirstOrDefault());
            if (bodyBottomMesh != null) this._3dPreview.AddBodyMesh(bodyBottomMesh, overlay);

            var feetMesh = (GenericRCOLResource)WrapperDealer.GetResource(0, fullPack,
                fullPack.GetResourceList.Where(x => x.ResourceType == 0x015A1849 && x.Instance == (isMale ? 0x203835e631bb51e2U: 0xca169679e2cd5df1U)).OrderByDescending(x => x.Memsize).FirstOrDefault());
            if (feetMesh != null) this._3dPreview.AddBodyMesh(feetMesh, overlay);
        }

        private void LoadMeshWithTexture(GenericRCOLResource geom, Bitmap img)
        {
            var bodyTexture = (RLEResource)WrapperDealer.GetResource(0, fullPack, fullPack.GetResourceList.FirstOrDefault(x => x.ResourceType == 0x3453CF95 && x.Instance == 0xC7B7131033261079));
            var bodyTextureBitmap = CASPItem.getBitMapFromRLE(bodyTexture);
            List<Bitmap> images = new List<Bitmap>() { bodyTextureBitmap, img };
            var overlay = Texture.GetOverlay(images);
            this._3dPreview.AddBodyMesh(geom, overlay);
        }

        private void removeBodyMesh()
        {
            this._3dPreview.ClearMeshes();
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
                    try
                    {
                        BinaryReader r = new BinaryReader(fs);

                        this.ddsData = r.ReadBytes((int)fs.Length);
                        DDSImage dds = new DDSImage(this.ddsData);
                        var image = dds.images[0];
                        MemoryStream ms = new MemoryStream();


                        image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;
                        //this.DDSPreviewAfter.Dispatcher.Invoke(new Action(() =>
                        //{
                        //    this.DDSPreviewAfter.Source = CASPItem.getBitmapFromStream(ms);
                        //}));

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
                                    LoadMeshWithTexture();
                                }
                            }));


                            thread.Start();

                        }
                    }
                    catch(Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
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

        private void TextBlock_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start("http://www.den.simlogical.com/denforum/index.php?topic=3190");
        }

        private void GEOMExportButton_Click(object sender, RoutedEventArgs e)
        {
            var geomStream = WrapperDealer.GetResource(0, result,
                         result.GetResourceList.Where(x => x.ResourceType == 0x015A1849).OrderByDescending(x => x.Memsize).FirstOrDefault()).Stream;
            SaveFileDialog save = new SaveFileDialog() { Filter = "GEOM|*.simgeom" };
            if (save.ShowDialog() == true)
            {
                using (FileStream fs = new FileStream(save.FileName, FileMode.Create))
                {
                    geomStream.CopyTo(fs);
                }
            }

        }

        private void GEOMImportButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog() { Filter = "DXT5 Image|*.dds" };
            if (open.ShowDialog() == true)
            {
                using (FileStream fs = new FileStream(open.FileName, FileMode.Open))
                {
                    try
                    {
                        BinaryReader r = new BinaryReader(fs);
                       
                        if (this.result != null)
                        {
                            Thread thread = new Thread(new ThreadStart(() =>
                            {
                                var oldgeom =  result.GetResourceList.Where(x => x.ResourceType == 0x015A1849).OrderByDescending(x => x.Memsize).FirstOrDefault();
                                if(oldgeom != null)
                                {
                                    result.DeleteResource(oldgeom);
                                    result.AddResource(oldgeom, fs, true);
                                    LoadMeshWithTexture();
                                }
                            }));


                            thread.Start();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                }
            }
        }



    }
}
