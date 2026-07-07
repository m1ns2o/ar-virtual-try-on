using System.Collections.Generic;
using UnityEngine;

namespace ARCloset
{
    public sealed class GarmentRuntimeRig : MonoBehaviour
    {
        [SerializeField] private GarmentSlot slot = GarmentSlot.Upper;
        [SerializeField, Range(0f, 1f)] private float deformationBlend = 0.22f;
        [SerializeField, Range(0f, 1f)] private float poseSmoothing = 0.18f;
        [SerializeField, Range(0f, 1f)] private float zInfluence = 0.03f;
        [SerializeField, Range(0f, 1f)] private float limbDeformationWeight = 0.38f;
        [SerializeField, Range(0.5f, 1f)] private float minSegmentStretch = 0.82f;
        [SerializeField, Range(1f, 1.8f)] private float maxSegmentStretch = 1.16f;
        [SerializeField] private bool updateNormals = true;

        private readonly List<RiggedMesh> riggedMeshes = new();
        private Bounds restBoundsRoot;
        private RestFrame restFrame;
        private TargetFrame smoothedTargetFrame;
        private bool initialized;
        private bool hasSmoothedPose;

        public void Configure(GarmentSlot garmentSlot)
        {
            slot = garmentSlot;
            hasSmoothedPose = false;
        }

        public void ApplyPose(Pose pose)
        {
            if (!pose.HasUpperBody)
            {
                ResetToRest();
                return;
            }

            EnsureInitialized();
            if (!initialized)
            {
                return;
            }

            TargetFrame targetFrame = BuildTargetFrame(pose);
            if (!hasSmoothedPose)
            {
                smoothedTargetFrame = targetFrame;
                hasSmoothedPose = true;
            }
            else
            {
                smoothedTargetFrame = TargetFrame.Lerp(smoothedTargetFrame, targetFrame, Mathf.Clamp01(poseSmoothing));
            }

            foreach (RiggedMesh riggedMesh in riggedMeshes)
            {
                riggedMesh.Apply(smoothedTargetFrame, deformationBlend, updateNormals);
            }
        }

        public void ResetToRest()
        {
            if (!initialized)
            {
                return;
            }

            hasSmoothedPose = false;
            foreach (RiggedMesh riggedMesh in riggedMeshes)
            {
                riggedMesh.ResetToRest(updateNormals);
            }
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
            bool hasBounds = false;

            foreach (MeshFilter filter in filters)
            {
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                {
                    continue;
                }

                Mesh runtimeMesh = Instantiate(filter.sharedMesh);
                runtimeMesh.name = filter.sharedMesh.name + "_RuntimeRig";
                runtimeMesh.MarkDynamic();
                filter.sharedMesh = runtimeMesh;

                RiggedMesh riggedMesh = new RiggedMesh(this, filter, runtimeMesh);
                riggedMeshes.Add(riggedMesh);

                foreach (Vector3 point in riggedMesh.RestRootVertices)
                {
                    if (!hasBounds)
                    {
                        restBoundsRoot = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        restBoundsRoot.Encapsulate(point);
                    }
                }
            }

            if (!hasBounds)
            {
                return;
            }

            restFrame = RestFrame.FromBounds(restBoundsRoot, slot);
            foreach (RiggedMesh riggedMesh in riggedMeshes)
            {
                riggedMesh.Bind(restFrame, slot, limbDeformationWeight);
            }

            initialized = true;
        }

