using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using SS3D.Logging;
using Object = UnityEngine.Object;

namespace SS3D.Data.AssetDatabases
{
    /// <summary>
    /// Dedupes concurrent Addressables loads by GUID key and ref-counts releases.
    /// </summary>
    public static class AssetProvider
    {
        private static IAssetLoadBackend _backend = new AddressablesLoadBackend();
        private static readonly Dictionary<string, SharedEntry> Entries = new();

        private sealed class SharedEntry
        {
            public Object Asset;
            public object ReleaseToken;
            public int RefCount;
            public UniTaskCompletionSource<bool> Loading;
        }

        /// <summary>
        /// Replace the load backend (EditMode tests). Clears live entries.
        /// </summary>
        public static void SetBackend([NotNull] IAssetLoadBackend backend)
        {
            ReleaseAll();
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <summary>
        /// Restore the production Addressables backend and clear state (EditMode tests).
        /// </summary>
        public static void ResetToAddressablesBackend()
        {
            ReleaseAll();
            _backend = new AddressablesLoadBackend();
        }

        public static async UniTask<AssetHandle<TAsset>> AcquireAsync<TAsset>([NotNull] string key)
            where TAsset : Object
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Asset key must be non-empty.", nameof(key));
            }

            if (Entries.TryGetValue(key, out SharedEntry existing))
            {
                if (existing.Loading != null)
                {
                    await existing.Loading.Task;
                }

                if (existing.Asset == null)
                {
                    throw new InvalidOperationException($"Asset key '{key}' failed to load.");
                }

                existing.RefCount++;
                return new AssetHandle<TAsset>(key, (TAsset)existing.Asset);
            }

            SharedEntry entry = new()
            {
                RefCount = 1,
                Loading = new UniTaskCompletionSource<bool>(),
            };
            Entries[key] = entry;

            try
            {
                (TAsset asset, object token) = await _backend.LoadAsync<TAsset>(key);
                entry.Asset = asset;
                entry.ReleaseToken = token;
                entry.Loading.TrySetResult(true);
                entry.Loading = null;
                return new AssetHandle<TAsset>(key, asset);
            }
            catch (Exception ex)
            {
                Entries.Remove(key);
                entry.Loading.TrySetException(ex);
                throw;
            }
        }

        /// <summary>
        /// Blocking acquire for startup preload (icons, etc.). Prefer <see cref="AcquireAsync{TAsset}"/> elsewhere.
        /// </summary>
        public static AssetHandle<TAsset> AcquireSync<TAsset>([NotNull] string key)
            where TAsset : Object
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Asset key must be non-empty.", nameof(key));
            }

            if (Entries.TryGetValue(key, out SharedEntry existing))
            {
                if (existing.Loading != null)
                {
                    existing.Loading.Task.GetAwaiter().GetResult();
                }

                existing.RefCount++;
                return new AssetHandle<TAsset>(key, (TAsset)existing.Asset);
            }

            SharedEntry entry = new() { RefCount = 1 };
            Entries[key] = entry;

            try
            {
                (TAsset asset, object token) = _backend.LoadSync<TAsset>(key);
                entry.Asset = asset;
                entry.ReleaseToken = token;
                return new AssetHandle<TAsset>(key, asset);
            }
            catch
            {
                Entries.Remove(key);
                throw;
            }
        }

        public static bool TryGetCached<TAsset>([NotNull] string key, out TAsset asset)
            where TAsset : Object
        {
            if (Entries.TryGetValue(key, out SharedEntry entry) && entry.Asset is TAsset typed)
            {
                asset = typed;
                return true;
            }

            asset = null;
            return false;
        }

        /// <summary>
        /// Load each key once and hold until <see cref="ReleaseAll"/> (do not Release the returned handles).
        /// </summary>
        public static void PreloadAndHoldSync([NotNull] IEnumerable<string> keys)
        {
            PreloadAndHoldSync<Object>(keys);
        }

        public static void PreloadAndHoldSync<TAsset>([NotNull] IEnumerable<string> keys)
            where TAsset : Object
        {
            foreach (string key in keys)
            {
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                try
                {
                    // Drop handle without Release — keeps RefCount >= 1 for the session.
                    _ = AcquireSync<TAsset>(key);
                }
                catch (Exception ex)
                {
                    Log.Error(typeof(AssetProvider), $"Failed to preload asset '{key}': {ex.Message}");
                }
            }
        }

        internal static void ReleaseShared([NotNull] string key)
        {
            if (!Entries.TryGetValue(key, out SharedEntry entry))
            {
                return;
            }

            entry.RefCount--;
            if (entry.RefCount > 0)
            {
                return;
            }

            if (entry.ReleaseToken != null)
            {
                _backend.Release(entry.ReleaseToken);
            }

            Entries.Remove(key);
        }

        public static void ReleaseAll()
        {
            foreach (KeyValuePair<string, SharedEntry> pair in Entries.ToList())
            {
                if (pair.Value.ReleaseToken != null)
                {
                    _backend.Release(pair.Value.ReleaseToken);
                }
            }

            Entries.Clear();
        }
    }
}
