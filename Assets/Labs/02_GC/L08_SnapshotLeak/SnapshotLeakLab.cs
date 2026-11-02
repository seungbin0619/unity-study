using System;
using System.Globalization;
using System.IO;
using Unity.Profiling.Memory;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;

namespace Labs.GarbageCollection
{
    /// <summary>
    /// LeakHolder를 Leaky(구독 해제 안 함) 50개, Clean(OnDestroy에서 해제) 50개 만들고 모두 Destroy한 뒤
    /// 스냅샷 A(생성 전) / B(Destroy + GC 후) / C(B 이후 Resources.UnloadUnusedAssets + GC)를 찍는다.
    /// 스냅샷과 별개로 그룹마다 GameObject(네이티브), LeakHolder·byte[]·Texture2D 래퍼(매니지드, WeakReference),
    /// Texture2D(네이티브)가 남았는지 세어 CSV에 남긴다.
    /// 스냅샷 경로: 플레이어 인자 -snapshotDir DIR (기본 Application.persistentDataPath).
    /// </summary>
    [Preserve, Lab("02-L08")]
    public sealed class SnapshotLeakLab : ILab
    {
        const int PerGroup = 50;
        static readonly string[] Groups = { "Leaky", "Clean" };

        // async 메서드의 지역 변수는 상태 머신 필드가 되어 객체를 붙잡으므로, 강한 참조는 필드에만 잠깐 두고 지운다
        GameObject[] _objects;
        readonly WeakReference[][] _holders = new WeakReference[2][];
        readonly WeakReference[][] _buffers = new WeakReference[2][];
        readonly WeakReference[][] _textures = new WeakReference[2][];

        public async Awaitable Run(LabContext ctx)
        {
            var dir = ReadArg("-snapshotDir") ?? Application.persistentDataPath;
            Directory.CreateDirectory(dir);
            using var metrics = new LabMetrics();
            ctx.Record("-", "metrics valid", metrics.Valid.ToString().ToLowerInvariant());

            await Settle();
            RecordMemory(ctx, "A", metrics);
            await Snapshot(ctx, dir, "A");

            Spawn();
            for (var i = 0; i < 3; i++) await Awaitable.NextFrameAsync();
            RecordMemory(ctx, "spawned", metrics);
            ctx.Record("spawned", "LeakEvents subscribers", LeakEvents.SubscriberCount.ToString(CultureInfo.InvariantCulture));

            DestroyAll();
            await Settle();
            RecordMemory(ctx, "B", metrics);
            await Snapshot(ctx, dir, "B");
            Check(ctx, "B");

            var unload = Resources.UnloadUnusedAssets();
            while (!unload.isDone) await Awaitable.NextFrameAsync();
            await Settle();
            RecordMemory(ctx, "C", metrics);
            await Snapshot(ctx, dir, "C");
            Check(ctx, "C");
        }

        void Spawn()
        {
            _objects = new GameObject[Groups.Length * PerGroup];
            for (var g = 0; g < Groups.Length; g++)
            {
                _holders[g] = new WeakReference[PerGroup];
                _buffers[g] = new WeakReference[PerGroup];
                _textures[g] = new WeakReference[PerGroup];
                for (var i = 0; i < PerGroup; i++)
                {
                    var go = new GameObject("L08_" + Groups[g] + "_" + i.ToString("D2", CultureInfo.InvariantCulture));
                    var holder = go.AddComponent<LeakHolder>();
                    holder.Unsubscribe = g == 1;
                    _holders[g][i] = new WeakReference(holder);
                    _buffers[g][i] = new WeakReference(holder.Buffer);
                    _textures[g][i] = new WeakReference(holder.Texture);
                    _objects[g * PerGroup + i] = go;
                }
            }
        }

        void DestroyAll()
        {
            foreach (var go in _objects) UnityEngine.Object.Destroy(go);
            _objects = null;
        }

        void Check(LabContext ctx, string stage)
        {
            ctx.Record(stage, "LeakEvents subscribers", LeakEvents.SubscriberCount.ToString(CultureInfo.InvariantCulture));
            var gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            for (var g = 0; g < Groups.Length; g++)
            {
                var prefix = "L08_" + Groups[g] + "_";
                var condition = stage + " " + Groups[g];

                var nativeGo = 0;
                foreach (var go in gameObjects) if (go.name.StartsWith(prefix, StringComparison.Ordinal)) nativeGo++;
                var nativeTex = 0;
                long texBytes = 0;
                foreach (var t in textures)
                {
                    if (!t.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    nativeTex++;
                    texBytes += Profiler.GetRuntimeMemorySizeLong(t);
                }

                int holderAlive = 0, holderUnityNull = 0, bufferAlive = 0, texWrapperAlive = 0;
                for (var i = 0; i < PerGroup; i++)
                {
                    if (_holders[g][i].Target is LeakHolder h)
                    {
                        holderAlive++;
                        if (h == null) holderUnityNull++; // 파괴된 래퍼(가짜 null)인지
                    }
                    if (_buffers[g][i].IsAlive) bufferAlive++;
                    if (_textures[g][i].IsAlive) texWrapperAlive++;
                }

                ctx.Record(condition, "1 GameObject native", nativeGo.ToString(CultureInfo.InvariantCulture));
                ctx.Record(condition, "2 LeakHolder managed alive|== null", holderAlive.ToString(CultureInfo.InvariantCulture) + "|" + holderUnityNull.ToString(CultureInfo.InvariantCulture));
                ctx.Record(condition, "3 byte[] managed alive", bufferAlive.ToString(CultureInfo.InvariantCulture));
                ctx.Record(condition, "4 Texture2D native|MB", nativeTex.ToString(CultureInfo.InvariantCulture) + "|" + Mb(texBytes));
                ctx.Record(condition, "4b Texture2D wrapper alive", texWrapperAlive.ToString(CultureInfo.InvariantCulture));
            }
            // FindObjectsOfTypeAll가 만든 래퍼를 놓아 다음 단계(C)의 판정에 섞이지 않게 한다
            gameObjects = null;
            textures = null;
            GC.Collect();
        }

        static async Awaitable Settle()
        {
            for (var i = 0; i < 3; i++) await Awaitable.NextFrameAsync();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            for (var i = 0; i < 2; i++) await Awaitable.NextFrameAsync();
        }

        static void RecordMemory(LabContext ctx, string stage, LabMetrics metrics)
        {
            ctx.Record(stage, "GC Used MB", Mb(metrics.GcUsedBytes));
            ctx.Record(stage, "native allocated MB", Mb(Profiler.GetTotalAllocatedMemoryLong()));
        }

        static async Awaitable Snapshot(LabContext ctx, string dir, string label)
        {
            var path = Path.Combine(dir, "02-L08_" + LabRunner.Backend + "_" + label + ".snap");
            if (File.Exists(path)) File.Delete(path);
            var done = false;
            var ok = false;
            MemoryProfiler.TakeSnapshot(path, (_, success) => { ok = success; done = true; },
                CaptureFlags.ManagedObjects | CaptureFlags.NativeObjects | CaptureFlags.NativeAllocations);
            while (!done) await Awaitable.NextFrameAsync();
            ctx.Record(label, "snapshot", (ok ? "ok " : "failed ") + path);
        }

        static string ReadArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        static string Mb(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture);
    }
}
