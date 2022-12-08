using System;

namespace com.zibra.liquid.DataStructures
{
    [Serializable]
    public class ObjectTransform
    {
        public string Q;
        public string T;
        public string S;
    }

    [Serializable]
    public class VoxelRepresentation
    {
        public string embeds;
        public string sd_grid;
        public ObjectTransform transform;
    }

    [Serializable]
    public class MultiVoxelRepresentation
    {
        public VoxelRepresentation[] meshes_data;
    }
}