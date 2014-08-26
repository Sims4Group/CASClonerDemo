using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using HelixToolkit.Wpf;
using meshExpImp.ModelBlocks;
using s4pi.GenericRCOLResource;
using s4pi.ImageResource;
using s4pi.Interfaces;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Path = System.Windows.Shapes.Path;
using Point = System.Windows.Point;
using Vertex = meshExpImp.ModelBlocks.Vertex;

namespace S4Studio.Rendering
{
    /// <summary>
    /// Interaction logic for S4Studio3DPreviewControl.xaml
    /// </summary>
    public partial class S4Studio3DPreviewControl : UserControl
    {

        private List<SceneMesh> mSceneMeshes;
        private GenericRCOLResource rcol;
        private Material mHiddenMaterial = new DiffuseMaterial();
        private Material mXrayMaterial = new DiffuseMaterial();
        private MaterialGroup mNonSelectedMaterial = new MaterialGroup();
        private MaterialGroup mSelectedMaterial = new MaterialGroup();
        private MaterialGroup mGlassMaterial = new MaterialGroup();

        public static readonly DependencyProperty ViewportProperty = DependencyProperty.Register(
            "Viewport", typeof (HelixViewport3D), typeof (S4Studio3DPreviewControl), new PropertyMetadata(default(HelixViewport3D)));

        public HelixViewport3D Viewport
        {
            get { return (HelixViewport3D) GetValue(ViewportProperty); }
            set { SetValue(ViewportProperty, value); }
        }
        public Material MaterialFromBitmap(Bitmap texture)
        {
            var bmp = new BitmapImage();
            var memstream = new MemoryStream();
            using (var fs = new MemoryStream())
            {
                texture.Save(fs, ImageFormat.Png);
                fs.Position = 0L;

                byte[] buffer = new byte[fs.Length];
                fs.Read(buffer, 0, buffer.Length);
                memstream.Write(buffer, 0, buffer.Length);
            }
            bmp.BeginInit();
            bmp.StreamSource = memstream;
            bmp.EndInit();
            var img_brush = new ImageBrush(bmp);

            img_brush.Stretch = Stretch.Fill;
            img_brush.TileMode = TileMode.Tile;
            img_brush.ViewboxUnits = BrushMappingMode.RelativeToBoundingBox;
            img_brush.ViewportUnits = BrushMappingMode.Absolute;
            var material = new DiffuseMaterial(img_brush);
            return material;
        }

        public S4Studio3DPreviewControl()
        {
            InitializeComponent();
            this.Viewport = mainViewport;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            mSceneMeshes = new List<SceneMesh>();

            mNonSelectedMaterial.Children.Add(new DiffuseMaterial(Brushes.LightGray));
            mNonSelectedMaterial.Children.Add(new SpecularMaterial(Brushes.GhostWhite, 20d));
            mSelectedMaterial.Children.Add(new DiffuseMaterial(Brushes.Red));
            mSelectedMaterial.Children.Add(new SpecularMaterial(Brushes.Red, 40d));
            mXrayMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromScRgb(0.4f, 1f, 0f, 0f)));


