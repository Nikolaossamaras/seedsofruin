using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoR.Core
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, IService> _services = new();

        public static void Register<T>(T service) where T : IService
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Service {type.Name} already registered. Overwriting.");
                _services[type].Dispose();
            }

            _services[type] = service;
            service.Initialize();
        }

        public static T Resolve<T>() where T : IService
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var service))
                return (T)service;

            throw new InvalidOperationException($"[ServiceLocator] Service {type.Name} not registered.");
        }

        public static bool TryResolve<T>(out T service) where T : IService
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var found))
            {
                service = (T)found;
                return true;
            }

            service = default;
            return false;
        }

        public static void Release<T>() where T : IService
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var service))
            {
                service.Dispose();
                _services.Remove(type);
            }
        }

        public static void Clear()
        {
            foreach (var service in _services.Values)
                service.Dispose();

            _services.Clear();
        }
    }
}
