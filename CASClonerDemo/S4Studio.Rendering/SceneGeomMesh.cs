using System.Windows.Documents;
using System.Windows.Media.Media3D;
using meshExpImp.ModelBlocks;

namespace S4Studio.Rendering
{
    public class SceneGeomMesh : SceneMesh
    {
        public SceneGeomMesh(GEOM mesh, GeometryModel3D model)
            : base(model)
        {
            Mesh = mesh;
        }

        public GEOM Mesh { get; set; }

        public override string ToString()
        {
            return "GEOM Mesh";
        }
    }
}