<p align="center">
<img width="400" src="https://private-user-images.githubusercontent.com/99481254/386965991-5f16d1ca-0f05-4ca0-88aa-6fa55ac9b817.png?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmF3LmdpdGh1YnVzZXJjb250ZW50LmNvbSIsImtleSI6ImtleTUiLCJleHAiOjE3NDIxODIzNDQsIm5iZiI6MTc0MjE4MjA0NCwicGF0aCI6Ii85OTQ4MTI1NC8zODY5NjU5OTEtNWYxNmQxY2EtMGYwNS00Y2EwLTg4YWEtNmZhNTVhYzliODE3LnBuZz9YLUFtei1BbGdvcml0aG09QVdTNC1ITUFDLVNIQTI1NiZYLUFtei1DcmVkZW50aWFsPUFLSUFWQ09EWUxTQTUzUFFLNFpBJTJGMjAyNTAzMTclMkZ1cy1lYXN0LTElMkZzMyUyRmF3czRfcmVxdWVzdCZYLUFtei1EYXRlPTIwMjUwMzE3VDAzMjcyNFomWC1BbXotRXhwaXJlcz0zMDAmWC1BbXotU2lnbmF0dXJlPWQxNDRjZjk5YTQzNzE0NDI5MTUxZGFhNTU0NWUyZTUzYjk1NDcyZDJlNTA4ZjVjYTM0ZDk4OTBiZjI1OGI1MDYmWC1BbXotU2lnbmVkSGVhZGVycz1ob3N0In0.n5-D5v5kHNKINYX9MKrZGhfJ-Z4vtxuhDAWAipsgAps">
</p>

<p align="center">
<img alt="Version" src="https://img.shields.io/github/package-json/v/DCFApixels/DragonECS-Hybrid?color=%23ff4e85&style=for-the-badge">
<img alt="License" src="https://img.shields.io/github/license/DCFApixels/DragonECS-Hybrid?color=ff4e85&style=for-the-badge">
<a href="https://discord.gg/kqmJjExuCf"><img alt="Discord" src="https://img.shields.io/badge/Discord-JOIN-00b269?logo=discord&logoColor=%23ffffff&style=for-the-badge"></a>
<a href="http://qm.qq.com/cgi-bin/qm/qr?_wv=1027&k=IbDcH43vhfArb30luGMP1TMXB3GCHzxm&authKey=s%2FJfqvv46PswFq68irnGhkLrMR6y9tf%2FUn2mogYizSOGiS%2BmB%2B8Ar9I%2Fnr%2Bs4oS%2B&noverify=0&group_code=949562781"><img alt="QQ" src="https://img.shields.io/badge/QQ-JOIN-00b269?logo=tencentqq&logoColor=%23ffffff&style=for-the-badge"></a>
</p>

# DragonECS-Hybrid
 
`IEcsHybridComponent` - Гибридные компоненты. Используются для реализации гибридности.

`EcsHybridPool` - пул для гибридных компонентов. Используются для реализации гибридности, хранит class-компоненты `IEcsHybridComponent`;

Для смешивания архитектурных подходов классического OOP и ECS используется специальный пул `EcsHybridPool<T>`. Принцип работы этого пула несколько от других и он добавляет поддержу наследования и полиморфизма в компоненты. 

<details>
<summary>Как это работает?</summary>

При добавлении элемента в пул, пул сканирует его иерархию наследования и реализуемые интерфейсы в поиске типов у которых есть интерфейс `IEcsHybridComponent` и автоматически добавляет компонент в соответствующие этим типам пулы. Таким же образом происходит удаление. Сканирование происходит не для типа T а для типа экземпляра, поэтому в примере ниже строчка в `_world.GetPool<ITransform>().Add(entity, _rigidbody);` добавляет не только в пул `EcsHybridPool<ITransform>` но и в остальные.

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
//Исключение - отсутствует компонент. Camera не является наследником или наследуемым классом для _rigidbody.
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
