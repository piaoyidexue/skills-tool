using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

public class VFXObjectPool
{
    private readonly Dictionary<string, IEntry> _entries = new();

    public void Register(string key, ParticleSystem prefab)
    {
        _entries[key] = new ParticleEntry(() => Object.Instantiate(prefab));
    }

    public void Register(string key, VFXBase prefab)
    {
        _entries[key] = new BehaviourEntry(() => Object.Instantiate(prefab));
    }

    public void Prewarm(string key, int count)
    {
        if (_entries.TryGetValue(key, out var entry)) entry.Prewarm(count);
    }

    public bool TryPlay(VFXRequest request, float duration, MonoBehaviour host)
    {
        if (_entries.TryGetValue(request.VFXKey, out var entry))
        {
            var ct = host != null ? host.GetCancellationTokenOnDestroy() : CancellationToken.None;
            return entry.TryPlay(request, duration, ct);
        }

        return false;
    }

    private interface IEntry
    {
        void Prewarm(int count);
        bool TryPlay(VFXRequest request, float duration, CancellationToken ct);
    }

    private sealed class ParticleEntry : IEntry
    {
        private readonly ObjectPool<ParticleSystem> _pool;

        public ParticleEntry(Func<ParticleSystem> factory)
        {
            _pool = new ObjectPool<ParticleSystem>(
                factory,
                ps => ps.gameObject.SetActive(true),
                ps =>
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.transform.SetParent(null);
                    ps.gameObject.SetActive(false);
                },
                ps => Object.Destroy(ps.gameObject),
                false,
                8,
                64);
        }

        public void Prewarm(int count)
        {
            var list = new List<ParticleSystem>(count);
            for (var i = 0; i < count; i++) list.Add(_pool.Get());

            foreach (var item in list) _pool.Release(item);
        }

        public bool TryPlay(VFXRequest request, float duration, CancellationToken ct)
        {
            var ps = _pool.Get();
            ps.transform.position = request.Position;
            ps.transform.rotation = request.Direction.sqrMagnitude > 0f
                ? Quaternion.LookRotation(request.Direction)
                : Quaternion.identity;
            ps.transform.SetParent(request.Parent);
            ps.Play(true);
            ReleaseAfterAsync(ps, duration, ct);
            return true;
        }

        private async UniTaskVoid ReleaseAfterAsync(ParticleSystem ps, float duration, CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
            _pool.Release(ps);
        }
    }

    private sealed class BehaviourEntry : IEntry
    {
        private readonly ObjectPool<VFXBase> _pool;

        public BehaviourEntry(Func<VFXBase> factory)
        {
            _pool = new ObjectPool<VFXBase>(
                factory,
                vfx => vfx.gameObject.SetActive(true),
                vfx =>
                {
                    vfx.Stop();
                    vfx.transform.SetParent(null);
                    vfx.gameObject.SetActive(false);
                },
                vfx => Object.Destroy(vfx.gameObject),
                false,
                8,
                64);
        }

        public void Prewarm(int count)
        {
            var list = new List<VFXBase>(count);
            for (var i = 0; i < count; i++) list.Add(_pool.Get());

            foreach (var item in list) _pool.Release(item);
        }

        public bool TryPlay(VFXRequest request, float duration, CancellationToken ct)
        {
            var vfx = _pool.Get();
            vfx.Play(request);
            ReleaseAfterAsync(vfx, duration, ct);
            return true;
        }

        private async UniTaskVoid ReleaseAfterAsync(VFXBase vfx, float duration, CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
            _pool.Release(vfx);
        }
    }
}