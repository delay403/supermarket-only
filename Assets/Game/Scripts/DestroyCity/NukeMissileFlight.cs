using System;
using System.Collections;
using UnityEngine;

namespace Game.DestroyCity
{
    [Serializable]
    public struct NukeMissileFlightConfig
    {
        [Tooltip("总飞行时长（秒）。")]
        public float totalDuration;

        [Tooltip("轨迹最高点附加高度（米）。")]
        public float arcHeight;

        [Range(0.05f, 0.9f)]
        public float stage1Ratio;

        [Range(0.05f, 0.9f)]
        public float stage2Ratio;

        [Tooltip("下落阶段可偏移落点的最大水平半径（米）。")]
        public float descentSteerRadius;

        [Tooltip("下落阶段弧长推进倍率（0.2 = 1/5 速度）。")]
        public float descentSpeedScale;

        [Tooltip("落点标记刷新间隔（秒）。")]
        public float landingMarkerInterval;

        public static NukeMissileFlightConfig Default => new NukeMissileFlightConfig
        {
            totalDuration = 3.5f,
            arcHeight = 55f,
            stage1Ratio = 0.25f,
            stage2Ratio = 0.5f,
            descentSteerRadius = 25f,
            descentSpeedScale = 0.2f,
            landingMarkerInterval = 0.12f,
        };

        public float Stage3Ratio => Mathf.Clamp01(1f - stage1Ratio - stage2Ratio);
    }

    /// <summary>核弹三段式飞行：上升 → 巡航 → 下落（弧长匀速，阶段切换速度连续）。</summary>
    public static class NukeMissileFlight
    {
        public enum FlightPhase
        {
            Ascent = 0,
            Cruise = 1,
            Descent = 2,
        }

        const int ArcTableSamples = 96;

        const float Stage1HorizShare = 0.10f;
        const float Stage2HorizShare = 0.80f;
        const float Stage3HorizShare = 0.10f;

        const float PitchStage1End = 39f;
        /// <summary>巡航前半段结束时的俯仰（后半段再抬向 135°）。</summary>
        const float PitchCruiseMid = 92f;
        const float PitchDescentStart = 150f;
        const float PitchFinal = 165f;

