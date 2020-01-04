# Virt-A-Mate Timeline

An animation timeline with keyframe and controllable curves

## Features

- Animate controllers by creating frames for each controller and loop them
- Scrubbing, playback, and frame navigation
- Curve control
- Link to animation pattern
- Multiple animations and animation blending
- Cut, Copy, Paste
- Custom frame display
- External playback controller
- Sync multiple atom animations

## Installing

Download `VamTimeline.zip` from [Releases](https://github.com/acidbubbles/vam-timeline/releases) and extract it into `(VaM Install Folder)/Custom/Scripts/Acidbubbles/VamTimeline`.

## Basic Setup

It is expected that you have some basic knowledge of [how Virt-A-Mate works](https://www.reddit.com/r/VirtAMate/wiki/index) before getting started. Basic knowledge of keyframe based animation is also useful. In a nutshell, you specify some positions at certain times, and all positions in between will be interpolated using curves (linear, smooth, etc.).

### Animating an atom

![VamTimeline Atom Plugin](screenshots/vam-timeline-atom.png)

1. Add the `VamTimeline.AtomAnimation.cslist` plugin on atoms you want to animate, and open the plugin settings (`Open Custom UI` in the Atom's `Plugin` section).
2. On the right side, select a controller you want to animate in the `Animate Controller` drop down, and select `Add / Remove Controller` to attach it. This will turn on the "position" and "rotation" controls for that controller.
3. To add a frame, move the `Time` slider to where you want to create a keyframe, and move the atom. This will create a new keyframe.
4. Check the `Display` text field; you can see all the keyframes you have created there, and visualize which one you have selected.
5. Move from a frame to another using the `Next Frame` and `Previous Frame` buttons.
6. Play your animation using the `Play` button, and stop it using the `Stop` button (was this instruction really useful?)

### Controller filtering

To allow moving between the frames of a specific controller, or to make the `Change Curve`, `Cut`, `Copy` and `Paste` features only affect a single frame of a single controller, you can use the `Selected Controller` drop down. Note that this will still allow you to move and create frames for other controllers.

### Multiple animations

You can add animations with `Add New Animation`. This will port over the _current_ displayed frame, as well as all controllers. Note that if you later add more controllers, they will not be assigned to all animations. This means that when you switch between animations, controllers not in the second animation will simply stay where they currently are.

You can switch between animations using the `Animation` drop down. When the animation is playing, it will smoothly blend between animations during the value specified in `Blend Duration`.

### Controlling curves

By default, the first frame will always synchronize the last frame and smooth the curve. You therefore cannot make the first/last frame linear.

Otherwise, you can use the `Change Curve` drop down when a frame is selected to change the curve style for that frame.

To smooth everything, use `Smooth all curves`.

### Triggering events

To use events, upi cam ise an `AnimationPattern` of the same length as the animation. When an Animation Pattern is linked, it will play, stop and scrub with the VamTimeline animation.

### Controlling morphs and other float parameters

Add the `VamTimeline.FloatParamAnimation.cslist` plugin on atoms you want to animate, and open the plugin settings (`Open Custom UI` in the Atom's `Plugin` section). It works in a similar way to the atom plugin, except you can add any float parameter. For morphs, make sure you enable the Animatable parameter in the morphs panel, and you'll find them under geometry.

### Adding an external playback controller

![VamTimeline Controller Plugin](screenshots/vam-timeline-controller.png)

This allows creating a floating payback controller, and control multiple atoms together. Create a `Simple Sign` atom and add the script to it. This is optional, you only need this if you want to animate more than one atom, or if you want the floating playback controls.

Add the `VamTimeline.Controller.cslist` plugin on a `Simple Sign` atom.

In the plugin settings, select the animations you want to control and select `Link`.

You can now control the animations in the floating panel; you can also select which atom and animation to play.

Note that all specified atoms must contain the same animations, and animations must have the same length.

## Development

The paths to the VaM dll files are relative, so clone into `(VaM Install Folder)/Custom/Scripts/Dev/vam-timeline` for example.

When reloading a Virt-A-Mate script after it was modified externally, you will lose your data. For complex animations, it can be a frustrating workflow. Add the `VamTimeline.Backup.cslist` script on all atoms that have the `VamTimeline.AtomAnimation.cslist`.

This allows for reloading the main script without losing your data. Mosty useful for development. Add to atoms with VamTimeline.cslist. Unnecessary with normal use.

## License

[MIT](LICENSE.md)
