using System;
using Unity.Profiling;

namespace Labs
{
    /// <summary>
    /// ProfilerRecorder 래퍼. 프레임 단위 카운터(LastFrame*)는 "직전에 끝난 프레임"의 값이므로,
    /// 측정할 코드를 프레임 F에서 돌렸다면 프레임 F+1에서 읽는다.
    /// Development 플레이어와 에디터에서만 유효하다(Valid로 확인).
    /// </summary>
    public sealed class LabMetrics : IDisposable
    {
        ProfilerRecorder _gcAllocBytes;
        ProfilerRecorder _gcAllocCount;
        ProfilerRecorder _gcUsed;
        ProfilerRecorder _gcReserved;

        public LabMetrics()
        {
            _gcAllocBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcAllocCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
            _gcUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            _gcReserved = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
        }

        public bool Valid => _gcAllocBytes.Valid && _gcAllocCount.Valid;

        public long LastFrameGcAllocBytes => _gcAllocBytes.LastValue;
        public long LastFrameGcAllocCount => _gcAllocCount.LastValue;
        public long GcUsedBytes => _gcUsed.CurrentValue;
        public long GcReservedBytes => _gcReserved.CurrentValue;

        public void Dispose()
        {
            _gcAllocBytes.Dispose();
            _gcAllocCount.Dispose();
            _gcUsed.Dispose();
            _gcReserved.Dispose();
        }
    }
}
