using System.Reflection;

namespace Di;

public class Container
{
    private Dictionary<Type, TypeRegistration> registeredTypes = new();
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
    
    public T Resolve<T>() where T : class
    {
        if (registeredTypes.TryGetValue(typeof(T), out var registration))
        {
            return (T)BuildObject(registration);
        }

        throw new Exception($"Type {typeof(T).Name} was not registered!");
    }

    private object BuildObject(TypeRegistration registration)
    {
        if (registration.SingletonInstance != null) return registration.SingletonInstance;
        
        var constructors = registration.ImplementationType
            .GetConstructors()
            .Select(ctor => new Constructor(ctor, ctor.GetParameters()))
            .ToArray();
        
        // Find suitable constructor.
        var suitableConstructor = constructors
            .Where(IsConstructorRelevant)
            .MinBy(x => x.Parameters.Length);

        if (suitableConstructor == null) throw new Exception($"Cannot construct RegisteredType: {registration.TargetType} ImplType: {registration.ImplementationType}");

        try
        {
            var constructedObject = suitableConstructor.Parameters.Length switch
            {
                0 => suitableConstructor.Ctor.Invoke(Array.Empty<object>()),
                _ => suitableConstructor.Ctor.Invoke(suitableConstructor.Parameters.Select(x => BuildObject(registeredTypes[x.ParameterType])).ToArray())
            };
            if (registration.Type == RegistrationType.Singleton) registration.SingletonInstance = constructedObject;
            return constructedObject;
        }
        catch (Exception e) // If we get an exception here, it means that we did not resolve a dependency correct!
        {
            throw new Exception($"Cannot construct RegisteredType: {registration.TargetType} ImplType: {registration.ImplementationType} Because we failed to construct inner type:\n{e.Message}");
        }
    }

    private bool IsConstructorRelevant(Constructor constructor)
    {
        if (constructor.Parameters.Length == 0) return true;
        return constructor.Parameters.All(x => registeredTypes.ContainsKey(x.ParameterType));
    }


    record Constructor(ConstructorInfo Ctor, ParameterInfo[] Parameters);
    
    enum RegistrationType
    {
        Transient,
        Singleton
    }
    record TypeRegistration(Type TargetType, Type ImplementationType, RegistrationType Type)
    {
        public object? SingletonInstance { get; set; }
    }
}