            mGlassMaterial.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromScRgb(0.6f, .9f, .9f, 1f))));
            mGlassMaterial.Children.Add(new SpecularMaterial(Brushes.White, 100d));

            var shadowBrush = new ImageBrush
            {
                Stretch = Stretch.Fill,
                TileMode = TileMode.Tile,
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
                ViewportUnits = BrushMappingMode.Absolute,
                Transform = new ScaleTransform(1, 1)
            };

            try
            {
                shadowBrush.ImageSource = new BitmapImage(new Uri(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "dropShadow.png")));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unable to load DropShadow.");
            }
        }

        public IEnumerable<SceneMesh> AddObjectMesh(GenericRCOLResource rcol, Bitmap texture = null)
        {
            GenericRCOLResource.ChunkEntry chunk = rcol.ChunkEntries.FirstOrDefault(x => x.RCOLBlock is MLOD);
            return this.AddMesh(chunk.RCOLBlock as MLOD, texture);

        }
        public IEnumerable<SceneMesh> AddBodyMesh(GenericRCOLResource rcol, Bitmap texture = null)
        {

            GenericRCOLResource.ChunkEntry geomChunk = rcol.ChunkEntries.FirstOrDefault();
            var geom = new GEOM(0, null, geomChunk.RCOLBlock.Stream);
            return this.AddMesh(geom, texture);

        }

        public IEnumerable<SceneMesh> AddMesh(MLOD mlod, Bitmap texture = null)
        {
            var scene_material =(DiffuseMaterial) (texture == null ? this.mNonSelectedMaterial : MaterialFromBitmap(texture));
            foreach (MLOD.Mesh m in mlod.Meshes)
            {
                SceneMesh sceneMesh = null;
                try
                {
                    var vbuf = (VBUF)GenericRCOLResource.ChunkReference.GetBlock(rcol, m.VertexBufferIndex);
                    var ibuf = (IBUF)GenericRCOLResource.ChunkReference.GetBlock(rcol, m.IndexBufferIndex);
                    VRTF vrtf = (VRTF)GenericRCOLResource.ChunkReference.GetBlock(rcol, m.VertexFormatIndex) ?? VRTF.CreateDefaultForMesh(m);
                    IRCOLBlock material = GenericRCOLResource.ChunkReference.GetBlock(rcol, m.MaterialIndex);

                    MATD matd = FindMainMATD(rcol, material);

                    float[] uvscale = GetUvScales(matd);
                    if (uvscale != null)
                        Debug.WriteLine(string.Format("{0} - {1} - {2}", uvscale[0], uvscale[2], uvscale[2]));
                    else
                        Debug.WriteLine("No scales");
                    GeometryModel3D model = DrawModel(vbuf.GetVertices(m, vrtf, uvscale), ibuf.GetIndices(m), scene_material);
                    
                    sceneMesh = new SceneMlodMesh(m, model);
                    if (matd != null)
                    {
                        sceneMesh.Shader = matd.Shader;
                        switch (matd.Shader)
                        {
                            case ShaderType.ShadowMap:
                            case ShaderType.DropShadow:
                                break;
                            default:
                                var maskWidth = GetMATDParam<ElementInt>(matd, FieldType.MaskWidth);
                                var maskHeight = GetMATDParam<ElementInt>(matd, FieldType.MaskHeight);
                                if (maskWidth != null && maskHeight != null)
                                {
                                    float scalar = Math.Max(maskWidth.Data, maskHeight.Data);
                                    scene_material.Brush.Transform = new ScaleTransform(maskHeight.Data / scalar, maskWidth.Data / scalar);
                                }
                                break;
                        }
                    }
                    try
                    {
                        var sceneGeostates = new SceneGeostate[m.GeometryStates.Count];
                        for (int i = 0; i < sceneGeostates.Length; i++)
                        {
                            GeometryModel3D state = DrawModel(vbuf.GetVertices(m, vrtf, m.GeometryStates[i], uvscale),
                                                              ibuf.GetIndices(m, m.GeometryStates[i]), mHiddenMaterial);
                            mGroupMeshes.Children.Add(state);
                            sceneGeostates[i] = new SceneGeostate(sceneMesh, m.GeometryStates[i], state);
                        }
                        sceneMesh.States = sceneGeostates;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Unable to load Geostates.  You may have some corrupted data: " + ex.ToString(),
                                        "Unable to load Geostates...");
                    }
                    mGroupMeshes.Children.Add(model);
                    mSceneMeshes.Add(sceneMesh);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(String.Format("Unable to load mesh id 0x{0:X8}", m.Name));
                }

                yield return sceneMesh;
            }
        }

        public void ClearMeshes()
        {
            this.mSceneMeshes.Clear();
            this.mGroupMeshes.Children.Clear();

        }
        public IEnumerable<SceneMesh> AddMesh(GEOM geom, Bitmap texture = null)
        {
            var verts = new List<Vertex>();
            foreach (GEOM.VertexDataElement vd in geom.VertexData)
            {
                var v = new Vertex();

                var pos = (GEOM.PositionElement)vd.Vertex.FirstOrDefault(e => e is GEOM.PositionElement);
                if (pos != null)
                {
                    v.Position = new[] { pos.X, pos.Y, pos.Z };
                }


                var norm = (GEOM.NormalElement)vd.Vertex.FirstOrDefault(e => e is GEOM.NormalElement);
                if (norm != null)
                {
                    v.Normal = new[] { norm.X, norm.Y, norm.Z };
                }


                var uv = (GEOM.UVElement)vd.Vertex.FirstOrDefault(e => e is GEOM.UVElement);
                if (uv != null)
                {
                    v.UV = new[] { new[] { uv.U, uv.V } };
                }
                verts.Add(v);
            }
            var facepoints = new List<int>();
            foreach (GEOM.Face face in geom.Faces)
            {
                facepoints.Add(face.VertexDataIndex0);
                facepoints.Add(face.VertexDataIndex1);
                facepoints.Add(face.VertexDataIndex2);
            }
            var material = texture == null ? this.mNonSelectedMaterial : MaterialFromBitmap(texture);
            GeometryModel3D model = DrawModel(verts.ToArray(), facepoints.ToArray(), material);
            var sceneMesh = new SceneGeomMesh(geom, model);
            mGroupMeshes.Children.Add(model);
            mSceneMeshes.Add(sceneMesh);
            return new SceneMesh[] { sceneMesh };

        }

        private static MATD FindMainMATD(GenericRCOLResource rcol, IRCOLBlock material)
        {
            float[] scales = null;
            if (material == null) return null;
            if (material is MATD)
            {
                return material as MATD;
            }
            else if (material is MTST)
            {
                var mtst = material as MTST;
                try
                {
                    material = GenericRCOLResource.ChunkReference.GetBlock(rcol, mtst.Index);
                }
                catch (NotImplementedException e)
                {
                    MessageBox.Show("Material is external, unable to locate UV scales.");
                    return null;
                }


                if (material is MATD)
                {
                    var matd = (MATD)material;
                    return matd;
                }
            }
            else
            {
                throw new ArgumentException("Material must be of type MATD or MTST", "material");
            }

            return null;
        }

        private static T GetMATDParam<T>(MATD matd, FieldType type) where T : class
        {
            return matd == null ? null : (matd.Mtnf != null ? matd.Mtnf.SData : matd.Mtrl.SData).FirstOrDefault(x => x.Field == type) as T;
        }

        private static float[] GetUvScales(MATD matd)
        {
            var param = GetMATDParam<ElementFloat3>(matd, FieldType.UVScales);
            return param != null ? new[] { param.Data0, param.Data1, param.Data2 } : new[] { 1f / short.MaxValue, 1f / short.MaxValue, 1f / short.MaxValue };
        }

        private static GeometryModel3D DrawModel(Vertex[] verts, Int32[] indices, Material material)
        {
            var mesh = new MeshGeometry3D();
            for (int k = 0; k < verts.Length; k++)
            {
                Vertex v = verts[k];

                if (v.Position != null) mesh.Positions.Add(new Point3D(v.Position[0], -v.Position[2], v.Position[1]));
                if (v.Normal != null) mesh.Normals.Add(new Vector3D(v.Normal[0], v.Normal[1], v.Normal[2]));
                if (v.UV != null && v.UV.Length > 0) mesh.TextureCoordinates.Add(new Point(v.UV[0][0], v.UV[0][1]));
            }
            for (int i = 0; i < indices.Length; i++)
            {
                mesh.TriangleIndices.Add(indices[i]);
            }
            return new GeometryModel3D(mesh, material);
        }

    }
}
