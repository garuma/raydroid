﻿//--------------------------------------------------------------------------
// 
//  Copyright (c) Microsoft Corporation.  All rights reserved. 
// 
//  File: Raytracer.cs
//
//--------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ParallelComputingPlatform.ParallelExtensions.Samples
{
    internal sealed class RayTracer
    {
        private int screenWidth;
        private int screenHeight;
        private const int MaxDepth = 5;

        public RayTracer(int screenWidth, int screenHeight)
        {
            this.screenWidth = screenWidth;
            this.screenHeight = screenHeight;
        }

        internal void RenderSequential(Scene scene, Int32[] rgb)
        {
            for (int y = 0; y < screenHeight; y++)
            {
                int stride = y * screenWidth;
                Camera camera = scene.Camera;
                for (int x = 0; x < screenWidth; x++)
                {
                    Color color = TraceRay(new Ray(camera.Pos, GetPoint(x, y, camera)), scene, 0);
                    rgb[x + stride] = color.ToInt32();
                }
            }
        }

        internal void RenderParallel(Scene scene, Int32[] rgb, ParallelOptions options)
        {
            Mono.Threading.Tasks.Parallel.For<object>(0, screenHeight, options, () => null, (y, state, hue) =>
            {
                int stride = y * screenWidth;
                Camera camera = scene.Camera;
                for (int x = 0; x < screenWidth; x++)
                {
                    Color color = TraceRay(new Ray(camera.Pos, GetPoint(x, y, camera)), scene, 0);
                    rgb[x + stride] = color.ToInt32();
                }
                return hue;
            }, delegate { });
        }

        internal void RenderParallelShowingThreads(Scene scene, Int32[] rgb, ParallelOptions options)
        {
            int id = 0;
            Mono.Threading.Tasks.Parallel.For<double>(0, screenHeight, options, () => GetHueShift(Interlocked.Increment(ref id)), (y, state, hue) =>
            {
                int stride = y * screenWidth;
                Camera camera = scene.Camera;
                for (int x = 0; x < screenWidth; x++)
                {
                    Color color = TraceRay(new Ray(camera.Pos, GetPoint(x, y, camera)), scene, 0);
                    color.ChangeHue(hue);
                    rgb[x + stride] = color.ToInt32();
                }
                return hue;
            }, 
            hue => Interlocked.Decrement(ref id));
        }

        private Dictionary<int, double> _numToHueShiftLookup = new Dictionary<int, double>();
        private Random _rand = new Random();

        private double GetHueShift(int id)
        {
            double shift;
            lock (_numToHueShiftLookup)
            {
                if (!_numToHueShiftLookup.TryGetValue(id, out shift))
                {
                    shift = _rand.NextDouble();
                    _numToHueShiftLookup.Add(id, shift);
                }
            }
            return shift;
        }

        internal readonly Scene DefaultScene = CreateDefaultScene();

        static Scene CreateDefaultScene()
        {
            SceneObject[] things =  {
                new Sphere( new Vector(-0.5f,1,1.5f), 0.5f, Surfaces.Shiny),
                new Sphere( new Vector(0,1f,-0.25f), 1, Surfaces.MatteShiny),
               new Plane( new Vector(0,1,0), 0, Surfaces.CheckerBoard)
            };
            Light[] lights = {
                //new Light(new Vector(-2,2.5f,0),new Color(.5,.45,.41)),
                new Light(new Vector(2,4.5f,2), new Color(.99,.95,.8))
            };
            Camera camera = Camera.Create(new Vector(2.75f, 2, 3.75f), new Vector(-0.6f, .5f, 0));

            return new Scene(things, lights, camera);
        }


        private ISect MinIntersection(Ray ray, Scene scene)
        {
            ISect min = ISect.Null;
            foreach (SceneObject obj in scene.Things)
            {
                ISect isect = obj.Intersect(ray);
                if (!ISect.IsNull(isect))
                {
                    if (ISect.IsNull(min) || min.Dist > isect.Dist)
                    {
                        min = isect;
                    }
                }
            }
            return min;
        }

        private double TestRay(Ray ray, Scene scene)
        {
            ISect isect = MinIntersection(ray, scene);
            if (ISect.IsNull(isect))
                return 0;
            return isect.Dist;
        }

        private Color TraceRay(Ray ray, Scene scene, int depth)
        {
            ISect isect = MinIntersection(ray, scene);
            if (ISect.IsNull(isect))
                return Color.Background;
            return Shade(isect, scene, depth);
        }

        private Color GetNaturalColor(SceneObject thing, Vector pos, Vector norm, Vector rd, Scene scene)
        {
            Color ret = new Color(0, 0, 0);
            foreach (Light light in scene.Lights)
            {
                Vector ldis = Vector.Minus(light.Pos, pos);
                Vector livec = Vector.Norm(ldis);
                double neatIsect = TestRay(new Ray(pos, livec), scene);
                bool isInShadow = !((neatIsect > Vector.Mag(ldis)) || (neatIsect == 0));
                if (!isInShadow)
                {
                    double illum = Vector.Dot(livec, norm);
                    Color lcolor = illum > 0 ? Color.Times(illum, light.Color) : new Color(0, 0, 0);
                    double specular = Vector.Dot(livec, Vector.Norm(rd));
                    Color scolor = specular > 0 ? Color.Times(Math.Pow(specular, thing.Surface.Roughness), light.Color) : new Color(0, 0, 0);
                    ret = Color.Plus(ret, Color.Plus(Color.Times(thing.Surface.Diffuse(pos), lcolor),
                                                     Color.Times(thing.Surface.Specular(pos), scolor)));
                }
            }
            return ret;
        }

        private Color GetReflectionColor(SceneObject thing, Vector pos, Vector norm, Vector rd, Scene scene, int depth)
        {
            return Color.Times(thing.Surface.Reflect(pos), TraceRay(new Ray(pos, rd), scene, depth + 1));
        }

        private Color Shade(ISect isect, Scene scene, int depth)
        {
            Vector d = isect.Ray.Dir;
            Vector pos = Vector.Plus(Vector.Times(isect.Dist, isect.Ray.Dir), isect.Ray.Start);
            Vector normal = isect.Thing.Normal(pos);
            Vector reflectDir = Vector.Minus(d, Vector.Times(2 * Vector.Dot(normal, d), normal));
            Color ret = Color.DefaultColor;
            ret = Color.Plus(ret, GetNaturalColor(isect.Thing, pos, normal, reflectDir, scene));
            if (depth >= MaxDepth)
            {
                return Color.Plus(ret, new Color(.5, .5, .5));
            }
            return Color.Plus(ret, GetReflectionColor(isect.Thing, Vector.Plus(pos, Vector.Times(.001f, reflectDir)), normal, reflectDir, scene, depth));
        }

        private float RecenterX(float x)
        {
            return (x - (screenWidth / 2.0f)) / (2.0f * screenWidth);
        }
        private float RecenterY(float y)
        {
            return -(y - (screenHeight / 2.0f)) / (2.0f * screenHeight);
        }

        private Vector GetPoint(float x, float y, Camera camera)
        {
            return Vector.Norm(Vector.Plus(camera.Forward, Vector.Plus(Vector.Times(RecenterX(x), camera.Right),
                                                                       Vector.Times(RecenterY(y), camera.Up))));
        }
    }
}