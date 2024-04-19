# DragonECS-Hybrid
 
`IEcsHybridComponent` - Гибридные компоненты. Испольщуются для реализации гибридности.

`EcsHybridPool` - пул для гибридных компонентов. Испольщуются для реализации гибридности, хранит struct-компоненты IEcsHybridComponent;

Для смешивания архитектурных подходов классического OOP и ECS используется специальный пул `EcsHybridPool<T>`. Принцип работы этого пула несколько отличается от других и он упрощает поддержу наследования и полиморфизма. 

<details>
<summary>Как это работает?</summary>

При добавлении элемента в пул, пул сканирует его иерархию наследования и реализуемые интерфейсы в поиске типов у которых есть интерфес `IEcsHybridComponent` и автоматически добавляет компонент в соответсвующие этим типам пулы. Таким же образом происходит удаление. Сканирвоание просиходит не для типа T а для типа экземпляра, поэтому в примере ниже строчка в `_world.GetPool<ITransform>().Add(entity, _rigidbody);` добавляет не только в пул `EcsHybridPool<ITransform>` но и в остальные.

</details>

Пример использования:
``` csharp
public interface ITransform : IEcsHybridComponent
{
    Vector3 Position { get; set; }
    // ...
}
public class Transform : ITransform
{
    public Vector3 Position { get; set; }
    // ...
}
public class Rigidbody : Transform
{
    public Vector3 Position { get; set; }
    public float Mass { get; set; }
    // ...
}
public class Camera : ITransform
{
    Vector3 Position { get; set; }
    // ...
}
public TransformAspect : EcsAspect
{
    public EcsHybridPool<Transform> transforms;
    public Aspect(Builder b) 
    {
        transforms = b.Include<Transform>();
    }
}
// ...

EcsWorld _world;
Rigidbody _rigidbody;
// ...

// Создадим пустую сущность.
int entity = _world.NewEmptyEntity();
// Получаем пул EcsHybridPool<ITransform> и добавляем в него для сущности компонент _rigidbody.
// Если вместо ITransform подставить Transform или Rigidbody, то результат будет одинаковый
_world.GetPool<ITransform>().Add(entity, _rigidbody);
// ...

//Все эти строчки вернут экземпляр _rigidbody.
ITransform iTransform = _world.GetPool<ITransform>().Get(entity);  
Transform transform = _world.GetPool<Transform>().Get(entity);  
Rigidbody rigidbody = _world.GetPool<Rigidbody>().Get(entity);
//Исключение - отсутсвует компонент. Camera не является наследником или наследуемым классом для _rigidbody.
Camera camera = _world.GetPool<Camera>().Get(entity);

//Вернет True. Поэтому фишка гибридных пулов будет работать и в запросах сущностей
bool isMatches = _world.GetAspect<TransformAspect>().IsMatches(entity);

//Все эти строчки вернут True.
bool isITransform = _world.GetPool<ITransform>().Has(entity);  
bool isTransform = _world.GetPool<Transform>().Has(entity);  
bool isRigidbody = _world.GetPool<Rigidbody>().Has(entity);
//Эта строчка вернет False.
bool isCamera = _world.GetPool<Camera>().Has(entity);
// ...

// Удалим у сущности компонент.
_world.GetPool<ITransform>().Del(entity);
// ...
//Все эти строчки вернут False.
bool isITransform = _world.GetPool<ITransform>().Has(entity);  
bool isTransform = _world.GetPool<Transform>().Has(entity);  
bool isRigidbody = _world.GetPool<Rigidbody>().Has(entity);
bool isCamera = _world.GetPool<Camera>().Has(entity);
// ...
```
