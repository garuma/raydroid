//--------------------------------------------------------------------------
// 
//  Copyright (c) Microsoft Corporation.  All rights reserved. 
// 
//  File: Sphere.cs
//
//--------------------------------------------------------------------------

using System;

namespace Microsoft.ParallelComputingPlatform.ParallelExtensions.Samples
{
    class Sphere : SceneObject
    {
        public Vector Center;
        public float Radius;

        public Sphere(Vector center, float radius, Surface surface) : base(surface) { Center = center; Radius = radius; }

        public override ISect Intersect(Ray ray)
        {
            Vector eo = Vector.Minus(Center, ray.Start);
            float v = Vector.Dot(eo, ray.Dir);
            float dist;
            if (v < 0)
            {
                dist = 0;
            }
            else
            {
                float disc = (float)Math.Pow(Radius, 2) - (Vector.Dot(eo, eo) - (float)Math.Pow(v, 2));
                dist = disc < 0 ? 0 : v - (float)Math.Sqrt(disc);
            }
            if (dist == 0) return ISect.Null;
            return new ISect(this, ray, dist);
        }

        public override Vector Normal(Vector pos)
        {
            return Vector.Norm(Vector.Minus(pos, Center));
        }
    }
}
