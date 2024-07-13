# Saitek X-55 HOTAS Throttle Mini Joystick to WASD Mapper

## Overview

Hey there! I created this small program to solve a very specific problem with my Saitek X-55 HOTAS Throttle. The throttle has a mini joystick near the thumb, which unfortunately isn't detected by the game X4: Foundations. In the unlikely case you're facing the same issue, this project might help you out.

## Background

![x55-hotas-throttle-mini-stick](https://github.com/user-attachments/assets/966e3109-88cb-4699-907a-4f564ecee20c)

I recently started playing [X4: Foundations](https://store.steampowered.com/app/392160/X4_Foundations/) and decided to try setting up my HOTAS with it today. While I was mapping the controls (following [this video](https://www.youtube.com/watch?v=Mog0rcd5aH4), super helpful), everything seemed to work great until I got to the mini joystick on the throttle. Strangely, the game didn't respond to it at all. I knew it wasn't a hardware issue because https://hardwaretester.com/gamepad detected it just fine.

As a workaround, I wanted to remap the mini joystick to the WASD keys on my keyboard. However, the software that came with my HOTAS, which has a remapping/macro feature, is quite old and didn't seem to work for this.

I also tried using Autohotkey, but it didn't detect the mini joystick either. Apparently, this is because the Windows Joystick API used by Autohotkey only supports up to 6 axes on a device ([source](https://www.autohotkey.com/board/topic/78802-fact-7axis-joystick-to-many-analog-for-ahk/)). I also gave [UCR](https://github.com/Snoothy/UCR) a shot, and while it looked promising, it also didn't detect the joystick.

## Solution

The `Windows.Gaming.Input` API has a [`RawGameController` class](https://learn.microsoft.com/en-us/uwp/api/windows.gaming.input.rawgamecontroller?view=winrt-26100) that seemed like it would be able to detect the mini joystick, as the axes are [output to an array](https://learn.microsoft.com/en-us/uwp/api/windows.gaming.input.rawgamecontroller.getcurrentreading?view=winrt-26100).

I decided to spend a few hours with GPT-4o to create a basic C# script that uses this API to remap the mini joystick axes to the WASD keys. I have little experience with idiomatic C# code or its ecosystem, but GPT-4o had that covered. It still ended up taking quite a bit of effort; the code GPT-4o produced required many rounds of reviewing, (manual) testing, and refactoring. Caught a number of logic bugs, race conditions, and general weirdness this way. But in the end, it worked!

The code isn't pretty and could do with some cleanup, but it's quite functional, which was my main goal. I have no plans for a refactor unless something breaks or someone else finds it useful.

## How It Works

This program detects the mini joystick's direction and maps it to corresponding WASD key presses. The keys are held down as long as the joystick is in a specific direction and are released immediately when the joystick returns to neutral, the device disconnects, or the program exits.

## Installation and Usage

1. Clone the repository.
2. Install the [H.InputSimulator](https://www.nuget.org/packages/H.InputSimulator) NuGet package.
3. Compile and run the program.
    - If you have a different model of HOTAS, you may need to change the `HardwareVendorId` and `HardwareProductId` in the `Program.cs` file accordingly. These can be found by adding some logging to the `manager.ControllerAdded` event handler. The logic in `GetCurrentMiniStickDirection`/`GetMiniStickDirection` may also need to be adapted for the different axes of your device.
4. Move the mini joystick on your X-55 HOTAS Throttle to see it remap to WASD keys.
5. Enjoy!

## License

This project is licensed under the MIT License. Feel free to use, modify, and distribute it as you wish.

## Contributing

I currently have no plans for further development unless something breaks. However, if you find this project useful and want to contribute, feel free to submit pull requests or fork the repository. Your contributions are welcome!
