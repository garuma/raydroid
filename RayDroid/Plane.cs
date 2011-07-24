//--------------------------------------------------------------------------
// 
//  Copyright (c) Microsoft Corporation.  All rights reserved. 
// 
//  File: Plane.cs
//
//--------------------------------------------------------------------------

namespace Microsoft.ParallelComputingPlatform.ParallelExtensions.Samples
{
    class Plane : SceneObject
    {
        public Vector Norm;
        public float Offset;

        public Plane(Vector norm, float offset, Surface surface) : base(surface) { Norm = norm; Offset = offset; }

        public override ISect Intersect(Ray ray)
        {
            float denom = Vector.Dot(Norm, ray.Dir);
            if (denom > 0) return ISect.Null;
            return new ISect(this, ray, (Vector.Dot(Norm, ray.Start) + Offset) / (-denom));
        }

        public override Vector Normal(Vector pos)
        {
            return Norm;
        }
    }
}
