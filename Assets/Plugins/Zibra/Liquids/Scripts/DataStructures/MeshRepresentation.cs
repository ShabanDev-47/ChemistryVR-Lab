using System;

namespace com.zibra.liquid.DataStructures
{
    [Serializable]
    public class MeshRepresentation
    {
        public string faces;
        public string vertices;
        public int vox_dim;
        public int sdf_dim;
        public float cutoff_weight;
        public bool static_quantization;
    }
}