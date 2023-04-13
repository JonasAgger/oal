using Di;

var container = new Container();

// Register Types
container.RegisterTransient<INumberProvider, RandomNumberProvider>();
container.RegisterSingleton<PrinterService, PrinterService>();

var numberProvider = container.Resolve<INumberProvider>();
var numberProvider2 = container.Resolve<INumberProvider>();

var service = container.Resolve<PrinterService>();
var service2 = container.Resolve<PrinterService>();

service.PrintNumber();
Console.WriteLine($"IsTransientSame: {ReferenceEquals(numberProvider, numberProvider2)}");
Console.WriteLine($"IsSingletonSame: {ReferenceEquals(service, service2)}");


record PrinterService(INumberProvider NumberProvider)
{
    public void PrintNumber() => Console.WriteLine(NumberProvider.GetNumber());
}

class EverythingNumberProvider : INumberProvider
{
    public int GetNumber() => 42;
}