        public static Quaternion ComputeLaunchRotation(Vector3 start, Vector3 target)
        {
            var forward = FlatForward(start, target);
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        public static IEnumerator CoFlyThreeStage(
            Transform missile,
            Vector3 start,
            Vector3 initialTarget,
            Transform steerTarget,
            NukeMissileFlightConfig config,
            Action<Vector3> onLanded,
            Action<FlightPhase> onPhaseChanged = null,
            Action<Vector3> onLandingPredicted = null)
        {
            if (missile == null)
            {
                onLanded?.Invoke(initialTarget);
                yield break;
            }

            config = SanitizeConfig(config);
            var baseRot = ComputeLaunchRotation(start, initialTarget);
            missile.SetPositionAndRotation(start, baseRot);

            var duration = Mathf.Max(0.2f, config.totalDuration);
            var stage1End = config.stage1Ratio;
            var stage2End = stage1End + config.stage2Ratio;
            var plannedLanding = initialTarget;
            var steerAnchor = new Vector3(plannedLanding.x, plannedLanding.y, plannedLanding.z);
            var descentSpeedScale = Mathf.Clamp(config.descentSpeedScale, 0.05f, 1f);
            var markerInterval = Mathf.Max(0.05f, config.landingMarkerInterval);

            var landing = plannedLanding;
            var arcTable = BuildArcLengthTable(
                start, landing, config.arcHeight, stage1End, stage2End, out var pathLength);
            var tableLanding = landing;
            var cruiseSpeed = pathLength / duration;

            var traveled = 0f;
            var landedAt = landing;
            var lastPhase = FlightPhase.Ascent;
            var nextMarkerUpdate = 0f;
            const float LandingRebuildThresholdSq = 0.04f;

            while (traveled < pathLength * 4f)
            {
                if (traveled >= pathLength - 0.01f)
                    break;

                var pathU = SolvePathUFromDistance(arcTable, traveled);

                if (pathU >= stage2End && steerTarget != null)
                {
                    var steered = ClampSteerTarget(
                        steerTarget.position,
                        steerAnchor,
                        config.descentSteerRadius);
                    if ((steered - tableLanding).sqrMagnitude > LandingRebuildThresholdSq)
                    {
                        landing = steered;
                        arcTable = BuildArcLengthTable(
                            start, landing, config.arcHeight, stage1End, stage2End, out pathLength);
                        tableLanding = landing;
                        pathU = SolvePathUFromDistance(arcTable, Mathf.Min(traveled, pathLength));
                    }
                }

                var phase = ResolveFlightPhase(pathU, stage1End, stage2End);
                if (phase != lastPhase)
                {
                    lastPhase = phase;
                    onPhaseChanged?.Invoke(phase);
                }

                if (phase == FlightPhase.Descent && onLandingPredicted != null && Time.time >= nextMarkerUpdate)
                {
                    var predicted = SamplePosition(start, landing, config.arcHeight, 1f, stage1End, stage2End);
                    onLandingPredicted.Invoke(predicted);
                    nextMarkerUpdate = Time.time + markerInterval;
                }

                var pos = SamplePosition(start, landing, config.arcHeight, pathU, stage1End, stage2End);
                missile.position = pos;

                var pitch = SamplePitch(pathU, stage1End, stage2End);
                var yawRot = SampleYawRotation(pos, landing, baseRot, pathU, stage1End, stage2End);
                missile.rotation = yawRot * Quaternion.Euler(pitch, 0f, 0f);
                landedAt = landing;

                var speedMul = phase == FlightPhase.Descent ? descentSpeedScale : 1f;
                traveled += cruiseSpeed * speedMul * Time.deltaTime;
                yield return null;
            }

            var finalPos = SamplePosition(start, landedAt, config.arcHeight, 1f, stage1End, stage2End);
            missile.position = finalPos;
            var finalPitch = SamplePitch(1f, stage1End, stage2End);
            var finalYaw = SampleYawRotation(finalPos, landedAt, baseRot, 1f, stage1End, stage2End);
            missile.rotation = finalYaw * Quaternion.Euler(finalPitch, 0f, 0f);
            onLanded?.Invoke(landedAt);
        }

        public static FlightPhase ResolveFlightPhase(float pathU, float stage1End, float stage2End)
        {
            if (pathU < stage1End)
                return FlightPhase.Ascent;
            if (pathU < stage2End)
                return FlightPhase.Cruise;
            return FlightPhase.Descent;
        }

        static NukeMissileFlightConfig SanitizeConfig(NukeMissileFlightConfig config)
        {
            if (config.totalDuration <= 0f)
                config.totalDuration = NukeMissileFlightConfig.Default.totalDuration;
            if (config.arcHeight <= 0f)
                config.arcHeight = NukeMissileFlightConfig.Default.arcHeight;
            if (config.stage1Ratio <= 0f)
                config.stage1Ratio = 0.25f;
            if (config.stage2Ratio <= 0f)
                config.stage2Ratio = 0.5f;

            var sum = config.stage1Ratio + config.stage2Ratio;
            if (sum >= 0.98f)
            {
                config.stage1Ratio = 0.25f;
                config.stage2Ratio = 0.5f;
            }

            if (config.descentSteerRadius <= 0f)
                config.descentSteerRadius = NukeMissileFlightConfig.Default.descentSteerRadius;

            if (config.descentSpeedScale <= 0f)
                config.descentSpeedScale = NukeMissileFlightConfig.Default.descentSpeedScale;

            if (config.landingMarkerInterval <= 0f)
                config.landingMarkerInterval = NukeMissileFlightConfig.Default.landingMarkerInterval;

            return config;
        }

        static float[] BuildArcLengthTable(
            Vector3 start,
            Vector3 end,
            float arcHeight,
            float stage1End,
            float stage2End,
            out float totalLength)
        {
            var table = new float[ArcTableSamples + 1];
            table[0] = 0f;
            var prev = SamplePosition(start, end, arcHeight, 0f, stage1End, stage2End);

            for (var i = 1; i <= ArcTableSamples; i++)
            {
                var u = i / (float)ArcTableSamples;
                var pos = SamplePosition(start, end, arcHeight, u, stage1End, stage2End);
                table[i] = table[i - 1] + Vector3.Distance(prev, pos);
                prev = pos;
            }

            totalLength = Mathf.Max(0.001f, table[ArcTableSamples]);
            return table;
        }

        static float SolvePathUFromDistance(float[] cumulativeLengths, float distance)
        {
            distance = Mathf.Clamp(distance, 0f, cumulativeLengths[ArcTableSamples]);
            if (distance <= 0f)
                return 0f;

            var lo = 0;
            var hi = ArcTableSamples;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (cumulativeLengths[mid] < distance)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            if (lo == 0)
                return 0f;

            var lenBefore = cumulativeLengths[lo - 1];
            var lenAfter = cumulativeLengths[lo];
            var segment = Mathf.Max(0.00001f, lenAfter - lenBefore);
            var localT = (distance - lenBefore) / segment;
            var uBefore = (lo - 1) / (float)ArcTableSamples;
            var uAfter = lo / (float)ArcTableSamples;
            return Mathf.Lerp(uBefore, uAfter, localT);
        }

        static Vector3 SamplePosition(
            Vector3 start,
            Vector3 end,
            float arcHeight,
            float pathU,
            float stage1End,
            float stage2End)
        {
            pathU = Mathf.Clamp01(pathU);
            SamplePathShape(pathU, arcHeight, stage1End, stage2End, out var hProg, out var yBoost);

            var forward = FlatForward(start, end);
            var horizDist = HorizontalDistance(start, end);
            var groundY = Mathf.Lerp(start.y, end.y, hProg);
            var flat = start + forward * (horizDist * hProg);
            return new Vector3(flat.x, groundY + yBoost, flat.z);
        }

        static void SamplePathShape(
            float pathU,
            float arcHeight,
            float stage1End,
            float stage2End,
            out float hProg,
            out float yBoost)
        {
            stage1End = Mathf.Clamp(stage1End, 0.05f, 0.9f);
            stage2End = Mathf.Clamp(stage2End, stage1End + 0.05f, 0.95f);

            if (pathU <= stage1End)
            {
                var u = Smooth01(pathU / stage1End);
                hProg = Stage1HorizShare * u;
                yBoost = arcHeight * Smooth01(u) * 0.42f;
                return;
            }

            if (pathU <= stage2End)
            {
                var u = (pathU - stage1End) / (stage2End - stage1End);
                hProg = Stage1HorizShare + Stage2HorizShare * Smooth01(u);
                var peak = SmoothBell(u);
                yBoost = Mathf.Lerp(arcHeight * 0.42f, arcHeight, peak);
                yBoost = Mathf.Lerp(yBoost, arcHeight * 0.32f, Smooth01(Mathf.InverseLerp(0.55f, 1f, u)));
                return;
            }

            var u3 = (pathU - stage2End) / (1f - stage2End);
            hProg = Stage1HorizShare + Stage2HorizShare + Stage3HorizShare * Smooth01(u3);
            yBoost = arcHeight * 0.32f * (1f - Smooth01(u3));
        }

        static float SamplePitch(float pathU, float stage1End, float stage2End)
        {
            pathU = Mathf.Clamp01(pathU);
            stage1End = Mathf.Clamp(stage1End, 0.05f, 0.9f);
            stage2End = Mathf.Clamp(stage2End, stage1End + 0.05f, 0.95f);

            var cruiseMid = stage1End + (stage2End - stage1End) * 0.5f;
            var knotU = new[]
            {
                0f,
                stage1End,
                cruiseMid,
                stage2End,
                1f,
            };
            var knotPitch = new[]
            {
                0f,
                PitchStage1End,
                PitchCruiseMid,
                PitchDescentStart,
                PitchFinal,
            };

            return SampleCatmullRom1D(knotU, knotPitch, pathU);
        }

        static Quaternion SampleYawRotation(
            Vector3 pos,
            Vector3 landing,
            Quaternion baseRot,
            float pathU,
            float stage1End,
            float stage2End)
        {
            var toLanding = landing - pos;
            toLanding.y = 0f;
            if (toLanding.sqrMagnitude < 0.0001f)
                return baseRot;

            var landingRot = Quaternion.LookRotation(toLanding.normalized, Vector3.up);
            var blendStart = stage1End + (stage2End - stage1End) * 0.45f;
            if (pathU <= blendStart)
                return baseRot;

            var w = Smooth01((pathU - blendStart) / Mathf.Max(0.0001f, 1f - blendStart));
            return Quaternion.Slerp(baseRot, landingRot, w);
        }

        static float SampleCatmullRom1D(float[] knotU, float[] knotValue, float pathU)
        {
            if (knotU == null || knotValue == null || knotU.Length != knotValue.Length || knotU.Length < 2)
                return 0f;

            if (pathU <= knotU[0])
                return knotValue[0];
            if (pathU >= knotU[knotU.Length - 1])
                return knotValue[knotValue.Length - 1];

            for (var i = 0; i < knotU.Length - 1; i++)
            {
                if (pathU > knotU[i + 1])
                    continue;

                var span = Mathf.Max(0.00001f, knotU[i + 1] - knotU[i]);
                var t = (pathU - knotU[i]) / span;
                var p0 = i > 0 ? knotValue[i - 1] : knotValue[i];
                var p1 = knotValue[i];
                var p2 = knotValue[i + 1];
                var p3 = i + 2 < knotValue.Length ? knotValue[i + 2] : knotValue[i + 1];
                return CatmullRom(p0, p1, p2, p3, t);
            }

            return knotValue[knotValue.Length - 1];
        }

        static float CatmullRom(float p0, float p1, float p2, float p3, float t)
        {
            t = Mathf.Clamp01(t);
            var t2 = t * t;
            var t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static float Smooth01(float t) => t * t * (3f - 2f * t);

        static float SmoothBell(float t)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Sin(t * Mathf.PI);
        }

        static Vector3 ClampSteerTarget(Vector3 desired, Vector3 anchor, float maxRadius)
        {
            var delta = desired - anchor;
            delta.y = 0f;
            if (delta.sqrMagnitude <= maxRadius * maxRadius)
                return new Vector3(desired.x, anchor.y, desired.z);

            delta = delta.normalized * maxRadius;
            return new Vector3(anchor.x + delta.x, anchor.y, anchor.z + delta.z);
        }

        static Vector3 FlatForward(Vector3 from, Vector3 to)
        {
            var forward = to - from;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            return forward.normalized;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var dx = b.x - a.x;
            var dz = b.z - a.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
