using System.Collections.Generic;
using UnityEngine;

namespace ARCloset
{
    [CreateAssetMenu(menuName = "AR Closet/Garment Catalog", fileName = "GarmentCatalog")]
    public sealed class GarmentCatalog : ScriptableObject
    {
        public List<GarmentDefinition> garments = new();
    }
}
