using UnityEngine;

namespace ARCloset
{
    public sealed class PoseAnchorRig : MonoBehaviour
    {
        public Transform leftShoulder;
        public Transform rightShoulder;
        public Transform neck;
        public Transform hips;

        public Vector3 ChestCenter
        {
            get
            {
                if (leftShoulder == null || rightShoulder == null)
                {
                    return transform.position;
                }

                return (leftShoulder.position + rightShoulder.position) * 0.5f;
            }
        }

        public float ShoulderWidth
        {
            get
            {
                if (leftShoulder == null || rightShoulder == null)
                {
                    return 0f;
                }

                return Vector3.Distance(leftShoulder.position, rightShoulder.position);
            }
        }
    }
}
