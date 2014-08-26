using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using s4pi.GenericRCOLResource;

namespace S4Studio.Rendering
{
    public class SceneMesh
    {
        public SceneMesh(GeometryModel3D model)
        {
            Model = model;
            States = new SceneGeostate[0];
        }
        public BitmapImage Texture { get; set; }
        public SceneGeostate[] States { get; set; }
        public SceneGeostate SelectedState { get; set; }

        public GeometryModel3D Model { get; set; }
        public ShaderType Shader { get; set; }
    }
}