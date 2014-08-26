using System.Windows.Media.Media3D;
using meshExpImp.ModelBlocks;

namespace S4Studio.Rendering
{
    public class SceneMlodMesh : SceneMesh
    {
        public SceneMlodMesh(MLOD.Mesh mesh, GeometryModel3D model)
            : base(model)
        {
            Mesh = mesh;
        }

        public MLOD.Mesh Mesh { get; set; }

        public override string ToString()
        {
            string meshName = "0x" + Mesh.Name.ToString("X8");
//                if (MeshDictionary.ContainsKey(Mesh.Name))
//                {
//                    meshName = MeshDictionary[Mesh.Name];
//                }
            return meshName;
        }
    }
}