﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Gaming.Input;
using WindowsInput;

namespace Application
{
    class Program
    {
        private const uint VendorId = 1848;
        private const uint ProductId = 41493;

        static async Task Main(string[] args)
        {
            Console.WriteLine("The current time is " + DateTime.Now);

            using GameControllerManager manager = new GameControllerManager();
            CancellationTokenSource cts = new CancellationTokenSource();
            MiniStickMonitor? monitor = null;
            Task? monitorTask = null;
            BlockingCollection<(double axis0, double axis1, Throttle.MiniStickDirection direction, bool warmup)> directionQueue = new BlockingCollection<(double, double, Throttle.MiniStickDirection, bool)>();

            void StartMonitor(RawGameController controller)
            {
                if (monitor == null)
                {
                    var throttle = new Throttle(controller);
                    monitor = new MiniStickMonitor(throttle, cts.Token, directionQueue);
                    monitorTask = Task.Run(() => monitor.Monitor());
                    Task.Run(() => LogDirections(directionQueue, cts.Token));
                }
            }

            manager.ControllerAdded += (sender, e) =>
            {
                if (e.HardwareVendorId == VendorId && e.HardwareProductId == ProductId)
                {
                    Console.WriteLine("Throttle connected: " + e.DisplayName);
                    StartMonitor(e);
                }
            };

            manager.ControllerRemoved += (sender, e) =>
            {
                if (e.HardwareVendorId == VendorId && e.HardwareProductId == ProductId)
                {
                    Console.WriteLine("Throttle disconnected: " + e.DisplayName);
                    cts.Cancel();
                    cts.Dispose();
                    cts = new CancellationTokenSource();
                    monitor?.ReleaseAllKeys();
                    monitor = null;
                }
            };

            Console.WriteLine("Waiting for throttle device to connect...");
            var throttleDevice = await manager.WaitForThrottleDeviceAsync(VendorId, ProductId);

            if (throttleDevice != null)
            {
                StartMonitor(throttleDevice);
            }

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
            cts.Cancel();

            if (monitorTask != null)
            {
                await monitorTask;
            }

            monitor?.ReleaseAllKeys();
            Console.WriteLine("Done");
        }

        static void LogDirections(BlockingCollection<(double axis0, double axis1, Throttle.MiniStickDirection direction, bool warmup)> directionQueue, CancellationToken token)
        {
            double lastAxis0 = 0, lastAxis1 = 0;
            Throttle.MiniStickDirection lastDirection = Throttle.MiniStickDirection.Neutral;
            bool lastWarmup = false;

            while (!token.IsCancellationRequested)
            {
                if (directionQueue.TryTake(out var item, 1000 / 10)) // Log at 10 Hz
                {
                    if (item.warmup != lastWarmup || item.axis0 != lastAxis0 || item.axis1 != lastAxis1 || item.direction != lastDirection)
                    {
                        Console.Clear();
                        if (item.warmup)
                        {
                            Console.WriteLine("Warm-up period: ignoring initial readings.");
                        }
                        else
                        {
                            Console.WriteLine($"Mini stick axis 0: {item.axis0}");
                            Console.WriteLine($"Mini stick axis 1: {item.axis1}");
                            Console.WriteLine($"Mini stick direction: {item.direction}");
                        }

                        lastAxis0 = item.axis0;
                        lastAxis1 = item.axis1;
                        lastDirection = item.direction;
                        lastWarmup = item.warmup;
                    }
                }
            }
        }
    }
}

class Throttle
{
    private readonly RawGameController throttle;
    private readonly bool[] buttons;
    private readonly GameControllerSwitchPosition[] switches;
    private readonly double[] axes;
    private const double Tolerance = 0.25; // Adjust this value as needed

    public Throttle(RawGameController throttle)
    {
        this.throttle = throttle;
        this.buttons = new bool[throttle.ButtonCount];
        this.switches = new GameControllerSwitchPosition[throttle.SwitchCount];
        this.axes = new double[throttle.AxisCount];
    }

