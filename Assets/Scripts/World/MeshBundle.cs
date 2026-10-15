using System.Collections.Generic;
using UnityEngine;

namespace MotorAndSpeed.World
{
    /// <summary>
    /// 產生出來的網格容器。標成二進位存檔，檔案大小約是文字格式的一半。
    /// </summary>
    [PreferBinarySerialization]
    public class MeshBundle : ScriptableObject
    {
        public List<Mesh> Meshes = new List<Mesh>();
    }
}
