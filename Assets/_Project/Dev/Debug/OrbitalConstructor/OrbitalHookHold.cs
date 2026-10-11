#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace Subject42.DebugLabs.Constructor
{
    // A scene-local grab lease. Restore the exact enabled/body states, including static test dummies.
    public sealed class OrbitalHookHold
    {
        public EnemyHealth Target { get; private set; }
        public int NodeId { get; private set; }
        public float Remaining => Target!=null ? Mathf.Max(0,releaseTime-Time.time) : 0;
        private readonly List<(Behaviour behaviour,bool enabled)> suspended=new();
        private Rigidbody2D body;
        private RigidbodyType2D bodyType;
        private float releaseTime;
        public bool Begin(EnemyHealth target,int node,Vector2 point,float seconds)
        {
            if (Target!=null || target==null || target.IsDead) return false;
            Target=target; NodeId=node; releaseTime=Time.time+seconds;
            foreach (var movement in target.GetComponents<EnemyMovement>()) Suspend(movement);
            Suspend(target.GetComponent<EnemyCollisionHandler>());
            body=target.GetComponent<Rigidbody2D>();
            if (body!=null) { bodyType=body.bodyType; body.linearVelocity=Vector2.zero; body.angularVelocity=0; body.bodyType=RigidbodyType2D.Kinematic; }
            Move(point); return true;
        }
        private void Suspend(Behaviour behaviour)
        { if (behaviour!=null) { suspended.Add((behaviour,behaviour.enabled)); behaviour.enabled=false; } }
        public void Move(Vector2 point)
        {
            if (Target==null) return;
            if (body!=null) { body.position=point; body.linearVelocity=Vector2.zero; }
            else Target.transform.position=point;
        }
        public void Release()
        {
            foreach (var entry in suspended) if (entry.behaviour!=null) entry.behaviour.enabled=entry.enabled;
            suspended.Clear();
            if (body!=null) { body.bodyType=bodyType; body.linearVelocity=Vector2.zero; body.angularVelocity=0; }
            Target=null; body=null; NodeId=0;
        }
    }
}
#endif