        private TargetFrame BuildTargetFrame(Pose pose)
        {
            Vector3 leftShoulder = ToRootLocal(pose.LeftShoulderWorld, restFrame.LeftShoulder);
            Vector3 rightShoulder = ToRootLocal(pose.RightShoulderWorld, restFrame.RightShoulder);
            Vector3 shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;

            Vector3 leftHip = pose.HasLeftHip ? ToRootLocal(pose.LeftHipWorld, restFrame.LeftHip) : restFrame.LeftHip + shoulderCenter - restFrame.ShoulderCenter;
            Vector3 rightHip = pose.HasRightHip ? ToRootLocal(pose.RightHipWorld, restFrame.RightHip) : restFrame.RightHip + shoulderCenter - restFrame.ShoulderCenter;
            Vector3 hipCenter = (leftHip + rightHip) * 0.5f;

            Vector3 leftElbow = pose.HasLeftElbow ? ToRootLocal(pose.LeftElbowWorld, restFrame.LeftElbow) : PredictLimbPoint(leftShoulder, restFrame.LeftShoulder, restFrame.LeftElbow);
            Vector3 rightElbow = pose.HasRightElbow ? ToRootLocal(pose.RightElbowWorld, restFrame.RightElbow) : PredictLimbPoint(rightShoulder, restFrame.RightShoulder, restFrame.RightElbow);
            Vector3 leftWrist = pose.HasLeftWrist ? ToRootLocal(pose.LeftWristWorld, restFrame.LeftWrist) : PredictLimbPoint(leftElbow, restFrame.LeftElbow, restFrame.LeftWrist);
            Vector3 rightWrist = pose.HasRightWrist ? ToRootLocal(pose.RightWristWorld, restFrame.RightWrist) : PredictLimbPoint(rightElbow, restFrame.RightElbow, restFrame.RightWrist);

            Vector3 leftKnee = pose.HasLeftKnee ? ToRootLocal(pose.LeftKneeWorld, restFrame.LeftKnee) : PredictLimbPoint(leftHip, restFrame.LeftHip, restFrame.LeftKnee);
            Vector3 rightKnee = pose.HasRightKnee ? ToRootLocal(pose.RightKneeWorld, restFrame.RightKnee) : PredictLimbPoint(rightHip, restFrame.RightHip, restFrame.RightKnee);
            Vector3 leftAnkle = pose.HasLeftAnkle ? ToRootLocal(pose.LeftAnkleWorld, restFrame.LeftAnkle) : PredictLimbPoint(leftKnee, restFrame.LeftKnee, restFrame.LeftAnkle);
            Vector3 rightAnkle = pose.HasRightAnkle ? ToRootLocal(pose.RightAnkleWorld, restFrame.RightAnkle) : PredictLimbPoint(rightKnee, restFrame.RightKnee, restFrame.RightAnkle);

            return new TargetFrame
            {
                Torso = SegmentFrame.From(shoulderCenter, hipCenter),
                Hips = SegmentFrame.From(leftHip, rightHip),
                LeftUpperArm = SegmentFrame.From(leftShoulder, leftElbow),
                LeftForearm = SegmentFrame.From(leftElbow, leftWrist),
                RightUpperArm = SegmentFrame.From(rightShoulder, rightElbow),
                RightForearm = SegmentFrame.From(rightElbow, rightWrist),
                LeftThigh = SegmentFrame.From(leftHip, leftKnee),
                LeftCalf = SegmentFrame.From(leftKnee, leftAnkle),
                RightThigh = SegmentFrame.From(rightHip, rightKnee),
                RightCalf = SegmentFrame.From(rightKnee, rightAnkle),
            };
        }

        private Vector3 ToRootLocal(Vector3 worldPoint, Vector3 restPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            local.z = Mathf.Lerp(restPoint.z, local.z, zInfluence);
            return local;
        }

        private static Vector3 PredictLimbPoint(Vector3 targetStart, Vector3 restStart, Vector3 restEnd)
        {
            return targetStart + (restEnd - restStart);
        }

        public struct Pose
        {
            public bool HasLeftShoulder;
            public bool HasRightShoulder;
            public bool HasLeftElbow;
            public bool HasRightElbow;
            public bool HasLeftWrist;
            public bool HasRightWrist;
            public bool HasLeftHip;
            public bool HasRightHip;
            public bool HasLeftKnee;
            public bool HasRightKnee;
            public bool HasLeftAnkle;
            public bool HasRightAnkle;
            public Vector3 LeftShoulderWorld;
            public Vector3 RightShoulderWorld;
            public Vector3 LeftElbowWorld;
            public Vector3 RightElbowWorld;
            public Vector3 LeftWristWorld;
            public Vector3 RightWristWorld;
            public Vector3 LeftHipWorld;
            public Vector3 RightHipWorld;
            public Vector3 LeftKneeWorld;
            public Vector3 RightKneeWorld;
            public Vector3 LeftAnkleWorld;
            public Vector3 RightAnkleWorld;

            public bool HasUpperBody => HasLeftShoulder && HasRightShoulder;
        }

