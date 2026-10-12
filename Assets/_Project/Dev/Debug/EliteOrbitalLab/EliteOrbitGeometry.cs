#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Subject42.DebugLabs.EliteOrbit
{
    internal static class EliteOrbitGeometry
    {
        public static float Distance(Vector2 point,Vector2 a,Vector2 b)
        { Vector2 edge=b-a; return Vector2.Distance(point,a+edge*Mathf.Clamp01(Vector2.Dot(point-a,edge)/Mathf.Max(.000001f,edge.sqrMagnitude))); }
        public static float SegmentDistance(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            float Cross(Vector2 x,Vector2 y)=>x.x*y.y-x.y*y.x;
            float divisor=Cross(b-a,d-c);
            if (Mathf.Abs(divisor)>.000001f)
            {
                float t=Cross(c-a,d-c)/divisor,u=Cross(c-a,b-a)/divisor;
                if (t>=0 && t<=1 && u>=0 && u<=1) return 0;
            }
            return Mathf.Min(Distance(a,c,d),Distance(b,c,d),Distance(c,a,b),Distance(d,a,b));
        }
        public static bool SegmentCircle(Vector2 a,Vector2 b,Vector2 center,float radius,out float time)
        {
            Vector2 delta=b-a,offset=a-center;
            if (offset.sqrMagnitude<=radius*radius) { time=0; return true; }
            float aa=delta.sqrMagnitude,bb=2*Vector2.Dot(offset,delta),cc=offset.sqrMagnitude-radius*radius;
            float discriminant=bb*bb-4*aa*cc;
            time=0;
            if (aa<.000001f || discriminant<0) return false;
            time=(-bb-Mathf.Sqrt(discriminant))/(2*aa);
            return time>=0 && time<=1;
        }
        // SAT between actual authored sprite bounds and a short physical plate section.
        public static bool SpriteBox(SpriteRenderer sprite,Vector2 a,Vector2 b,float thickness)
        {
            if (sprite==null || !sprite.enabled || !sprite.gameObject.activeInHierarchy || sprite.sprite==null) return false;
            Bounds bounds=sprite.localBounds;
            Vector2[] first={ sprite.transform.TransformPoint(new Vector3(bounds.min.x,bounds.min.y)),sprite.transform.TransformPoint(new Vector3(bounds.max.x,bounds.min.y)),
                sprite.transform.TransformPoint(new Vector3(bounds.max.x,bounds.max.y)),sprite.transform.TransformPoint(new Vector3(bounds.min.x,bounds.max.y)) };
            Vector2 normal=new(-(b-a).y,(b-a).x); normal=normal.normalized*thickness*.5f;
            Vector2[] second={ a+normal,b+normal,b-normal,a-normal };
            foreach (var points in new[] { first,second })
                for (int i=0;i<2;i++)
                {
                    Vector2 edge=points[i+1]-points[i],axis=new(-edge.y,edge.x);
                    if (axis.sqrMagnitude<.000001f) continue;
                    float minA=float.PositiveInfinity,maxA=float.NegativeInfinity,minB=float.PositiveInfinity,maxB=float.NegativeInfinity;
                    foreach (var p in first) { float dot=Vector2.Dot(p,axis); minA=Mathf.Min(minA,dot); maxA=Mathf.Max(maxA,dot); }
                    foreach (var p in second) { float dot=Vector2.Dot(p,axis); minB=Mathf.Min(minB,dot); maxB=Mathf.Max(maxB,dot); }
                    if (maxA<minB || maxB<minA) return false;
                }
            return true;
        }
    }
}
#endif
