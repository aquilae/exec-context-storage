using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Web;

namespace Aq.ExecutionContextStorage
{
    public class ExecContextStorage : IDisposable
    {
        public static ExecContextStorage Current
        {
            get {
                Guid? maybeStorageKey = null;
                try {
                    var httpContext = HttpContext.Current;
                    if (httpContext != null) {
                        maybeStorageKey = (Guid) httpContext.Items[CONTEXT_KEY];
                    }
                }
                catch {
                    maybeStorageKey = null;
                }

                Guid storageKey;
                if (maybeStorageKey == null) {
                    storageKey = (Guid) CallContext.LogicalGetData(CONTEXT_KEY);
                }
                else {
                    storageKey = maybeStorageKey.Value;
                    CallContext.LogicalSetData(CONTEXT_KEY, storageKey);
                }

                return Storages[storageKey];
            }
        }

        public static void Init(HttpContext httpContext = null)
        {
            var storageKey = Guid.NewGuid();
            Storages[storageKey] = new ExecContextStorage();
            CallContext.LogicalSetData(CONTEXT_KEY, storageKey);
            if (httpContext != null) {
                httpContext.Items[CONTEXT_KEY] = storageKey;
            }
        }

        public static void Free(HttpContext httpContext = null)
        {
            Guid? maybeStorageKey = null;
            if (httpContext != null) {
                try {
                    maybeStorageKey = (Guid) httpContext.Items[CONTEXT_KEY];
                }
                catch {
                    maybeStorageKey = null;
                }
            }

            if (maybeStorageKey == null) {
                maybeStorageKey = (Guid?) CallContext.LogicalGetData(CONTEXT_KEY);
            }
            
            if (maybeStorageKey != null) {
                var storageKey = maybeStorageKey.Value;
                var storage = Storages[storageKey];
                Storages.Remove(storageKey);
                storage.Dispose();
            }
        }

        public object this[string key]
        {
            get { return this.Get(key); }
            set {
                var disposable = value as IDisposable;
                if (disposable == null) {
                    this.Set(key, value);
                }
                else {
                    this.Set(key, disposable);
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref this._disposed, 1, 0) == 0) {
                foreach (var tuple in this._data.Values) {
                    if (tuple.Item1) {
                        ((IDisposable) tuple.Item2).Dispose();
                    }
                }
                this._data.Clear();
            }
        }

        public object Get(string key)
        {
            Tuple<bool, object> tuple;
            if (this._data.TryGetValue(key, out tuple)) {
                return tuple.Item2;
            }
            return null;
        }

        public bool TryGet(string key, out object item)
        {
            Tuple<bool, object> tuple;
            if (this._data.TryGetValue(key, out tuple)) {
                item = tuple.Item2;
                return true;
            }
            item = null;
            return false;
        }

        public bool TryGet<T>(string key, out T item)
        {
            object obj;
            if (this.TryGet(key, out obj)) {
                if (obj is T) {
                    item = (T) obj;
                    return true;
                }
            }
            item = default (T);
            return false;
        }

        public void Set(string key, object item)
        {
            this._data[key] = Tuple.Create(false, item);
        }

        public void Set(string key, IDisposable item)
        {
            this._data[key] = Tuple.Create(true, (object) item);
        }

        private const string CONTEXT_KEY = "{E75F273E-AFEA-42C8-9EA2-804231B4FC29}";

        private static readonly IDictionary<Guid, ExecContextStorage>
            Storages = new ConcurrentDictionary<Guid, ExecContextStorage>();

        private readonly IDictionary<string, Tuple<bool, object>> _data;
        private int _disposed;

        private ExecContextStorage()
        {
            this._data = new ConcurrentDictionary<string, Tuple<bool, object>>();
        }
    }
}
