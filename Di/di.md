# One Abstraction Lower
## Inversion of Control (DI) Containers in C#

A programmer often have to deal with many new concepts and programming paradigms being introduced at a rate not seen in many other industries.

As a programmer, we're often expected to know and use these concepts, and often enough, a lot of them are abstracted away from the programmer by means of libraries and packages. 

In this series, I will try to uncover a bit of the technical side of a lot of these abstractions by implementing "simple" versions of the concepts.



The first concept we're looking at is "Inversion of Control - Containers".
Most people programming in C# or Java have used IoC container's, and some might not even realise. ASPNET and Java Spring both uses IoC containers heavily to control objects and services.

An IoC container can give us an service, if it's possible for the container to create it.
This means the container will try to create an instance of the service, resolving the dependency graph and providing all the needed dependencies.
If the container cant resolve the dependency graph internally, it can't make the service.

Lets try to make our own IoC Container, but first we need to make a service to resolve!

Lets create a simple interface which just gives us a number:
And an implementation for the interface using the Random class:
```csharp
public interface INumberProvider
{
    int GetNumber();
}

public class RandomNumberProvider : INumberProvider
{
    public int GetNumber() => Random.Shared.Next();
}

```


Currently this class only has an empty constructor (implicitly defined in C# unless another constructor has been defined).
We want our IoC container to be able to provide a INumberProvider instance, so lets make a container class and a method to resolve a generic type:
```csharp
public class Container
{
    public T? Resolve<T>() where T : class
    {
        return default;
    }
}
```

We can resolve a generic instance of a type, but it just returns default values (null).
Now we need to tell the container that we want it to be able to make a INumberProvider.
Normally this is called registering a dependency in IoC containers, so lets call our method that as well.
Register takes 2 generic parameters here (for simplicity), the target type, eg. interface, and the implementation type.
The target Type is INumberProvider, and source is RandomNumberProvider here.
```csharp
public class Container
{
    public void Register<T, TImpl>() where TImpl : T where T : class
    {
    }
}
```

Okay, now we have our 2 methods in our IoC container, lets take a step back and think about requirements.
An IoC container normally have an idea about lifetimes of it's services.
We'll implement the 2 most used ones: Transient (Meaning we create the service every time it's requested) and Singleton (Only 1 instance of the service is ever created, and every subsequent 'Resolve' call will return the same instance).
We will need to make this very clear when we register services, so lets refactor the 'Register' method to make the lifetimes very clear.

```csharp
public class Container
{
    public void RegisterSingleton<T, TImpl>() where TImpl : T where T : class
    {
    }
    
    public void RegisterTransient<T, TImpl>() where TImpl : T where T : class
    {
    }
}
```

Now we need somewhere to store our registrations. A dictionary with the target type as key sounds like the tool to use!
But what should the value be? Lets reflect:
We need to know the Implementation Type, we also need to know the lifetime.
But, with a singleton object, we also need a place to store the singleton object!
Aight this should do the trick: 
```csharp
enum RegistrationType
{
    Transient,
    Singleton
}
record TypeRegistration(Type TargetType, Type ImplementationType, RegistrationType Type)
{
    public object? SingletonInstance { get; set; }
}
```

Now we can implement our 'Register' methods:
```csharp
public class Container
{
    private readonly Dictionary<Type, TypeRegistration> registeredTypes = new();

    public void RegisterSingleton<T, TImpl>() where TImpl : T where T : class
    {
        var registeredType = new TypeRegistration(typeof(T), typeof(TImpl), RegistrationType.Singleton);
        registeredTypes.Add(typeof(T), registeredType);
    }
    
    public void RegisterTransient<T, TImpl>() where TImpl : T where T : class
    {
        var registeredType = new TypeRegistration(typeof(T), typeof(TImpl), RegistrationType.Transient);
        registeredTypes.Add(typeof(T), registeredType);
    }
}
```
We can also extend our 'Resolve' method a bit. If we have the type registered, we can try to build it, if not, we can throw an exception. You could return null as well, but I feel like it's an exception if you'd try to resolve a service which was not registered.
In C#, everything is an object, so our 'BuildObject' method can just return the base object, and we should be able to safely cast it to a generic T value, since 'BuildObject' returned a value.
```csharp
public class Container
{
    public T? Resolve<T>() where T : class
    {
        if (registeredTypes.TryGetValue(typeof(T), out var registration))
        {
            return (T)BuildObject(registration);
        }
       
        throw new Exception($"Type {typeof(T).Name} was not registered!");
    }

    private object BuildObject(TypeRegistration registration)
    {
        return null;
    }
}
```

