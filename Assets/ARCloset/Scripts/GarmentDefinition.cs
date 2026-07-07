using UnityEngine;

namespace ARCloset
{
    [CreateAssetMenu(menuName = "AR Closet/Garment Definition", fileName = "GarmentDefinition")]
    public sealed class GarmentDefinition : ScriptableObject
    {
        public string garmentId = "garment-id";
        public string displayName = "Garment";
        public GarmentSlot slot = GarmentSlot.Upper;
        public string author;
        public string license;
        public string sourceUrl;
        public GameObject garmentPrefab;
        public Vector3 localPositionOffset;
        public Vector3 localEulerOffset;
        public Vector3 localScale = Vector3.one;
        public Vector2 fitAnchorOffset = Vector2.zero;
        public float fitWidthMultiplier = 1.0f;
        public float fitHeightMultiplier = 1.0f;
        public float fitVerticalBias = 0.0f;
    }
}