        private sealed class RiggedMesh
        {
            private readonly GarmentRuntimeRig owner;
            private readonly MeshFilter filter;
            private readonly Mesh mesh;
            private readonly Vector3[] restLocalVertices;
            private readonly Vector3[] restRootVertices;
            private readonly Vector3[] deformedLocalVertices;
            private VertexBinding[] bindings;
            private RestFrame restFrame;

            public RiggedMesh(GarmentRuntimeRig owner, MeshFilter filter, Mesh mesh)
            {
                this.owner = owner;
                this.filter = filter;
                this.mesh = mesh;
                restLocalVertices = mesh.vertices;
                restRootVertices = new Vector3[restLocalVertices.Length];
                deformedLocalVertices = new Vector3[restLocalVertices.Length];

                for (int i = 0; i < restLocalVertices.Length; i++)
                {
                    restRootVertices[i] = owner.transform.InverseTransformPoint(filter.transform.TransformPoint(restLocalVertices[i]));
                    deformedLocalVertices[i] = restLocalVertices[i];
                }
            }

            public Vector3[] RestRootVertices => restRootVertices;

            public void Bind(RestFrame frame, GarmentSlot slot, float limbWeight)
            {
                restFrame = frame;
                bindings = new VertexBinding[restRootVertices.Length];
                for (int i = 0; i < restRootVertices.Length; i++)
                {
                    bindings[i] = VertexBinding.Create(restRootVertices[i], frame.Bounds, slot, limbWeight);
                }
            }

            public void Apply(TargetFrame targetFrame, float blend, bool recalculateNormals)
            {
                if (bindings == null)
                {
                    return;
                }

                for (int i = 0; i < restRootVertices.Length; i++)
                {
                    Vector3 restRoot = restRootVertices[i];
                    Vector3 deformedRoot = bindings[i].Apply(
                        restRoot,
                        restFrame,
                        targetFrame,
                        owner.minSegmentStretch,
                        owner.maxSegmentStretch);
                    deformedRoot = Vector3.Lerp(restRoot, deformedRoot, blend);
                    deformedLocalVertices[i] = filter.transform.InverseTransformPoint(owner.transform.TransformPoint(deformedRoot));
                }

                mesh.vertices = deformedLocalVertices;
                mesh.RecalculateBounds();
                if (recalculateNormals)
                {
                    mesh.RecalculateNormals();
                }
            }

            public void ResetToRest(bool recalculateNormals)
            {
                mesh.vertices = restLocalVertices;
                mesh.RecalculateBounds();
                if (recalculateNormals)
                {
                    mesh.RecalculateNormals();
                }
            }
        }

        private struct VertexBinding
        {
            private float torso;
            private float hips;
            private float leftUpperArm;
            private float leftForearm;
            private float rightUpperArm;
            private float rightForearm;
            private float leftThigh;
            private float leftCalf;
            private float rightThigh;
            private float rightCalf;

            public static VertexBinding Create(Vector3 point, Bounds bounds, GarmentSlot slot, float limbWeight)
            {
                return slot == GarmentSlot.Lower
                    ? CreateLower(point, bounds, limbWeight)
                    : CreateUpperLike(point, bounds, slot, limbWeight);
            }

            public Vector3 Apply(Vector3 restPoint, RestFrame rest, TargetFrame target, float minStretch, float maxStretch)
            {
                Vector3 sum = Vector3.zero;
                float total = 0f;
                Add(torso, SegmentFrame.Transform(restPoint, rest.Torso, target.Torso, minStretch, maxStretch), ref sum, ref total);
                Add(hips, SegmentFrame.Transform(restPoint, rest.Hips, target.Hips, minStretch, maxStretch), ref sum, ref total);
                Add(leftUpperArm, SegmentFrame.Transform(restPoint, rest.LeftUpperArm, target.LeftUpperArm, minStretch, maxStretch), ref sum, ref total);
                Add(leftForearm, SegmentFrame.Transform(restPoint, rest.LeftForearm, target.LeftForearm, minStretch, maxStretch), ref sum, ref total);
                Add(rightUpperArm, SegmentFrame.Transform(restPoint, rest.RightUpperArm, target.RightUpperArm, minStretch, maxStretch), ref sum, ref total);
                Add(rightForearm, SegmentFrame.Transform(restPoint, rest.RightForearm, target.RightForearm, minStretch, maxStretch), ref sum, ref total);
                Add(leftThigh, SegmentFrame.Transform(restPoint, rest.LeftThigh, target.LeftThigh, minStretch, maxStretch), ref sum, ref total);
                Add(leftCalf, SegmentFrame.Transform(restPoint, rest.LeftCalf, target.LeftCalf, minStretch, maxStretch), ref sum, ref total);
                Add(rightThigh, SegmentFrame.Transform(restPoint, rest.RightThigh, target.RightThigh, minStretch, maxStretch), ref sum, ref total);
                Add(rightCalf, SegmentFrame.Transform(restPoint, rest.RightCalf, target.RightCalf, minStretch, maxStretch), ref sum, ref total);
                return total > 0.001f ? sum / total : restPoint;
            }

