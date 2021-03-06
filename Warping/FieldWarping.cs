﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MorphingTool
{
    /// <summary>
    /// Warping using field lines
    /// Sources:
    /// http://blog.polysfactory.com/2012/01/image-morphing.html
    /// http://www.cg.tuwien.ac.at/studentwork/VRSem96/Morphing/
    /// </summary>
    public class FieldWarping : WarpingAlgorithm
    {
        struct WarpMarker
        {
            public Vector target_start;
            public Vector target_dirNorm;
            public Vector target_perpendicNorm;
            public double target_lineLength;

            public Vector dest_start;
            public Vector dest_dirNorm;
            public Vector dest_perpendicNorm;
        };

        const float LINE_WEIGHT = 0.03f;

        unsafe void WarpingAlgorithm.WarpImage(MarkerSet markerSet, Morphing.ImageData inputImage, Morphing.ImageData outputImage, bool startImage)
        {
            System.Diagnostics.Debug.Assert(markerSet != null && inputImage != null && outputImage != null);

            LineMarkerSet lineMarkerSet = markerSet as LineMarkerSet;
            System.Diagnostics.Debug.Assert(lineMarkerSet != null);

            double xStep = 1.0 / outputImage.Width;
            
            WarpMarker[] markers;
            if (startImage)
            {
                markers = lineMarkerSet.Lines.Select(x => new WarpMarker
                {
                    target_start = x.InterpolatedMarker.Start,
                    target_dirNorm = x.InterpolatedMarker.End - x.InterpolatedMarker.Start,
                    target_perpendicNorm = (x.InterpolatedMarker.End - x.InterpolatedMarker.Start).Perpendicular(),
                    target_lineLength = (x.InterpolatedMarker.End - x.InterpolatedMarker.Start).Length,

                    dest_start = x.StartMarker.Start,
                    dest_dirNorm = x.StartMarker.End - x.StartMarker.Start,
                    dest_perpendicNorm = (x.StartMarker.End - x.StartMarker.Start).Perpendicular(),
                }).ToArray();
            }
            else
            {
                markers =  lineMarkerSet.Lines.Select(x => new WarpMarker
                {
                    target_start = x.InterpolatedMarker.Start,
                    target_dirNorm = x.InterpolatedMarker.End - x.InterpolatedMarker.Start,
                    target_perpendicNorm = (x.InterpolatedMarker.End - x.InterpolatedMarker.Start).Perpendicular(),
                    target_lineLength = (x.InterpolatedMarker.End - x.InterpolatedMarker.Start).Length,

                    dest_start = x.EndMarker.Start,
                    dest_dirNorm = x.EndMarker.End - x.EndMarker.Start,
                    dest_perpendicNorm = (x.EndMarker.End - x.EndMarker.Start).Perpendicular(),
                }).ToArray();
            }
            for (int markerIdx = 0; markerIdx < markers.Length; ++markerIdx)
            {
             //   markers[markerIdx].target_dirLength = markers[markerIdx].target_dir.Length;
                markers[markerIdx].target_perpendicNorm.Normalize();
                markers[markerIdx].target_dirNorm.Normalize();
                markers[markerIdx].dest_perpendicNorm.Normalize();
                markers[markerIdx].dest_dirNorm.Normalize();
            }

            if (markers.Length == 0)
                return;


            Parallel.For(0, outputImage.Height, yi =>
            {
                Color* outputDataPixel = outputImage.Data + yi * outputImage.Width;
                Color* lastOutputDataPixel = outputDataPixel + outputImage.Width;
                double y = (double)yi / outputImage.Height;

                for (double x = 0; outputDataPixel != lastOutputDataPixel; x += xStep, ++outputDataPixel)
                {
                    Vector position = new Vector(x, y);
                    Vector displacement = new Vector(0, 0);
                    double weightSum = 0.0f;

                    for (int markerIdx = 0; markerIdx < markers.Length; ++markerIdx)
                    {
                        Vector toStart = position - markers[markerIdx].target_start;
   
                        // calc relative coordinates to line
                        double u = toStart.Dot(markers[markerIdx].target_dirNorm);
                        double v = toStart.Dot(markers[markerIdx].target_perpendicNorm);
                        // calc weight
                        double weight;
                        if (u < 0) // bellow
                            weight = toStart.LengthSquared;
                        else if (u > 1) // above
                            weight = (toStart + markers[markerIdx].target_dirNorm * markers[markerIdx].target_lineLength).LengthSquared;
                        else // beside
                            weight = v * v;
                        weight = Math.Exp(-weight / LINE_WEIGHT); //Math.Pow(markers[markerIdx].target_lineLength / (A + weight), B);
                        weightSum += weight;

                        // translation
                        Vector srcPoint = markers[markerIdx].dest_start + u * markers[markerIdx].dest_dirNorm + v * markers[markerIdx].dest_perpendicNorm;
                        displacement += (srcPoint - position) * weight;
                    }

                    displacement /= weightSum;
                    position += displacement;
                    position = position.ClampToImageArea();

                    *outputDataPixel = inputImage.Sample(position.X, position.Y);
                }
            });
        }
    }
}
