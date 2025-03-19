#if DISABLE_DEBUG
#undef DEBUG
#endif
using DCFApixels.DragonECS.Core;
using DCFApixels.DragonECS.Hybrid;
using DCFApixels.DragonECS.Hybrid.Internal;
using DCFApixels.DragonECS.PoolsCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DCFApixels.DragonECS
{
    /// <summary> Hybrid component. </summary>
    [MetaColor(MetaColor.DragonRose)]
    [MetaGroup(EcsHybridConsts.PACK_GROUP, EcsConsts.POOLS_GROUP)]
    [MetaDescription(EcsConsts.AUTHOR, "Hybrid component.")]
    [MetaID("DragonECS_2EBB343694014EC74A02E802E36B8D68")]
    public interface IEcsHybridComponent : IEcsComponentMember
    {
        bool IsAlive { get; }
        void OnAddToPool(entlong entity);
        void OnDelFromPool(entlong entity);
    }

    /// <summary> Pool for IEcsHybridComponent components. </summary>
    [MetaColor(MetaColor.DragonRose)]
    [MetaGroup(EcsHybridConsts.PACK_GROUP, EcsConsts.POOLS_GROUP)]
    [MetaDescription(EcsConsts.AUTHOR, "Pool for IEcsHybridComponent components.")]
    [MetaID("DragonECS_4ACF343694012D56DB73D5FA50DCAA75")]
    [DebuggerDisplay("Count: {Count} ComponentType: {ComponentType}")]
    public sealed class EcsHybridPool<T> : IEcsPoolImplementation<T>, IEcsHybridPool<T>, IEcsHybridPoolInternal, IEnumerable<T> //IEnumerable<T> - IntelliSense hack
        where T : class, IEcsHybridComponent
    {
        private EcsWorld _source;
        private int _componentTypeID;
        private EcsMaskChunck _maskBit;

        private int[] _mapping;// index = entityID / value = itemIndex;/ value = 0 = no entityID
        private T[] _items; //dense
        private int[] _entities;
        private int _itemsCount = 0;

        private int[] _recycledItems;
        private int _recycledItemsCount = 0;

#if !DRAGONECS_DISABLE_POOLS_EVENTS
        private List<IEcsPoolEventListener> _listeners = new List<IEcsPoolEventListener>();
#endif
        private bool _isLocked;

        private EcsWorld.PoolsMediator _mediator;
        private HybridGraph _graph;

        #region Properites
        public int Count
        {
            get { return _itemsCount; }
        }
        public int Capacity
        {
            get { return _items.Length; }
        }
        public int ComponentTypeID
        {
            get { return _componentTypeID; }
        }
        public Type ComponentType
        {
            get { return typeof(T); }
        }
        public EcsWorld World
        {
            get { return _source; }
        }
        public bool IsReadOnly
        {
            get { return false; }
        }
        #endregion

        #region Methods
        void IEcsHybridPoolInternal.AddRefInternal(int entityID, object componentRaw)
        {
#if DEBUG || DRAGONECS_STABILITY_MODE
            if (_isLocked) { EcsPoolThrowHelper.ThrowPoolLocked(); }
#endif
            ref int itemIndex = ref _mapping[entityID];
            if (itemIndex > 0)
            {
#if DEBUG || DRAGONECS_STABILITY_MODE
                if (_items[itemIndex] != componentRaw) { EcsPoolThrowHelper.ThrowAlreadyHasComponent<T>(entityID); }
#endif
                return;
            }
            if (_recycledItemsCount > 0)
            {
                itemIndex = _recycledItems[--_recycledItemsCount];
                _itemsCount++;
            }
            else
            {
                itemIndex = ++_itemsCount;
                if (itemIndex >= _items.Length)
                {
                    Array.Resize(ref _items, _items.Length << 1);
                    Array.Resize(ref _entities, _items.Length);
                }
            }
            _mediator.RegisterComponent(entityID, _componentTypeID, _maskBit);
#if !DRAGONECS_DISABLE_POOLS_EVENTS
            _listeners.InvokeOnAdd(entityID);
#endif
            _items[itemIndex] = (T)componentRaw;
            _entities[itemIndex] = entityID;
        }
        public void Add(int entityID, T component)
        {
#if DEBUG || DRAGONECS_STABILITY_MODE
            if (_isLocked) { EcsPoolThrowHelper.ThrowPoolLocked(); }
#endif
            HybridBranch branch = _graph.GetBranch(component.GetType());
            //branch.TargetTypePool.AddRefInternal(entityID, component);
            foreach (var pool in branch.GetRelatedTypePools())
            {
                pool.AddRefInternal(entityID, component);
            }
            component.OnAddToPool((_source, entityID));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int entityID, T component)
        {
            if (Has(entityID))
            {
                Del(entityID);
            }
            Add(entityID, component);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get(int entityID)
        {
#if DEBUG || DRAGONECS_STABILITY_MODE
            if (!Has(entityID)) EcsPoolThrowHelper.ThrowNotHaveComponent<T>(entityID);
#endif
#if !DRAGONECS_DISABLE_POOLS_EVENTS
            _listeners.InvokeOnGet(entityID);
#endif
            return _items[_mapping[entityID]];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T Read(int entityID)
        {
#if DEBUG || DRAGONECS_STABILITY_MODE
            if (!Has(entityID)) EcsPoolThrowHelper.ThrowNotHaveComponent<T>(entityID);
#endif
            return ref _items[_mapping[entityID]];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int entityID)
        {
            return _mapping[entityID] > 0;
        }
        void IEcsHybridPoolInternal.DelInternal(int entityID)
        {
#if DEBUG || DRAGONECS_STABILITY_MODE
            if (!Has(entityID)) EcsPoolThrowHelper.ThrowNotHaveComponent<T>(entityID);
            if (_isLocked) { EcsPoolThrowHelper.ThrowPoolLocked(); }
#endif
            ref int itemIndex = ref _mapping[entityID];
            T component = _items[itemIndex];
            if (_recycledItemsCount >= _recycledItems.Length)
            {
                Array.Resize(ref _recycledItems, _recycledItems.Length << 1);
            }
            _recycledItems[_recycledItemsCount++] = itemIndex;
            _mapping[entityID] = 0;
            _entities[itemIndex] = 0;
            _itemsCount--;
            _mediator.UnregisterComponent(entityID, _componentTypeID, _maskBit);
#if !DRAGONECS_DISABLE_POOLS_EVENTS
            _listeners.InvokeOnDel(entityID);
#endif
        }
        public void Del(int entityID)
        {
            var component = Get(entityID);
            HybridBranch branch = _graph.GetBranch(component.GetType());
            //branch.TargetTypePool.DelInternal(entityID);
            foreach (var pool in branch.GetRelatedTypePools())
            {
                pool.DelInternal(entityID);
            }
            component.OnDelFromPool((_source, entityID));
        }
        public void TryDel(int entityID)
        {
            if (Has(entityID))
            {
                Del(entityID);
            }
        }
        public void Copy(int fromEntityID, int toEntityID)
        {
#if DEBUG || DRAGONECS_STABILITY_MODE
            if (!Has(fromEntityID)) EcsPoolThrowHelper.ThrowNotHaveComponent<T>(fromEntityID);
#endif
            Set(toEntityID, Get(fromEntityID));
        }
        public void Copy(int fromEntityID, EcsWorld toWorld, int toEntityID)
        {
#if DEBUG || DRAGONECS_STABILITY_MODE
            if (!Has(fromEntityID)) EcsPoolThrowHelper.ThrowNotHaveComponent<T>(fromEntityID);
#endif
            toWorld.GetPool<T>().Set(toEntityID, Get(fromEntityID));
        }

        public void ClearNotAliveComponents()
        {
#if DEBUG || DRAGONECS_STABILITY_MODE
            if (_isLocked) { EcsPoolThrowHelper.ThrowPoolLocked(); }
#endif
            for (int i = _itemsCount - 1; i >= 0; i--)
            {
                if (!_items[i].IsAlive)
                {
                    Del(_entities[i]);
                }
            }
        }

        public void ClearAll()
        {
#if DEBUG || DRAGONECS_STABILITY_MODE
            if (_isLocked) { EcsPoolThrowHelper.ThrowPoolLocked(); }
#endif
            var span = _source.Where(out SinglePoolAspect<EcsHybridPool<T>> _);
            foreach (var entityID in span)
            {
                Del(entityID);
            }
        }
        #endregion

        #region Callbacks
        void IEcsPoolImplementation.OnInit(EcsWorld world, EcsWorld.PoolsMediator mediator, int componentTypeID)
        {
            _source = world;
            _mediator = mediator;
            _componentTypeID = componentTypeID;
            _maskBit = EcsMaskChunck.FromID(componentTypeID);

            int capacity = world.Configs.GetWorldConfigOrDefault().PoolComponentsCapacity;

            _mapping = new int[world.Capacity];
            _items = new T[capacity];
            _entities = new int[capacity];
            _recycledItems = new int[world.Configs.GetWorldConfigOrDefault().PoolRecycledComponentsCapacity];

            _graph = _source.Get<HybridGraphCmp>().Graph;
            _graph.InitPool(this);
        }
        void IEcsPoolImplementation.OnWorldResize(int newSize)
        {
            Array.Resize(ref _mapping, newSize);
        }
        void IEcsPoolImplementation.OnWorldDestroy() { }
        void IEcsPoolImplementation.OnReleaseDelEntityBuffer(ReadOnlySpan<int> buffer)
        {
            foreach (var entityID in buffer)
            {
                TryDel(entityID);
            }
        }
        void IEcsPoolImplementation.OnLockedChanged_Debug(bool locked) { _isLocked = locked; }
        #endregion

        #region Other
        void IEcsPool.AddEmpty(int entityID)
        {
#if DEBUG
            EcsDebug.PrintWarning("AddEmpty is not supported for EcsHybridPool.");
#endif
        }
        void IEcsPool.AddRaw(int entityID, object dataRaw) { Add(entityID, (T)dataRaw); }
        object IEcsReadonlyPool.GetRaw(int entityID) { return Get(entityID); }
        object IEcsHybridPoolInternal.GetRaw(int entityID) { return Get(entityID); }
        void IEcsPool.SetRaw(int entityID, object dataRaw) { Set(entityID, (T)dataRaw); }
        #endregion

        #region Listeners
#if !DRAGONECS_DISABLE_POOLS_EVENTS
        public void AddListener(IEcsPoolEventListener listener)
        {
            if (listener == null) { throw new ArgumentNullException("listener is null"); }
            _listeners.Add(listener);
        }
        public void RemoveListener(IEcsPoolEventListener listener)
        {
            if (listener == null) { throw new ArgumentNullException("listener is null"); }
            _listeners.Remove(listener);
        }
#endif
        #endregion

        #region IEnumerator - IntelliSense hack
        IEnumerator<T> IEnumerable<T>.GetEnumerator() { throw new NotImplementedException(); }
        IEnumerator IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
        #endregion

        #region MarkersConverter
        public static implicit operator EcsHybridPool<T>(IncludeMarker a) { return a.GetInstance<EcsHybridPool<T>>(); }
        public static implicit operator EcsHybridPool<T>(ExcludeMarker a) { return a.GetInstance<EcsHybridPool<T>>(); }
        public static implicit operator EcsHybridPool<T>(OptionalMarker a) { return a.GetInstance<EcsHybridPool<T>>(); }
        #endregion
    }

    #region EcsHybridPoolExtensions
    public static class EcsHybridPoolExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrNotAlive(this IEcsHybridComponent self)
        {
            return self == null || self.IsAlive;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsHybridPool<T> GetPool<T>(this EcsWorld self) where T : class, IEcsHybridComponent
        {
            return self.GetPoolInstance<EcsHybridPool<T>>();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsHybridPool<T> GetPoolUnchecked<T>(this EcsWorld self) where T : class, IEcsHybridComponent
        {
            return self.GetPoolInstanceUnchecked<EcsHybridPool<T>>();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsHybridPool<T> Inc<T>(this EcsAspect.Builder self) where T : class, IEcsHybridComponent
        {
            return self.IncludePool<EcsHybridPool<T>>();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsHybridPool<T> Exc<T>(this EcsAspect.Builder self) where T : class, IEcsHybridComponent
        {
            return self.ExcludePool<EcsHybridPool<T>>();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsHybridPool<T> Opt<T>(this EcsAspect.Builder self) where T : class, IEcsHybridComponent
        {
            return self.OptionalPool<EcsHybridPool<T>>();
        }

        #region Obsolete

        [Obsolete("Use " + nameof(EcsAspect) + "." + nameof(EcsAspect.Builder) + "." + nameof(Inc) + "<T>()")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsHybridPool<T> Include<T>(this EcsAspect.Builder self) where T : class, IEcsHybridComponent
        {
            return self.IncludePool<EcsHybridPool<T>>();
        }
        [Obsolete("Use " + nameof(EcsAspect) + "." + nameof(EcsAspect.Builder) + "." + nameof(Exc) + "<T>()")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsHybridPool<T> Exclude<T>(this EcsAspect.Builder self) where T : class, IEcsHybridComponent
        {
            return self.ExcludePool<EcsHybridPool<T>>();
        }
        [Obsolete("Use " + nameof(EcsAspect) + "." + nameof(EcsAspect.Builder) + "." + nameof(Opt) + "<T>()")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsHybridPool<T> Optional<T>(this EcsAspect.Builder self) where T : class, IEcsHybridComponent
        {
            return self.OptionalPool<EcsHybridPool<T>>();
        }

        //-------------------------------------------------


        [Obsolete("Use " + nameof(EcsAspect) + "." + nameof(EcsAspect.Builder) + "." + nameof(GetPool) + "<T>()")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsHybridPool<T> GetHybridPool<T>(this EcsWorld self) where T : class, IEcsHybridComponent
        {
            return self.GetPoolInstance<EcsHybridPool<T>>();
        }
        [Obsolete("Use " + nameof(EcsAspect) + "." + nameof(EcsAspect.Builder) + "." + nameof(GetPoolUnchecked) + "<T>()")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsHybridPool<T> GetHybridPoolUnchecked<T>(this EcsWorld self) where T : class, IEcsHybridComponent
        {
            return self.GetPoolInstanceUnchecked<EcsHybridPool<T>>();
        }

        [Obsolete("Use " + nameof(EcsAspect) + "." + nameof(EcsAspect.Builder) + "." + nameof(Inc) + "<T>()")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsHybridPool<T> IncludeHybrid<T>(this EcsAspect.Builder self) where T : class, IEcsHybridComponent
        {
            return self.IncludePool<EcsHybridPool<T>>();
        }
        [Obsolete("Use " + nameof(EcsAspect) + "." + nameof(EcsAspect.Builder) + "." + nameof(Exc) + "<T>()")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsHybridPool<T> ExcludeHybrid<T>(this EcsAspect.Builder self) where T : class, IEcsHybridComponent
        {
            return self.ExcludePool<EcsHybridPool<T>>();
        }
        [Obsolete("Use " + nameof(EcsAspect) + "." + nameof(EcsAspect.Builder) + "." + nameof(Opt) + "<T>()")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EcsHybridPool<T> OptionalHybrid<T>(this EcsAspect.Builder self) where T : class, IEcsHybridComponent
        {
            return self.OptionalPool<EcsHybridPool<T>>();
        }
        #endregion
    }
    #endregion
}

namespace DCFApixels.DragonECS.Hybrid.Internal
{
    #region IEcsHybridPoolInternal
    internal interface IEcsHybridPoolInternal
    {
        Type ComponentType { get; }
        void AddRefInternal(int entityID, object component);
        bool Has(int entityID);
        object GetRaw(int entityID);
        void DelInternal(int entityID);
    }
    #endregion

    #region HybridGraph
    internal readonly struct HybridGraphCmp : IEcsWorldComponent<HybridGraphCmp>
    {
        public readonly HybridGraph Graph;
        public HybridGraphCmp(EcsWorld world)
        {
            Graph = new HybridGraph(world);
        }
        public void Init(ref HybridGraphCmp component, EcsWorld world)
        {
            component = new HybridGraphCmp(world);
        }
        public void OnDestroy(ref HybridGraphCmp component, EcsWorld world)
        {
            component = default;
        }
    }

    internal class HybridGraph
    {
        private readonly EcsWorld _world;
        private readonly Dictionary<Type, HybridBranch> _branches = new Dictionary<Type, HybridBranch>();
        private readonly List<IEcsHybridPoolInternal> _pools = new List<IEcsHybridPoolInternal>();
        public HybridGraph(EcsWorld world)
        {
            _world = world;
        }
        internal HybridBranch GetBranch(Type branchComponentnType)
        {
            if (_branches.TryGetValue(branchComponentnType, out HybridBranch branch) == false)
            {
                branch = new HybridBranch(_world, branchComponentnType);
                _branches.Add(branchComponentnType, branch);

                foreach (var pool in _pools)
                {
                    if (pool.ComponentType.IsAssignableFrom(branchComponentnType))
                    {
                        branch.AddRelatedPool(pool);
                    }
                }
            }
            return branch;
        }

        public void InitPool<TComponent>(EcsHybridPool<TComponent> newPool) where TComponent : class, IEcsHybridComponent
        {
            if (newPool.ComponentType == typeof(IEcsHybridComponent)) { return; }

            var abstrNewPool = (IEcsHybridPoolInternal)newPool;
            var entities = _world.Entities;

            // Инитим значения
            Type tType = typeof(TComponent);
            foreach (var pool in _pools)
            {
                foreach (var e in entities)
                {
                    if (pool.Has(e))
                    {
                        var obj = pool.GetRaw(e);
                        if (tType.IsAssignableFrom(obj.GetType()))
                        {
                            abstrNewPool.AddRefInternal(e, obj);
                        }
                    }
                }
            }

            // Закидываем пул во все ветви.
            _pools.Add(abstrNewPool);
            foreach (var (branchComponentnType, branch) in _branches)
            {
                if (typeof(TComponent).IsAssignableFrom(branchComponentnType))
                {
                    branch.AddRelatedPool(newPool);
                }
            }
        }
    }
    internal class HybridBranch
    {
        public readonly Type Type;
        public readonly EcsMask Mask;
        private List<IEcsHybridPoolInternal> _relatedTypePools;

        public HybridBranch(EcsWorld source, Type type)
        {
#if DEBUG
            if (!type.IsClass) { throw new ArgumentException(); }
#endif
            Type = type;
            Mask = EcsMask.New(source).Inc(type).Build();
            _relatedTypePools = new List<IEcsHybridPoolInternal>();
        }
        public void AddRelatedPool(IEcsHybridPoolInternal pool)
        {
            _relatedTypePools.Add(pool);
        }
        public List<IEcsHybridPoolInternal> GetRelatedTypePools()
        {
            return _relatedTypePools;
        }
    }
    #endregion
}