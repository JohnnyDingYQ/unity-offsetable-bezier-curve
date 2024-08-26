using System;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Offsetted.Curve;
public class CurveTest
{
    float3 stride =  new(10, 0, 0);
    const float DefaultTolerance = 0.001f;

    [Test]
    public void MulitpleAddDistance()
    {
        Curve curve = new(new(0, stride, 2 * stride));
        float decrement = curve.Length / 5;

        curve = curve.AddStartDistance(decrement);
        float3 prevStart = curve.StartPos;
        curve = curve.AddStartDistance(decrement);
        Assert.AreNotEqual(prevStart, curve.StartPos);
        Assert.True(IsApproxEqual(math.length(prevStart - curve.StartPos), decrement));

        curve.AddEndDistance(decrement);
        float3 prevEnd = curve.EndPos;
        curve.AddEndDistance(decrement);
        Assert.AreNotEqual(prevStart, curve.EndPos);
        Assert.True(IsApproxEqual(math.length(prevEnd - curve.EndPos), decrement));
    }

    [Test]
    public void AddStartDistanceMultipleSegments()
    {
        Curve curve = new(new(0, stride, 2 * stride));
        float singleSegmentLength = curve.Length;
        curve.Add(new(new(2 * stride, 3 * stride, 4 * stride)));

        curve = curve.AddStartDistance(1.5f * singleSegmentLength);
        Assert.True(IsApproxEqual(curve.Length, 0.5f * singleSegmentLength));
        Assert.True(IsApproxEqual(3 * stride, curve.StartPos));
    }

    [Test]
    public void AddEndDistanceMultipleSegments()
    {
        Curve curve = new(new(0, stride, 2 * stride));
        float singleSegmentLength = curve.Length;
        curve.Add(new(new(2 * stride, 3 * stride, 4 * stride)));

        curve = curve.AddEndDistance(1.5f * singleSegmentLength);
        Assert.True(IsApproxEqual(curve.Length, 0.5f * singleSegmentLength));
        Assert.True(IsApproxEqual(stride, curve.EndPos));
    }

    [Test]
    public void SplitSingleSegment()
    {
        Curve curve = new(new(0, stride, 2 * stride));
        curve.Split(curve.Length * 0.5f, out Curve left, out Curve right);

        Assert.True(IsApproxEqual(right.Length, left.Length));
    }

    [Test]
    public void SplitMultiSegmentCurve()
    {
        Curve curve = new(new(0, stride, 2 * stride));
        float decrement = curve.Length;
        curve.Add(new(new(2 * stride, 3 * stride, 4 * stride)));
        float combinedLength = curve.Length;
        curve.Split(decrement * 1.5f, out Curve left, out Curve right);

        Assert.AreEqual(combinedLength, curve.Length);
        Assert.True(IsApproxEqual(decrement * 1.5f, left.Length));
        Assert.True(IsApproxEqual(decrement * 0.5f, right.Length));
    }

    [Test]
    public void AddAndSplitMultipleTimes()
    {
        Curve curve = new(new(0, stride, 2 * stride));
        float singleSegmentLength = curve.Length;
        curve.Add(new(new(2 * stride, 3 * stride, 4 * stride)));
        curve.Split(singleSegmentLength, out Curve left, out Curve right);
        for (int i = 0; i < 3; i++)
        {
            left.Add(right);
            left.Split(singleSegmentLength, out left, out right);
        }
        Assert.True(IsApproxEqual(left.Length, right.Length));
    }

    [Test]
    public void SplitLongCurve()
    {
        float longLength = 2000;
        float3 up = new(0, 0, 1);
        Curve curve = new(new(0, up * longLength / 2, up * longLength));
        curve.Split(1, out Curve left, out Curve right);

        Assert.IsTrue(IsApproxEqual(1, left.Length));
        Assert.IsTrue(IsApproxEqual(longLength - 1, right.Length));
    }

    [Test]
    public void GetNearestPointLongCurve()
    {
        float longLength = 2000;
        float3 up = new(0, 0, 1);
        Curve curve = new(new(0, up * longLength / 2, up * longLength));
        curve.GetNearestDistance(new Ray(up + new float3(0, 1, 0), new(0, -1, 0)), out float distanceOnCurve);

        Assert.IsTrue(IsApproxEqual(1, distanceOnCurve));
    }

    [Test]
    public void ReverseTest()
    {
        float3 up = new(0, 0, 500);
        float3 right = new(500, 0, 0);
        Curve curve = new(new(0, up, up + right));
        Curve reversed = curve.Duplicate().Offset(1).Reverse();

        Assert.AreEqual(curve.StartPos + curve.Start2DNormal, reversed.EndPos);
        Assert.AreEqual(curve.EndPos + curve.End2DNormal, reversed.StartPos);
    }

    [Test]
    public void GetNearestPointAtEnd()
    {
        for (float length = 100; length < 2000; length += 50)
        {
            Curve curve = new(new(0, new float3(1, 0, 0) * length / 2, new float3(1, 0, 0) * length));
            curve.GetNearestDistance(new(new(length - 1, -1, 1), new(0, 1, 0)), out float distA);
            curve.GetNearestDistance(new(new(length - 2, -1, 1), new(0, 1, 0)), out float distB);

            Assert.AreNotEqual(distA, distB);
        }
    }

    public static bool IsApproxEqual(float3 a, float3 b)
    {
        return Vector3.Distance(a, b) < DefaultTolerance;
    }

    public static bool IsApproxEqual(float a, float b)
    {
        return Math.Abs(a - b) < DefaultTolerance;
    }

}