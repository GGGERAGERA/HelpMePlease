namespace Subject42.Combat.OrbitalStation
{
    public interface IOrbitalPathGeometryResolver
    {
        bool TryResolve(OrbitalPathType pathType,
            OrbitalPresentationConfig config,
            out OrbitalPathGeometry geometry,
            out string error);
    }

    public sealed class OrbitalPathGeometryResolver : IOrbitalPathGeometryResolver
    {
        public static readonly OrbitalPathGeometryResolver Default = new();

        public bool TryResolve(OrbitalPathType pathType,
            OrbitalPresentationConfig config,
            out OrbitalPathGeometry geometry,
            out string error)
        {
            geometry = null;
            if (config == null)
                return Fail("presentation config is missing", out error);

            if (pathType == OrbitalPathType.Circle)
            {
                geometry = OrbitalPathGeometry.Circle;
                error = null;
                return true;
            }
            if (pathType == OrbitalPathType.FigureEight)
            {
                geometry = new OrbitalPathGeometry(config);
                error = null;
                return true;
            }
            if (pathType == OrbitalPathType.Custom)
            {
                geometry = OrbitalPathGeometry.Circle;
                error = null;
                return true;
            }

            return Fail($"unsupported ORBITAL path '{pathType}'", out error);
        }

        private static bool Fail(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
