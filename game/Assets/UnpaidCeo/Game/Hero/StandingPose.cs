using UnityEngine;

namespace UnpaidCeo.Game.Hero
{
    /// <summary>
    /// Where a character's soles and the top of its head are in its idle pose, in the model's own units (measured by the
    /// editor from the posed skinned mesh). The renderer's bounding box of a skinned model is larger than the model, so
    /// standing it by the box left people hovering ~0.4 m over the floor and a little small.
    /// </summary>
    public sealed class StandingPose : MonoBehaviour
    {
        [SerializeField] float sole;
        [SerializeField] float top;

        public float Sole => sole;
        public float Top => top;
        public float Height => top - sole;
        public bool Valid => top - sole > 1e-4f;

        public void Set(float soleY, float topY)
        {
            sole = soleY;
            top = topY;
        }

        /// <summary>
        /// The lowest and highest point of what the model draws in its current pose, in world space, by skinning the mesh
        /// with its bones (the editor and tests use it; the mesh must be readable). SkinnedMeshRenderer.BakeMesh is not used:
        /// in edit mode it does not follow a pose set by AnimationClip.SampleAnimation.
        /// </summary>
        public static (float min, float max) PosedHeightRange(GameObject model)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var bones = smr.bones;
                var bind = mesh.bindposes;
                var weights = mesh.boneWeights;
                var vertices = mesh.vertices;
                var skin = new Matrix4x4[bones.Length];
                for (int b = 0; b < bones.Length; b++) skin[b] = bones[b] != null ? bones[b].localToWorldMatrix * bind[b] : smr.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = weights[i];
                    var v = vertices[i];
                    float y = skin[w.boneIndex0].MultiplyPoint3x4(v).y * w.weight0 + skin[w.boneIndex1].MultiplyPoint3x4(v).y * w.weight1
                            + skin[w.boneIndex2].MultiplyPoint3x4(v).y * w.weight2 + skin[w.boneIndex3].MultiplyPoint3x4(v).y * w.weight3;
                    if (y < min) min = y;
                    if (y > max) max = y;
                }
            }
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.GetComponent<Renderer>().bounds;
                min = Mathf.Min(min, b.min.y);
                max = Mathf.Max(max, b.max.y);
            }
            return (min, max);
        }
    }
}
