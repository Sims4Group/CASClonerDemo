using System.Windows.Media.Media3D;
using meshExpImp.ModelBlocks;

namespace S4Studio.Rendering
{
    public class SceneGeostate
    {
        public SceneGeostate(SceneMesh owner, MLOD.GeometryState state, GeometryModel3D model)
        {
            Owner = owner;
            State = state;
            Model = model;
        }

        public SceneMesh Owner { get; set; }
        public MLOD.GeometryState State { get; set; }
        public GeometryModel3D Model { get; set; }

        public override string ToString()
        {
            if (State == null)
            {
                return "None";
            }
            else
            {
                string stateName = "0x" + State.Name.ToString("X8");
//                    if (GeostateDictionary.ContainsKey(State.Name))
//                    {
//                        stateName = GeostateDictionary[State.Name];
//                    }
                return stateName;
            }
        }
    }
}