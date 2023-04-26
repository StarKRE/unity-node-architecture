using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace GameNodes
{
    public class GameNode : MonoBehaviour
    {
        private GameNode parent;

        [SerializeField]
        private List<GameNode> children;

        private readonly List<object> services = new();

        private readonly List<IGameUpdater> updaters = new();

        private readonly List<IGameFixedUpdater> fixedUpdaters = new();

        private readonly List<IGameLateUpdater> lateUpdaters = new();

        private bool installed;

        private void Update()
        {
            if (!this.installed)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            for (int i = 0, count = this.updaters.Count; i < count; i++)
            {
                var listener = this.updaters[i];
                listener.Update(deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (!this.installed)
            {
                return;
            }

            var deltaTime = Time.fixedDeltaTime;
            for (int i = 0, count = this.fixedUpdaters.Count; i < count; i++)
            {
                var listener = this.fixedUpdaters[i];
                listener.FixedUpdate(deltaTime);
            }
        }

        private void LateUpdate()
        {
            if (!this.installed)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            for (int i = 0, count = this.lateUpdaters.Count; i < count; i++)
            {
                var listener = this.lateUpdaters[i];
                listener.LateUpdate(deltaTime);
            }
        }

        [ContextMenu("Install")]
        public Task InstallAsync()
        {
            return Task.Run(this.Install);
        }

        [ContextMenu("Install")]
        public void Install()
        {
            if (this.installed)
            {
                Debug.LogWarning($"Game Node {this.name} is already installed", this);
                return;
            }

            this.installed = true;
            
            foreach (var service in this.ProvideServices())
            {
                this.InstallService(service);
            }

            this.Construct();

            for (int i = 0, count = this.children.Count; i < count; i++)
            {
                var node = this.children[i];
                node.parent = this;
                node.Install();
            }
        }

        protected virtual IEnumerable<object> ProvideServices()
        {
            yield break;
        }

        protected virtual void Construct()
        {
        }

        private void InstallService(object service)
        {
            this.services.Add(service);

            if (service is IGameUpdater updater)
            {
                this.updaters.Add(updater);
            }

            if (service is IGameFixedUpdater fixedUpdater)
            {
                this.fixedUpdaters.Add(fixedUpdater);
            }

            if (service is IGameLateUpdater lateUpdater)
            {
                this.lateUpdaters.Add(lateUpdater);
            }
        }

        public Task CallAsync<T>() where T : GameEvent
        {
            return Task.Run(this.Call<T>);
        }

        public void Call<T>() where T : GameEvent
        {
            if (!this.installed)
            {
                Debug.LogWarning($"Game Node {this.name} is not installed yet", this);
                return;
            }

            this.CallServices<T>();

            for (int i = 0, count = this.children.Count; i < count; i++)
            {
                var node = this.children[i];
                node.Call<T>();
            }
        }

        private void CallServices<T>() where T : Attribute
        {
            for (int i = 0, count = this.services.Count; i < count; i++)
            {
                var service = this.services[i];
                this.CallService<T>(service);
            }
        }

        private void CallService<T>(object service) where T : Attribute
        {
            var type = service.GetType();
            while (type != null && type != typeof(object) && type != typeof(MonoBehaviour))
            {
                var methods = type.GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly
                );

                for (int i = 0, count = methods.Length; i < count; i++)
                {
                    var method = methods[i];
                    if (method.GetCustomAttribute<T>() != null)
                    {
                        this.CallMethod(service, method);
                    }
                }

                type = type.BaseType;
            }
        }

        private void CallMethod(object service, MethodInfo method)
        {
            var parameters = method.GetParameters();
            var count = parameters.Length;

            var args = new object[count];
            for (var i = 0; i < count; i++)
            {
                var parameter = parameters[i];
                var parameterType = parameter.ParameterType;
                args[i] = this.FindService(parameterType);
            }

            method.Invoke(service, args);
        }

        protected object FindService(Type serviceType)
        {
            var node = this;
            while (node != null)
            {
                if (node.TryGetService(serviceType, out var service))
                {
                    return service;
                }

                node = node.parent;
            }

            throw new Exception($"Can't find service {serviceType.Name}!");
        }

        protected T FindService<T>()
        {
            var node = this;
            while (node != null)
            {
                if (node.TryGetService<T>(out var service))
                {
                    return service;
                }

                node = node.parent;
            }

            throw new Exception($"Can't find service {typeof(T).Name}!");
        }

        private bool TryGetService(Type targetType, out object service)
        {
            for (int i = 0, count = this.services.Count; i < count; i++)
            {
                service = this.services[i];
                var serviceType = service.GetType();
                if (targetType.IsAssignableFrom(serviceType))
                {
                    return true;
                }
            }

            service = default;
            return false;
        }

        private bool TryGetService<T>(out T service)
        {
            for (int i = 0, count = this.services.Count; i < count; i++)
            {
                var current = this.services[i];
                if (current is T tService)
                {
                    service = tService;
                    return true;
                }
            }

            service = default;
            return false;
        }

        public T Node<T>(Func<T, bool> predicate = null) where T : GameNode
        {
            if (predicate == null)
            {
                predicate = _ => true;
            }

            for (int i = 0, count = this.children.Count; i < count; i++)
            {
                var node = this.children[i];
                if (node is not T tNode)
                {
                    continue;
                }

                if (predicate.Invoke(tNode))
                {
                    return tNode;
                }
            }

            throw new Exception($"Node of type {typeof(T).Name} is not found!");
        }

        public void AddNode(GameNode node)
        {
            this.children.Add(node);
            node.parent = this;
            node.Install();
        }

        public void RemoveNode(GameNode node)
        {
            if (this.children.Remove(node))
            {
                node.parent = null;
            }
        }
    }
}