            private static VertexBinding CreateUpperLike(Vector3 point, Bounds bounds, GarmentSlot slot, float limbWeight)
            {
                float width = Mathf.Max(0.001f, bounds.size.x);
                float height = Mathf.Max(0.001f, bounds.size.y);
                float centerX = bounds.center.x;
                float minY = bounds.min.y;
                float maxY = bounds.max.y;
                float halfWidth = width * 0.5f;
                float absX = Mathf.Abs(point.x - centerX);
                float y01 = Mathf.InverseLerp(minY, maxY, point.y);
                float torsoHalf = slot == GarmentSlot.Outerwear ? width * 0.24f : width * 0.22f;
                float side = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(torsoHalf, halfWidth * 0.98f, absX));
                float armBand = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.72f, y01));
                float arm = Mathf.Clamp01(side * armBand);
                float sleeveT = Mathf.InverseLerp(torsoHalf, halfWidth * 0.98f, absX);
                float lowerBody = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 0.05f, y01));
                float skirt = slot == GarmentSlot.OnePiece ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.08f, y01)) : 0f;
                float armTorsoRelease = Mathf.Lerp(0.28f, 0.82f, Mathf.Clamp01(limbWeight));

                VertexBinding binding = new VertexBinding
                {
                    torso = Mathf.Clamp01(1f - arm * armTorsoRelease - lowerBody * 0.22f - skirt * 0.28f),
                    hips = Mathf.Max(lowerBody * 0.30f, skirt * 0.50f),
                };

                if (point.x < centerX)
                {
                    binding.leftUpperArm = arm * (1f - sleeveT) * limbWeight;
                    binding.leftForearm = arm * sleeveT * limbWeight;
                    binding.leftThigh = skirt * Mathf.Clamp01(1f - sleeveT * 0.35f) * 0.12f * limbWeight;
                    binding.leftCalf = skirt * Mathf.Clamp01(sleeveT) * 0.08f * limbWeight;
                }
                else
                {
                    binding.rightUpperArm = arm * (1f - sleeveT) * limbWeight;
                    binding.rightForearm = arm * sleeveT * limbWeight;
                    binding.rightThigh = skirt * Mathf.Clamp01(1f - sleeveT * 0.35f) * 0.12f * limbWeight;
                    binding.rightCalf = skirt * Mathf.Clamp01(sleeveT) * 0.08f * limbWeight;
                }

                binding.Normalize();
                return binding;
            }

            private static VertexBinding CreateLower(Vector3 point, Bounds bounds, float limbWeight)
            {
                float width = Mathf.Max(0.001f, bounds.size.x);
                float centerX = bounds.center.x;
                float minY = bounds.min.y;
                float maxY = bounds.max.y;
                float y01 = Mathf.InverseLerp(minY, maxY, point.y);
                float absX = Mathf.Abs(point.x - centerX);
                float centerBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, width * 0.10f, absX));
                float waist = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.70f, 0.96f, y01));
                float lowerLeg = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.08f, y01));

                VertexBinding binding = new VertexBinding
                {
                    hips = waist * (1f - centerBlend * 0.45f),
                };

                float thigh = (1f - lowerLeg) * centerBlend;
                float calf = lowerLeg * centerBlend;
                if (point.x < centerX)
                {
                    binding.leftThigh = thigh * limbWeight;
                    binding.leftCalf = calf * limbWeight;
                }
                else
                {
                    binding.rightThigh = thigh * limbWeight;
                    binding.rightCalf = calf * limbWeight;
                }

                binding.Normalize();
                return binding;
            }

            private void Normalize()
            {
                float total = torso + hips + leftUpperArm + leftForearm + rightUpperArm + rightForearm + leftThigh + leftCalf + rightThigh + rightCalf;
                if (total <= 0.001f)
                {
                    torso = 1f;
                    return;
                }

                float inv = 1f / total;
                torso *= inv;
                hips *= inv;
                leftUpperArm *= inv;
                leftForearm *= inv;
                rightUpperArm *= inv;
                rightForearm *= inv;
                leftThigh *= inv;
                leftCalf *= inv;
                rightThigh *= inv;
                rightCalf *= inv;
            }

            private static void Add(float weight, Vector3 value, ref Vector3 sum, ref float total)
            {
                if (weight <= 0.0001f)
                {
                    return;
                }

                sum += value * weight;
                total += weight;
            }
        }

        private struct RestFrame
        {
            public Bounds Bounds;
            public Vector3 ShoulderCenter;
            public Vector3 LeftShoulder;
            public Vector3 RightShoulder;
            public Vector3 LeftElbow;
            public Vector3 RightElbow;
            public Vector3 LeftWrist;
            public Vector3 RightWrist;
            public Vector3 LeftHip;
            public Vector3 RightHip;
            public Vector3 LeftKnee;
            public Vector3 RightKnee;
            public Vector3 LeftAnkle;
            public Vector3 RightAnkle;
            public SegmentFrame Torso;
            public SegmentFrame Hips;
            public SegmentFrame LeftUpperArm;
            public SegmentFrame LeftForearm;
            public SegmentFrame RightUpperArm;
            public SegmentFrame RightForearm;
            public SegmentFrame LeftThigh;
            public SegmentFrame LeftCalf;
            public SegmentFrame RightThigh;
            public SegmentFrame RightCalf;

            public static RestFrame FromBounds(Bounds bounds, GarmentSlot slot)
            {
                float width = Mathf.Max(0.001f, bounds.size.x);
                float height = Mathf.Max(0.001f, bounds.size.y);
                float centerX = bounds.center.x;
                float centerZ = bounds.center.z;
                float minX = bounds.min.x;
                float maxX = bounds.max.x;
                float minY = bounds.min.y;
                float maxY = bounds.max.y;

                float shoulderY = maxY - height * 0.24f;
                float hipY = slot == GarmentSlot.Lower ? maxY - height * 0.12f : minY + height * 0.22f;
                float shoulderHalf = width * 0.22f;
                float hipHalf = width * 0.20f;
                float kneeY = minY + height * 0.42f;
                float ankleY = minY + height * 0.08f;
                float legHalf = width * 0.12f;

                Vector3 shoulderCenter = new Vector3(centerX, shoulderY, centerZ);
                Vector3 hipCenter = new Vector3(centerX, hipY, centerZ);
                Vector3 leftShoulder = new Vector3(centerX - shoulderHalf, shoulderY, centerZ);
                Vector3 rightShoulder = new Vector3(centerX + shoulderHalf, shoulderY, centerZ);
                Vector3 leftWrist = new Vector3(minX + width * 0.04f, shoulderY - height * 0.04f, centerZ);
                Vector3 rightWrist = new Vector3(maxX - width * 0.04f, shoulderY - height * 0.04f, centerZ);
                Vector3 leftElbow = Vector3.Lerp(leftShoulder, leftWrist, 0.52f);
                Vector3 rightElbow = Vector3.Lerp(rightShoulder, rightWrist, 0.52f);
                Vector3 leftHip = new Vector3(centerX - hipHalf, hipY, centerZ);
                Vector3 rightHip = new Vector3(centerX + hipHalf, hipY, centerZ);
                Vector3 leftKnee = new Vector3(centerX - legHalf, kneeY, centerZ);
                Vector3 rightKnee = new Vector3(centerX + legHalf, kneeY, centerZ);
                Vector3 leftAnkle = new Vector3(centerX - legHalf, ankleY, centerZ);
                Vector3 rightAnkle = new Vector3(centerX + legHalf, ankleY, centerZ);

                return new RestFrame
                {
                    Bounds = bounds,
                    ShoulderCenter = shoulderCenter,
                    LeftShoulder = leftShoulder,
                    RightShoulder = rightShoulder,
                    LeftElbow = leftElbow,
                    RightElbow = rightElbow,
                    LeftWrist = leftWrist,
                    RightWrist = rightWrist,
                    LeftHip = leftHip,
                    RightHip = rightHip,
                    LeftKnee = leftKnee,
                    RightKnee = rightKnee,
                    LeftAnkle = leftAnkle,
                    RightAnkle = rightAnkle,
                    Torso = SegmentFrame.From(shoulderCenter, hipCenter),
                    Hips = SegmentFrame.From(leftHip, rightHip),
                    LeftUpperArm = SegmentFrame.From(leftShoulder, leftElbow),
                    LeftForearm = SegmentFrame.From(leftElbow, leftWrist),
                    RightUpperArm = SegmentFrame.From(rightShoulder, rightElbow),
                    RightForearm = SegmentFrame.From(rightElbow, rightWrist),
                    LeftThigh = SegmentFrame.From(leftHip, leftKnee),
                    LeftCalf = SegmentFrame.From(leftKnee, leftAnkle),
                    RightThigh = SegmentFrame.From(rightHip, rightKnee),
                    RightCalf = SegmentFrame.From(rightKnee, rightAnkle),
                };
            }
        }

        private struct TargetFrame
        {
            public SegmentFrame Torso;
            public SegmentFrame Hips;
            public SegmentFrame LeftUpperArm;
            public SegmentFrame LeftForearm;
            public SegmentFrame RightUpperArm;
            public SegmentFrame RightForearm;
            public SegmentFrame LeftThigh;
            public SegmentFrame LeftCalf;
            public SegmentFrame RightThigh;
            public SegmentFrame RightCalf;

            public static TargetFrame Lerp(TargetFrame a, TargetFrame b, float t)
            {
                return new TargetFrame
                {
                    Torso = SegmentFrame.Lerp(a.Torso, b.Torso, t),
                    Hips = SegmentFrame.Lerp(a.Hips, b.Hips, t),
                    LeftUpperArm = SegmentFrame.Lerp(a.LeftUpperArm, b.LeftUpperArm, t),
                    LeftForearm = SegmentFrame.Lerp(a.LeftForearm, b.LeftForearm, t),
                    RightUpperArm = SegmentFrame.Lerp(a.RightUpperArm, b.RightUpperArm, t),
                    RightForearm = SegmentFrame.Lerp(a.RightForearm, b.RightForearm, t),
                    LeftThigh = SegmentFrame.Lerp(a.LeftThigh, b.LeftThigh, t),
                    LeftCalf = SegmentFrame.Lerp(a.LeftCalf, b.LeftCalf, t),
                    RightThigh = SegmentFrame.Lerp(a.RightThigh, b.RightThigh, t),
                    RightCalf = SegmentFrame.Lerp(a.RightCalf, b.RightCalf, t),
                };
            }
        }

        private struct SegmentFrame
        {
            private const float MinLength = 0.001f;

            public Vector3 Start;
            public Vector3 End;
            public Quaternion Rotation;
            public float Length;

            public static SegmentFrame From(Vector3 start, Vector3 end)
            {
                Vector3 direction = end - start;
                float length = direction.magnitude;
                if (length <= MinLength)
                {
                    direction = Vector3.down * MinLength;
                    length = MinLength;
                    end = start + direction;
                }

                return new SegmentFrame
                {
                    Start = start,
                    End = end,
                    Rotation = Quaternion.FromToRotation(Vector3.up, direction),
                    Length = length,
                };
            }

            public static SegmentFrame Lerp(SegmentFrame a, SegmentFrame b, float t)
            {
                return From(Vector3.Lerp(a.Start, b.Start, t), Vector3.Lerp(a.End, b.End, t));
            }

            public static Vector3 Transform(Vector3 point, SegmentFrame rest, SegmentFrame target, float minStretch, float maxStretch)
            {
                Vector3 local = Quaternion.Inverse(rest.Rotation) * (point - rest.Start);
                float stretch = target.Length / Mathf.Max(MinLength, rest.Length);
                local.y *= Mathf.Clamp(stretch, minStretch, maxStretch);
                return target.Start + target.Rotation * local;
            }
        }
    }
}
