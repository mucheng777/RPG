using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用对象池核心（纯 C# 类，不是 MonoBehaviour）。
///
/// 职责边界：只管"存货 / 取货 / 还货"，不认识任何业务类型。
///   T 取 Component（敌人、子弹、伤害数字都能用），池不做类型判断，
///   对象的状态复位交给 <see cref="IPoolable"/> 的实现组件自理。
///
/// 取 &amp; 还的生命周期：
///   取 Get()     —— 空池则 Instantiate 补货 → 置位 → 激活 → 逐个 OnSpawned（等价"第二次 Awake"）
///   还 Release() —— 逐个 OnDespawned（退订事件/清理引用）→ 隐藏 → 挂回池根 → 入栈待复用
///
/// 为什么"隐藏"用 SetActive(false)：隐藏的对象不再参与 Update/渲染/物理，
/// 等于开销归零，但对象本身没被销毁，下次取用省掉 Instantiate 的构造与 GC 成本。
/// </summary>
public class ObjectPool<T> where T : Component
{
    private readonly T _prefab;
    private readonly Transform _root;
    private readonly Stack<T> _idle = new Stack<T>();

    /// <param name="prefab">池化的源预制体（必须是预制体资源，否则复用的是场景对象）</param>
    /// <param name="root">池的父节点，回收的对象都挂到它下面（保证 Hierarchy 整洁）</param>
    /// <param name="prewarm">启动时预先造几个，避免第一次取货时卡顿</param>
    public ObjectPool(T prefab, Transform root, int prewarm = 0)
    {
        _prefab = prefab;
        _root = root;
        for (int i = 0; i < prewarm; i++)
            _idle.Push(CreateNew());
    }

    /// <summary>从池中取一个实例并放到指定位置/朝向</summary>
    public T Get(Vector3 position, Quaternion rotation)
    {
        T instance = _idle.Count > 0 ? _idle.Pop() : CreateNew();

        instance.transform.SetPositionAndRotation(position, rotation);
        // 先激活再 OnSpawned：激活会立刻触发子组件 OnEnable（事件订阅先就位），
        // 之后 OnSpawned 里广播的血量/状态才不会丢给"还没订阅"的 UI。
        instance.gameObject.SetActive(true);

        // shortcut: 每次都 GetComponentsInChildren 会产生一次小数组分配，
        //           对象种类多、生成频率高时再改成缓存 IPoolable[] 复用。
        var poolables = instance.GetComponentsInChildren<IPoolable>(true);
        for (int i = 0; i < poolables.Length; i++)
            poolables[i].OnSpawned();
        return instance;
    }

    /// <summary>把实例归还进池，等待下次复用</summary>
    public void Release(T instance)
    {
        if (instance == null) return;

        var poolables = instance.GetComponentsInChildren<IPoolable>(true);
        for (int i = 0; i < poolables.Length; i++)
            poolables[i].OnDespawned();

        instance.gameObject.SetActive(false);
        instance.transform.SetParent(_root, false); // 防止运行期被改过父节点，回收时统一挂回池根
        _idle.Push(instance);
    }

    private T CreateNew()
    {
        T instance = Object.Instantiate(_prefab, _root);
        instance.gameObject.SetActive(false);
        return instance;
    }
}