Okay, now the fun part! Lets actually implement code which will try to build our services!

Action plan:
 - if it has a singleton instance, just return it. It means it's registered as a singleton, and we've already created it before.
 - Find all constructors for the given type, and save the reference to the constructor and it's required parameters.
 - Filter them based on which ones we can resolve (have all the parameters registered)
 - If all dependencies arent registered, throw Exception.
 - If constructor has no parameters, we can just invoke it directly. Otherwise try recursively to resolve all dependencies (We can get in an endless circle of trying to resolve the same service in a loop, but that's out of scope for this article).
 - Last, if the type we're resolving is a singleton, then set the newly created singleton instance.
```csharp
private object BuildObject(TypeRegistration registration)
{
    if (registration.SingletonInstance != null) return registration.SingletonInstance;
    
    var constructors = registration.ImplementationType
        .GetConstructors()
        .Select(ctor => new Constructor(ctor, ctor.GetParameters()))
        .ToArray();
    
    // Find suitable constructor. Here we could have checked for all potential constructors, but for this exercise, lets just take the first
    var suitableConstructor = constructors
        .Where(CanResolveConstructor)
        .MinBy(x => x.Parameters.Length); // just takes the first, with the lowest amount of parameters

    if (suitableConstructor == null) throw new Exception($"Cannot construct TargetType: {registration.TargetType} ImplType: {registration.ImplementationType}");

    try
    {
        // If the constructor has no parameters, we can just invoke it. If not, let's try to resolve all subtypes recursively
        var constructedObject = suitableConstructor.Parameters.Length switch
        {
            0 => suitableConstructor.Ctor.Invoke(Array.Empty<object>()),
            _ => suitableConstructor.Ctor.Invoke(suitableConstructor.Parameters.Select(x => BuildObject(registeredTypes[x.ParameterType])).ToArray())
        };
        // If registration is a singleton, and we've just constructetd the object, save it for next time.
        if (registration.Type == RegistrationType.Singleton) registration.SingletonInstance = constructedObject;
        return constructedObject;
    }
    catch (Exception e) // If we get an exception here, it means that we did not resolve a dependency correct!
    {
        throw new Exception($"Cannot construct RegisteredType: {registration.TargetType} ImplType: {registration.ImplementationType} Because we failed to construct inner type:\n{e.Message}");
    }
}

private bool CanResolveConstructor(Constructor constructor)
{
    if (constructor.Parameters.Length == 0) return true;
    return constructor.Parameters.All(x => registeredTypes.ContainsKey(x.ParameterType));
}


record Constructor(ConstructorInfo Ctor, ParameterInfo[] Parameters);
```

Aight, so far so good! Lets make a small program to test our container now!
```csharp
var container = new Container();
// Register Types
container.RegisterTransient<INumberProvider, RandomNumberProvider>();
container.RegisterSingleton<PrinterService, PrinterService>();
// Resolve
var numberProvider = container.Resolve<INumberProvider>();
var numberProvider2 = container.Resolve<INumberProvider>();

var service = container.Resolve<PrinterService>();
var service2 = container.Resolve<PrinterService>();
// Test
service.PrintNumber();
Console.WriteLine($"IsTransientSame: {ReferenceEquals(numberProvider, numberProvider2)}");
Console.WriteLine($"IsSingletonSame: {ReferenceEquals(service, service2)}");

record PrinterService(INumberProvider NumberProvider)
{
    public void PrintNumber() => Console.WriteLine(NumberProvider.GetNumber());
}
```
The code outputs:
 - 1693487198 (Just a random number, changes every time)
 - IsTransientSame: False
 - IsSingletonSame: True

Aight, here we're testing that our container works as expected, and that when we're resolving transient dependencies it always gives us a new dependency, and when it's a singleton, it always gives us the same!

Lets try to switch the implementation around to use another INumberProvider!
We also switch around the implementation type we register:
```csharp
class EverythingNumberProvider : INumberProvider
{
    public int GetNumber() => 42;
}

container.RegisterTransient<INumberProvider, EverythingNumberProvider>();
```

If we then run our old test program, our output now changes to: 
- 42 (The same every time)
- IsTransientSame: False
- IsSingletonSame: True

That it! We've made a simple, but working IoC container in less than 100 lines of code! Now we can add all the features that our heart desires.

The code in it's entirety can be found at: [Container.cs](./Container.cs)
