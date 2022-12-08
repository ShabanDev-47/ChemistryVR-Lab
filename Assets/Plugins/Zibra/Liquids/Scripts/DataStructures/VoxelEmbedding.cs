using System;
using UnityEngine;

namespace com.zibra.liquid.DataStructures
{
    [Serializable]
    public struct VoxelEmbedding
    {
        public Color32[] embeds;
        public byte[] grid;
    }
}