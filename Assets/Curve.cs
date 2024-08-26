namespace Offsetted.Curve
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.Splines;


    /// <summary>
    /// Container for Unity Spline's BezierCurve to allow offsetting/Stroking
    /// </summary>
    public sealed class Curve
    {
        BezierCurve bCurve;
        float bCurveLength;
        float offsetDistance;
        float startDistance, endDistance;
        float startT = 0, endT = 1;
        Curve nextCurve;
        DistanceToInterpolation[] lut;
        float SegmentLength { get => bCurveLength - startDistance - endDistance; }
        /// <summary>
        /// Length of curve
        /// </summary>
        public float Length { get => GetLength(); }
        /// <summary>
        /// Starting position of curve
        /// </summary>
        public float3 StartPos { get => GetStartPos(); }
        /// <summary>
        /// Ending position of curve
        /// </summary>
        public float3 EndPos { get => GetEndPos(); }
        /// <summary>
        /// Starting tangent of curve
        /// </summary>
        public float3 StartTangent { get => GetStartTangent(); }
        /// <summary>
        /// Ending tangent of curve
        /// </summary>
        public float3 EndTangent { get => GetEndTangent(); }
        /// <summary>
        /// Starting normalized 2D (xz plane) normal of curve
        /// </summary>
        public float3 Start2DNormal { get => GetStartNormal(); }
        /// <summary>
        /// Ending normalized 2D (xz plane) normal of curve
        /// </summary>
        public float3 End2DNormal { get => GetEndNormal(); }

        const float getNearestPointTolerance = 0.001f;
        const float minimumCurveLength = 0.005f;
        const int distanceToInterpolationCacheSize = 30;

        private Curve() { }

        #region Getters
        Curve GetLastCurve()
        {
            Curve c = this;
            while (c.nextCurve != null && c.nextCurve.SegmentLength != 0)
                c = c.nextCurve;
            return c;
        }

        float3 GetStartPos()
        {
            if (SegmentLength == 0)
                return nextCurve.GetStartPos();
            return CurveUtility.EvaluatePosition(bCurve, startT) + Start2DNormal * offsetDistance;
        }

        float3 GetEndPos()
        {
            Curve last = GetLastCurve();
            return CurveUtility.EvaluatePosition(last.bCurve, last.endT) + last.End2DNormal * last.offsetDistance;
        }

        float3 GetStartTangent()
        {
            return CurveUtility.EvaluateTangent(bCurve, startT);
        }

        float3 GetEndTangent()
        {
            Curve last = GetLastCurve();
            return CurveUtility.EvaluateTangent(last.bCurve, last.endT);
        }

        float3 GetStartNormal()
        {
            return Normalized2DNormal(bCurve, startT);
        }

        float3 GetEndNormal()
        {
            Curve last = GetLastCurve();
            return Normalized2DNormal(last.bCurve, last.endT);
        }

        float3 Normalized2DNormal(BezierCurve bezierCurve, float t)
        {
            float3 tangent = CurveUtility.EvaluateTangent(bezierCurve, t);
            tangent.y = 0;
            return Vector3.Cross(tangent, Vector3.up).normalized;
        }

        float GetLength()
        {
            if (nextCurve != null)
                return SegmentLength + nextCurve.Length;
            return SegmentLength;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Containerize a BeizerCurve to allow offsetting/stroking
        /// </summary>
        /// <param name="beizerCurve">BeizerCurve to offset</param>
        public Curve(BezierCurve beizerCurve)
        {
            bCurve = beizerCurve;
            bCurveLength = CurveUtility.CalculateLength(bCurve);
            CreateDistanceCache();
        }

        /// <summary>
        /// Creates a deep copy of the current curve
        /// </summary>
        /// <returns>Deep copy</returns>
        public Curve Duplicate()
        {
            Curve newCurve = DuplicateHelper();
            if (newCurve.nextCurve != null)
                newCurve.nextCurve = newCurve.nextCurve.Duplicate();

            return newCurve;

            Curve DuplicateHelper()
            {
                Curve newCurve = new()
                {
                    bCurve = bCurve,
                    bCurveLength = bCurveLength,
                    offsetDistance = offsetDistance,
                    startDistance = startDistance,
                    endDistance = endDistance,
                    startT = startT,
                    endT = endT,
                    nextCurve = nextCurve,
                    lut = lut
                };
                return newCurve;
            }
        }

        /// <summary>
        /// Remove a portion of the curve a given distance from the start
        /// </summary>
        /// <param name="distance">Distance from start</param>
        /// <returns>Truncated Curve</returns>
        /// <exception cref="ArgumentException">Given distance is negative</exception>
        public Curve AddStartDistance(float distance)
        {
            if (distance < 0)
                throw new ArgumentException("distance cannot be negative", "distance");
            Curve curr = this;
            while (curr.SegmentLength < distance)
            {
                distance -= curr.SegmentLength;
                curr = curr.nextCurve;
            }
            curr.startDistance += distance;
            curr.startT = CurveUtility.GetDistanceToInterpolation(curr.lut, curr.startDistance);
            return curr;
        }

        /// <summary>
        /// Remove a portion of the curve a given distance from the end
        /// </summary>
        /// <param name="distance">Distance from end</param>
        /// <returns>Truncated Curve</returns>
        /// <exception cref="ArgumentException">Given distance is negative</exception>
        public Curve AddEndDistance(float distance)
        {
            if (distance < 0)
                throw new ArgumentException("distance cannot be negative", "distance");
            int index = GetChainLength() - 1;
            Curve curr = GetCurveByIndex(index);
            while (curr.SegmentLength < distance)
            {
                distance -= curr.SegmentLength;
                curr = GetCurveByIndex(--index);
                curr.nextCurve = null;
            }
            curr.endDistance += distance;
            curr.endT = CurveUtility.GetDistanceToInterpolation(curr.lut, curr.bCurveLength - curr.endDistance);
            return this;
        }

        /// <summary>
        /// Reverse the direction of the curve
        /// </summary>
        /// <returns>The reversed curve</returns>
        public Curve Reverse()
        {
            Curve newHead = ReverseLinkedList(this);

            newHead = ReverseHelper(newHead);
            return newHead;

            static Curve ReverseLinkedList(Curve head)
            {
                if (head == null || head.nextCurve == null)
                    return head;
                Curve prev = null;
                Curve curr = head;
                while (curr != null)
                {
                    Curve next = curr.nextCurve;
                    curr.nextCurve = prev;
                    prev = curr;
                    curr = next;
                }

                return prev;
            }

            static Curve ReverseHelper(Curve curve)
            {
                if (curve == null)
                    return null;
                Curve reversed = new()
                {
                    bCurve = curve.bCurve.GetInvertedCurve(),
                    startDistance = curve.endDistance,
                    endDistance = curve.startDistance,
                    bCurveLength = curve.bCurveLength,
                    offsetDistance = -curve.offsetDistance,
                    nextCurve = curve.nextCurve
                };
                reversed.nextCurve = ReverseHelper(reversed.nextCurve);
                reversed.CreateDistanceCache();
                reversed.startT = reversed.GetDistanceToInterpolation(curve.startDistance);
                reversed.endT = reversed.GetDistanceToInterpolation(curve.bCurveLength - curve.endDistance);
                return reversed;
            }
        }

        /// <summary>
        /// Offset the curve a given distance with respect to its 2D normal
        /// </summary>
        /// <param name="distance">Offset distance</param>
        /// <returns>Offsetted Curve</returns>
        public Curve Offset(float distance)
        {
            Curve curve = this;
            while (curve != null)
            {
                curve.offsetDistance += distance;
                curve = curve.nextCurve;
            }
            return this;
        }

        /// <summary>
        /// Get an IEnumerable of equally distanced points on the curve
        /// </summary>
        /// <param name="numPoints"></param>
        /// <returns></returns>
        public OutlineEnum GetOutline(int numPoints)
        {
            return new(this, numPoints);
        }

        /// <summary>
        /// Add the start of another curve to the end of this curve
        /// </summary>
        /// <param name="other">The other curve</param>
        public void Add(Curve other)
        {
            Curve last = GetLastCurve();
            last.nextCurve = other.Duplicate();
        }

        /// <summary>
        /// Evaluate the position of a given distance on curve 
        /// </summary>
        /// <param name="distance">The given distance</param>
        /// <returns>Position on curve</returns>
        /// <exception cref="ArgumentException">Given distance is negative</exception>
        public float3 EvaluatePosition(float distance)
        {
            if (distance < 0)
                throw new ArgumentException("distance cannot be negative", "distance");
            if (distance > SegmentLength && nextCurve != null)
                return nextCurve.EvaluatePosition(distance - SegmentLength);
            float t = CurveUtility.GetDistanceToInterpolation(lut, startDistance + distance);
            if (offsetDistance == 0)
                return CurveUtility.EvaluatePosition(bCurve, t);
            return CurveUtility.EvaluatePosition(bCurve, t) + Normalized2DNormal(bCurve, t) * offsetDistance;
        }

        /// <summary>
        /// Evaluate the tangent of a given distance on curve
        /// </summary>
        /// <param name="distance">The given distance</param>
        /// <returns>Tangent</returns>
        /// <exception cref="ArgumentException">Given distance is negative</exception>
        public float3 EvaluateTangent(float distance)
        {
            if (distance < 0)
                throw new ArgumentException("distance cannot be negative", "distance");
            if (distance > SegmentLength && nextCurve != null)
                return nextCurve.EvaluateTangent(distance - SegmentLength);
            float t = CurveUtility.GetDistanceToInterpolation(lut, startDistance + distance);
            return CurveUtility.EvaluateTangent(bCurve, t);
        }

        /// <summary>
        /// Evaluate the normalized 2D (xz plane) normal of a given distance on curve
        /// </summary>
        /// <param name="distance">The given distance</param>
        /// <returns>Normalized 2D nomral</returns>
        /// <exception cref="ArgumentException">Given distance is negative</exception>
        public float3 Evaluate2DNormal(float distance)
        {
            if (distance < 0)
                throw new ArgumentException("distance cannot be negative", "distance");
            if (distance > SegmentLength && nextCurve != null)
                return nextCurve.Evaluate2DNormal(distance - SegmentLength);
            float t = CurveUtility.GetDistanceToInterpolation(lut, startDistance + distance);
            return math.normalize(Normalized2DNormal(bCurve, t));
        }

        /// <summary>
        /// Compute the minimum distance between the curve and a given ray
        /// </summary>
        /// <param name="ray">The given ray</param>
        /// <param name="distanceOnCurve">The distance on curve when the said minimum occurs</param>
        /// <param name="resolution">Higher resolution prevents local minimum from being mistaken as global minimum</param>
        /// <returns>The minimum distance between the curve and a given ray</returns>
        public float GetNearestDistance(Ray ray, out float distanceOnCurve, int resolution = 10)
        {
            float minDistance = float.MaxValue;
            distanceOnCurve = 0;
            float distanceStep = Length / resolution;
            float localMin = 0;
            while (distanceOnCurve <= Length)
            {
                float3 pos = EvaluatePosition(distanceOnCurve);
                float distance = GetDistanceToCurve(pos);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    localMin = distanceOnCurve;
                }
                distanceOnCurve += distanceStep;
            }
            float low = localMin - distanceStep >= 0 ? localMin - distanceStep : 0;
            float high = localMin + distanceStep <= Length ? localMin + distanceStep : Length;
            do
            {
                float mid = (low + high) / 2;
                if (GetDistanceToCurve(EvaluatePosition(mid - getNearestPointTolerance))
                    < GetDistanceToCurve(EvaluatePosition(mid + getNearestPointTolerance)))
                    high = mid;
                else
                    low = mid;
            } while (high - low > getNearestPointTolerance);

            distanceOnCurve = low;
            return GetDistanceToCurve(EvaluatePosition(low));

            float GetDistanceToCurve(float3 pos)
            {
                return Vector3.Cross(ray.direction, (Vector3)pos - ray.origin).magnitude;
            }
        }

        /// <summary>
        /// Split the curve with a given distance on curve
        /// </summary>
        /// <param name="distance">The given distance</param>
        /// <param name="left">Left curve</param>
        /// <param name="right">Right curve</param>
        /// <exception cref="ArgumentException">Split distance is greater than curve length</exception>
        public void Split(float distance, out Curve left, out Curve right)
        {
            if (distance >= Length)
                throw new ArgumentException("distance has to be smaller than curve length", "distance");
            int index = 0;
            Curve toSplit = this;
            float currDistance = distance;

            while (true)
            {
                if (Math.Abs(currDistance - toSplit.SegmentLength) < minimumCurveLength)
                {
                    left = this;
                    right = toSplit.nextCurve;
                    toSplit.nextCurve = null;
                    return;
                }
                if (currDistance >= toSplit.SegmentLength)
                {
                    currDistance -= toSplit.SegmentLength;
                    toSplit = toSplit.nextCurve;
                    index++;
                }
                else
                    break;
            }

            CurveUtility.Split(toSplit.bCurve, toSplit.GetDistanceToInterpolation(currDistance), out BezierCurve l, out BezierCurve r);
            left = new(l) { offsetDistance = offsetDistance };
            left = left.AddStartDistance(startDistance);
            right = new(r) { offsetDistance = offsetDistance };
            right = right.AddEndDistance(endDistance);

            if (index == 0)
            {
                right.nextCurve = nextCurve;
                return;
            }
            Curve newHead = Duplicate();
            Curve prev = newHead;
            while (index != 1)
            {
                prev = prev.nextCurve;
                index--;
            }
            Curve next = prev.nextCurve.nextCurve;
            prev.nextCurve = left;
            left = newHead;
            right.nextCurve = next;
        }

        #endregion


        private void CreateDistanceCache()
        {
            lut = new DistanceToInterpolation[distanceToInterpolationCacheSize];
            CurveUtility.CalculateCurveLengths(bCurve, lut);
        }

        private Curve GetCurveByIndex(int index)
        {
            Curve curve = this;
            for (int i = 0; i < index; i++)
                curve = curve.nextCurve;
            return curve;
        }

        private int GetChainLength()
        {
            int count = 0;
            Curve curve = this;
            while (curve != null)
            {
                count++;
                curve = curve.nextCurve;
            }
            return count;
        }

        float GetDistanceToInterpolation(float distance)
        {
            return CurveUtility.GetDistanceToInterpolation(lut, distance);
        }

        public override bool Equals(object obj)
        {
            if (obj is Curve other)
                return bCurve.P0.Equals(other.bCurve.P0) && bCurve.P1.Equals(other.bCurve.P1)
                    && bCurve.P2.Equals(other.bCurve.P2) && bCurve.P3.Equals(other.bCurve.P3) && bCurveLength == other.bCurveLength
                    && offsetDistance == other.offsetDistance && startDistance == other.startDistance && endDistance == other.endDistance
                    && startT == other.startT && endT == other.endT && !(nextCurve == null ^ other.nextCurve == null)
                    && ((nextCurve == null && other.nextCurve == null) || nextCurve.Equals(other.nextCurve));
            else
                return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class OutlineEnum : IEnumerable<float3>
    {
        readonly Curve curve;
        readonly float numPoints;

        public OutlineEnum(Curve curve, float numPoints)
        {
            this.curve = curve;
            this.numPoints = numPoints;
        }

        public IEnumerator<float3> GetEnumerator()
        {
            float pointSeparation = curve.Length / (numPoints - 1);
            int count = 0;
            float currDistance = 0;
            while (count++ < numPoints)
            {
                yield return curve.EvaluatePosition(currDistance);
                currDistance += pointSeparation;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}