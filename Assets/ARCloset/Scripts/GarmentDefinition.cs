using UnityEngine;

namespace ARCloset
{
    [CreateAssetMenu(menuName = "AR Closet/Garment Definition", fileName = "GarmentDefinition")]
    public sealed class GarmentDefinition : ScriptableObject
    {
        public string garmentId = "garment-id";
        public string displayName = "Garment";
        public GarmentSlot slot = GarmentSlot.Upper;
        public GameObject garmentPrefab;
        public Vector3 localPositionOffset;
        public Vector3 localEulerOffset;
        public Vector3 localScale = Vector3.one;
    }
}
