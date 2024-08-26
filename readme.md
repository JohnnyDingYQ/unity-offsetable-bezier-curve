# 2D Offsetting/Stroking of Unity's BezierCurve
This library provides the Curve class to containerize BeizerCurve of Unity.Spline to offset/stroke Bezier Curves with other functionalities. While the Beizer Curve itself is in 3D, the offset is assumed to be in 2D and with respect to the xz plane.

## Requirements
* Unity Engine
* Unity.Splines package

## Features
* Offset curves with respect to the xz plane
* Lightweight implementation with low overhead
* Truncate curves from the start or the end
* Merge/Add curves
* Split curves
* Reverse curves
* Duplicate curves
* Find minimum distance between a curve and a Unity Ray
* Evaluate position, tangent, and 2D normal of curve given a distance on curve

## Potential Limitations
* Numerical inaccuracies caused by the conversion between distance on curve and interpolation for Bezier Curve
* Robustness under extreme cases

## Installation/Usage
* Copy and paste the Curve.cs file into your project
* Create an instance of Unity.Spline.BezierCurve and pass it to the constructor of the Curve class 
