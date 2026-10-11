#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace Subject42.DebugLabs.Constructor
{
    [Serializable]
    public sealed class PartMasses
    {
        [Min(.01f)] public float Beam=.2f;
        [Min(.01f)] public float Fork=.3f;
        [Min(.01f)] public float Blade=2f;
        [Min(.01f)] public float Electrode=.7f;
        [Min(.01f)] public float Shield=2.5f;
        [Min(.01f)] public float Hook=1.6f;
        public float Get(PartKind kind) => Mathf.Max(.01f,kind switch
        {
            PartKind.Beam=>Beam, PartKind.Fork=>Fork, PartKind.Blade=>Blade,
            PartKind.Electrode=>Electrode, PartKind.Shield=>Shield, _=>Hook
        });
        public void Set(PartKind kind,float mass)
        {
            mass=Mathf.Max(.01f,mass);
            switch (kind)
            {
                case PartKind.Beam: Beam=mass; break;
                case PartKind.Fork: Fork=mass; break;
                case PartKind.Blade: Blade=mass; break;
                case PartKind.Electrode: Electrode=mass; break;
                case PartKind.Shield: Shield=mass; break;
                case PartKind.Hook: Hook=mass; break;
            }
        }
    }

    public readonly struct MassBalance
    {
        public readonly float TotalMass, Moment, DegreesPerSecond;
        public float TurnSeconds=>360f/DegreesPerSecond;
        public MassBalance(float mass,float moment,float speed) { TotalMass=mass; Moment=moment; DegreesPerSecond=speed; }
        public static MassBalance Calculate(OrbitalAssembly assembly,PartMasses masses,float baseSpeed,float response,float minimum,float maximum)
        {
            float mass=0, moment=0;
            foreach (var node in assembly.Nodes)
            {
                float value=masses.Get(node.Kind);
                Vector2 center=node.Point(OrbitalPartCatalog.Get(node.Kind).MassCenter);
                mass+=value; moment+=value*center.sqrMagnitude;
            }
            minimum=Mathf.Max(1f,minimum); maximum=Mathf.Max(minimum,maximum);
            float speed=Mathf.Clamp(Mathf.Max(0,baseSpeed)/(1+Mathf.Max(0,response)*moment),minimum,maximum);
            return new MassBalance(mass,moment,speed);
        }
        public float HoldSeconds(float fraction,float minimum,float maximum)
        {
            maximum=Mathf.Max(0,maximum);
            return Mathf.Clamp(Mathf.Max(0,fraction)*TurnSeconds,Mathf.Clamp(minimum,0,maximum),maximum);
        }
    }
}
#endif
