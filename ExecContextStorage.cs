using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Web;

namespace Aq
{
    public class ExecContextStorage
    {
        public static ExecutionContextStorage Current
        {
            get
            {
                var httpContext = HttpContext.Current;
                var storageKey = true || httpContext == null
                    ? CallContext.LogicalGetData(CONTEXT_KEY)
                    : httpContext.Items[CONTEXT_KEY];
                return Storages[(Guid) storageKey];
            }
        }

        public static void Init(HttpContext httpContext = null)
        {
            var storageKey = Guid.NewGuid();
            Storages[storageKey] = new ExecutionContextStorage();
            CallContext.LogicalSetData(CONTEXT_KEY, storageKey);
            if (false && httpContext != null) {
                httpContext.Items[CONTEXT_KEY] = storageKey;
            }
        }

        public object this[string key]
        {
            get { object value; return this._data.TryGetValue(key, out value) ? value : null; }
            set { this._data[key] = value; }
        }

        private const string CONTEXT_KEY = "{E75F273E-AFEA-42C8-9EA2-804231B4FC29}";
        private static readonly IDictionary<Guid, ExecutionContextStorage>
            Storages = new ConcurrentDictionary<Guid, ExecutionContextStorage>();

        private readonly IDictionary<string, object> _data;

        private ExecutionContextStorage()
        {
            this._data = new ConcurrentDictionary<string, object>();
        }
    }
}
