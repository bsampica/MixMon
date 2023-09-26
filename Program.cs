
using System.Device.Gpio;
using Iot.Device.OneWire;
using Spectre.Console;

namespace MixMon;


class Program
{
    private const int defaultCheckTime = 5_000;
    private const int PIN = 13;

    static async Task Main(string[] args)
    {

        Console.WriteLine("VIBE: Temp Controlled C02 ");

        await DrawUI(args);
    }


    static async Task DrawUI(string[] args)
    {
        var checkTime = defaultCheckTime;
        if (args.Any())
        {
            _ = int.TryParse(args[0], out checkTime);
        }
        var relayOn = false;
        var table = new Table().Centered();

        Console.WriteLine($"Using {checkTime}ms as temp refresh time");

        //AnsiConsole.Console.Clear(false);

        await AnsiConsole.Live(table)
                 .AutoClear(false)
                 .Overflow(VerticalOverflow.Ellipsis)
                 .Cropping(VerticalOverflowCropping.Top)
                 .StartAsync(async ctx =>
                 {

                     table.AddColumn("[bold]Temperature[/]").Centered();
                     table.AddColumn("[bold]CO2 State[/]").Centered();
                     table.AddRow("N/A", "N/A").Centered();

                     var initialTemp = await GetTemperature();
                     table.UpdateCell(0, 0, $"[green] {initialTemp:F1}ºC [/]").Centered();
                     table.UpdateCell(0, 1, $"[green] {relayOn} [/]").Centered();
                     ctx.Refresh();

                     while (!Console.KeyAvailable)
                     {
                         var temp = await GetTemperature();
                         switch (temp)
                         {
                             case >= 30:
                                 table.UpdateCell(0, 0, $"[red] {temp:F2}ºC [/]");
                                 //CLOSE THE RELAY
                                 if (relayOn)
                                     await ControlSolenoid(false);
                                     
                                 relayOn = false;
                                 ctx.Refresh();

                                 break;
                             case >= 5:
                                 table.UpdateCell(0, 0, $"[yellow] {temp:F1}ºC [/]");
                                 // CLOSE THE RELAY
                                 if (relayOn)
                                     await ControlSolenoid(false);

                                 relayOn = false;
                                 ctx.Refresh();

                                 break;
                             case <= 4:
                                 table.UpdateCell(0, 0, $"[green] {temp:F2}ºC [/]");
                                 // OPEN THE RELAY
                                 if (!relayOn)
                                     await ControlSolenoid(true);

                                 relayOn = true;
                                 ctx.Refresh();

                                 break;
                         }

                         switch (relayOn)
                         {
                             case true:
                                 table.UpdateCell(0, 1, $"[green] ON [/]");
                                 break;
                             case false:
                                 table.UpdateCell(0, 1, $"[yellow] OFF [/]");
                                 break;
                         }

                         ctx.Refresh();

                         await Task.Delay(checkTime);
                     }

                     // Make sure if we exit the program the right way, the solenoid is reset to off
                     ControlSolenoid(open: false).GetAwaiter().GetResult();
                     return Task.CompletedTask;

                 });

    }

    static Task ControlSolenoid(bool open)
    {

        using (var gpio = new GpioController())
        {
            if (!gpio.IsPinOpen(PIN))
            {
                gpio.OpenPin(PIN, PinMode.Output);
            }

            if (open) gpio.Write(PIN, PinValue.High);
            if (!open) gpio.Write(PIN, PinValue.Low);
        }
        return Task.CompletedTask;
    }


    static async Task<double> GetTemperature()
    {
        OneWireThermometerDevice device = new("w1_bus_master1", "28-03139794254b");
        var temp = await device.ReadTemperatureAsync();
        // Console.WriteLine($"TEMP: {temp.DegreesFahrenheit}");
        return temp.DegreesCelsius;
    }

}