    public MiniStickDirection GetCurrentMiniStickDirection(out double axis0, out double axis1)
    {
        throttle.GetCurrentReading(buttons, switches, axes);

        axis0 = axes[6];
        axis1 = axes[7];

        return GetMiniStickDirection(axis0, axis1);
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

class MiniStickMonitor : IDisposable
{
    private readonly Throttle throttle;
    private readonly CancellationToken token;
    private readonly InputSimulator inputSimulator;
    private readonly BlockingCollection<(double axis0, double axis1, Throttle.MiniStickDirection direction, bool warmup)> directionQueue;
    private readonly int warmupPeriod = 5000; // Warm-up period in milliseconds
    private Throttle.MiniStickDirection lastDirection = Throttle.MiniStickDirection.Neutral;

    public MiniStickMonitor(Throttle throttle, CancellationToken token, BlockingCollection<(double axis0, double axis1, Throttle.MiniStickDirection direction, bool warmup)> directionQueue)
    {
        this.throttle = throttle;
        this.token = token;
        this.inputSimulator = new InputSimulator();
        this.directionQueue = directionQueue;
    }

    public void Monitor()
    {
        var warmupEndTime = DateTime.Now.AddMilliseconds(warmupPeriod);

        while (!token.IsCancellationRequested)
        {
            double axis0, axis1;
            var direction = throttle.GetCurrentMiniStickDirection(out axis0, out axis1);
            bool warmup = DateTime.Now < warmupEndTime;

            directionQueue.Add((axis0, axis1, direction, warmup));

            if (!warmup && direction != lastDirection)
            {
                UpdateKeys(lastDirection, direction);
                lastDirection = direction;
            }

            Thread.Sleep(1000 / 60); // Poll input at 60 Hz
        }

        ReleaseAllKeys();
    }

    private void UpdateKeys(Throttle.MiniStickDirection lastDirection, Throttle.MiniStickDirection newDirection)
    {
        var lastKeys = GetKeysForDirection(lastDirection);
        var newKeys = GetKeysForDirection(newDirection);

        // Release keys that are no longer needed
        foreach (var key in lastKeys.Except(newKeys))
        {
            inputSimulator.Keyboard.KeyUp(key);
        }

        // Press keys that are newly needed
        foreach (var key in newKeys.Except(lastKeys))
        {
            inputSimulator.Keyboard.KeyDown(key);
        }
    }

    private IEnumerable<VirtualKeyCode> GetKeysForDirection(Throttle.MiniStickDirection direction)
    {
        switch (direction)
        {
            case Throttle.MiniStickDirection.Up:
                return new[] { VirtualKeyCode.VK_W };
            case Throttle.MiniStickDirection.Down:
                return new[] { VirtualKeyCode.VK_S };
            case Throttle.MiniStickDirection.Left:
                return new[] { VirtualKeyCode.VK_A };
            case Throttle.MiniStickDirection.Right:
                return new[] { VirtualKeyCode.VK_D };
            case Throttle.MiniStickDirection.TopRight:
                return new[] { VirtualKeyCode.VK_W, VirtualKeyCode.VK_D };
            case Throttle.MiniStickDirection.TopLeft:
                return new[] { VirtualKeyCode.VK_W, VirtualKeyCode.VK_A };
            case Throttle.MiniStickDirection.BottomRight:
                return new[] { VirtualKeyCode.VK_S, VirtualKeyCode.VK_D };
            case Throttle.MiniStickDirection.BottomLeft:
                return new[] { VirtualKeyCode.VK_S, VirtualKeyCode.VK_A };
            case Throttle.MiniStickDirection.Neutral:
                return Enumerable.Empty<VirtualKeyCode>();
            default:
                return Enumerable.Empty<VirtualKeyCode>();
        }
    }

    public void ReleaseAllKeys()
    {
        var allKeys = new[]
        {
            VirtualKeyCode.VK_W,
            VirtualKeyCode.VK_A,
            VirtualKeyCode.VK_S,
            VirtualKeyCode.VK_D
        };

        foreach (var key in allKeys)
        {
            inputSimulator.Keyboard.KeyUp(key);
        }
    }

    public void Dispose()
    {
        ReleaseAllKeys();
    }
}

class GameControllerManager : IDisposable
{
    private readonly object controllerLock = new object();
    private readonly HashSet<RawGameController> controllers = new HashSet<RawGameController>();

    public event EventHandler<RawGameController>? ControllerAdded;
    public event EventHandler<RawGameController>? ControllerRemoved;

    public GameControllerManager()
    {
        RawGameController.RawGameControllerAdded += OnRawGameControllerAdded;
        RawGameController.RawGameControllerRemoved += OnRawGameControllerRemoved;

        lock (controllerLock)
        {
            foreach (var rawGameController in RawGameController.RawGameControllers)
            {
                controllers.Add(rawGameController);
            }
        }
    }

    private void OnRawGameControllerAdded(object? sender, RawGameController e)
    {
        lock (controllerLock)
        {
            controllers.Add(e);
        }
        ControllerAdded?.Invoke(this, e);
    }

    private void OnRawGameControllerRemoved(object? sender, RawGameController e)
    {
        lock (controllerLock)
        {
            controllers.Remove(e);
        }
        ControllerRemoved?.Invoke(this, e);
    }

    public bool IsControllerConnected(uint vendorId, uint productId)
    {
        lock (controllerLock)
        {
            return controllers.Any(c => c.HardwareVendorId == vendorId && c.HardwareProductId == productId);
        }
    }

    public async Task<RawGameController?> WaitForThrottleDeviceAsync(uint vendorId, uint productId)
    {
        var tcs = new TaskCompletionSource<RawGameController?>();

        EventHandler<RawGameController>? addedHandler = null;
        addedHandler = (sender, e) =>
        {
            if (e.HardwareVendorId == vendorId && e.HardwareProductId == productId)
            {
                tcs.TrySetResult(e);
                ControllerAdded -= addedHandler;
            }
        };

        ControllerAdded += addedHandler;

        // Check again to avoid race conditions
        lock (controllerLock)
        {
            var existingController = controllers.FirstOrDefault(c => c.HardwareVendorId == vendorId && c.HardwareProductId == productId);
            if (existingController != null)
            {
                tcs.TrySetResult(existingController);
                ControllerAdded -= addedHandler;
            }
        }

        return await tcs.Task.ConfigureAwait(false);
    }

    public void Dispose()
    {
        RawGameController.RawGameControllerAdded -= OnRawGameControllerAdded;
        RawGameController.RawGameControllerRemoved -= OnRawGameControllerRemoved;
    }
}
