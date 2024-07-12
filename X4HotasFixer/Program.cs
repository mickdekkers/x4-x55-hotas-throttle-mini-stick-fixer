using Windows.Gaming.Input;
using System;

namespace Application
{
    class Program
    {
        static void Main(string[] args)
        {

            // See https://aka.ms/new-console-template for more information
            Console.WriteLine("The current time is " + DateTime.Now);

            MyClass myClass = new MyClass();

            System.Threading.Thread.Sleep(1000);

            lock (myClass.myLock)
            {
                foreach (var rawGameController in myClass.myRawGameControllers)
                {
                    Console.WriteLine(rawGameController.DisplayName);
                }

                var throttleCandidate = myClass.myRawGameControllers.Where(x => x.DisplayName.Contains("X-55 Rhino Throttle")).First();

                Console.WriteLine("Throttle: " + throttleCandidate.DisplayName);
                Console.WriteLine("Throttle vendor: " + throttleCandidate.HardwareVendorId);
                Console.WriteLine("Throttle product: " + throttleCandidate.HardwareProductId);
                Console.WriteLine("Throttle axis count: " + throttleCandidate.AxisCount);

                var throttle = new Throttle(throttleCandidate);

                while (true)
                {

                    Console.Clear();

                    var dir = throttle.GetCurrentMiniStickDirection();


                    // Now wait 500 ms

                    System.Threading.Thread.Sleep(500);
                }

            }

            Console.WriteLine("Press any key to exit");

            Console.ReadKey();

            Console.WriteLine("Done");

        }
    }
}

class Throttle
{
    private readonly RawGameController throttle;
    private readonly bool[] buttons;
    private readonly GameControllerSwitchPosition[] switches;
    private readonly double[] axes;

    public Throttle(RawGameController throttle)
    {
        this.throttle = throttle;
        this.buttons = new bool[throttle.ButtonCount];
        this.switches = new GameControllerSwitchPosition[throttle.SwitchCount];
        this.axes = new double[throttle.AxisCount];
    }

    public enum MiniStickDirection
    {
        Neutral,
        Up,
        Down,
        Left,
        Right,
        TopRight,
        TopLeft,
        BottomRight,
        BottomLeft
    }

    public MiniStickDirection GetCurrentMiniStickDirection()
    {
        throttle.GetCurrentReading(buttons, switches, axes);

        var miniStickAxis0 = axes[6];
        var miniStickAxis1 = axes[7];

        Console.WriteLine("Mini stick axis 0: " + miniStickAxis0);
        Console.WriteLine("Mini stick axis 1: " + miniStickAxis1);

        var dir = GetMiniStickDirection(miniStickAxis0, miniStickAxis1);

        Console.WriteLine("Mini stick direction: " + dir);

        return dir;
    }

    private const double Tolerance = 0.25; // Adjust this value as needed

    private static MiniStickDirection GetMiniStickDirection(double axis0, double axis1)
    {
        if (IsWithinTolerance(axis0, 0.5) && IsWithinTolerance(axis1, 0.5))
            return MiniStickDirection.Neutral;

        if (IsWithinTolerance(axis0, 0) && IsWithinTolerance(axis1, 0))
            return MiniStickDirection.Up;

        if (IsWithinTolerance(axis0, 1) && IsWithinTolerance(axis1, 1))
            return MiniStickDirection.Down;

        if (IsWithinTolerance(axis0, 1) && IsWithinTolerance(axis1, 0))
            return MiniStickDirection.Right;

        if (IsWithinTolerance(axis0, 0) && IsWithinTolerance(axis1, 1))
            return MiniStickDirection.Left;

        if (IsWithinTolerance(axis0, 0.5) && IsWithinTolerance(axis1, 0))
            return MiniStickDirection.TopRight;

        if (IsWithinTolerance(axis0, 0) && IsWithinTolerance(axis1, 0.5))
            return MiniStickDirection.TopLeft;

        if (IsWithinTolerance(axis0, 0.5) && IsWithinTolerance(axis1, 1))
            return MiniStickDirection.BottomLeft;

        if (IsWithinTolerance(axis0, 1) && IsWithinTolerance(axis1, 0.5))
            return MiniStickDirection.BottomRight;

        return MiniStickDirection.Neutral; // Default to Neutral if no direction is determined
    }



    private static bool IsWithinTolerance(double value, double target)
    {
        return Math.Abs(value - target) <= Tolerance;
    }
}

class MyClass
{
    public readonly object myLock = new object();
    public HashSet<RawGameController> myRawGameControllers = new HashSet<RawGameController>();


    public MyClass()
    {
        RawGameController.RawGameControllerAdded += (object sender, RawGameController e) =>
        {
            lock (myLock)
            {
                myRawGameControllers.Add(e);
            }
        };


        RawGameController.RawGameControllerRemoved += (object sender, RawGameController e) =>
        {
            lock (myLock)
            {
                myRawGameControllers.Remove(e);
            }
        };

        lock (myLock)
        {
            foreach (var rawGameController in RawGameController.RawGameControllers)
            {
                myRawGameControllers.Add(rawGameController);
            }
        }
    }
}


