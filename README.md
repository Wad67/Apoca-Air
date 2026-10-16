# Apoc-Air

A BepInEx 5 plugin for Apocalypter: aircraft. It has a flyable **gyroplane** in the style of the one in Mad Max 2;
a small **bush plane** is planned.

The airframes are SigmaMod templates and bring what makes them aircraft: the gyro's wheels, rotor, pusher propeller
and tail. You fit the rest as on any vehicle: engine, radiator, seat, gauges. The engine drives the propeller
directly through its crankshaft; there is no gearbox in the way. Engine Sim is optional. Spawn the gyroplane with Apocaspawner.

## How it flies

Everything is physics, worked out from the air's speed over each surface, with no scripted flight modes:

- **Air:** density from height and the game's air temperature. A hot day or a high field takes thrust and lift away.
- **Propeller:** blade element momentum theory. It loads the engine with the torque it takes at its speed and
  airspeed. Standing still it holds the engine below its top rpm; in flight it unloads; unpowered at speed it
  windmills.
- **Gyroplane rotor:** blade elements around the disc, with the blades free to flap on their teeter hinge. Nothing
  drives it in flight. Air coming up through the tilted-back disc keeps it turning (autorotation), at the speed where
  the driving inner disc and the dragging outer disc balance. Too slow, and it slows down and stops lifting.
- **Wings and tail:** lifting surfaces that stall.

### Checked offline

`tools/AeroTest` runs the same code outside the game against real aircraft:

| | Real | Here |
|---|---|---|
| 8.4 m two-blade gyro rotor (Magni M16 class), cruise rotor speed | about 330 - 380 rpm | 361 - 375 rpm |
| same, disc angle to carry 450 kg at 25 - 35 m/s | about 5 - 10 deg | 4 - 8 deg |
| 1.73 m three-blade pusher prop on 84 kW, static thrust | 2.5 - 3 kN | 2.6 kN |
| 72 x 42 Cub prop on 63 kW, static rpm | about 2250 | about 2400 |

```
dotnet run -c Release --project tools/AeroTest [-- airfoil | prop | rotor]
```

## Flying the gyroplane

| Key | |
|---|---|
| W / S | throttle up / down; it stays where you leave it |
| E / Q | stick forward / back (tilts the rotor head) |
| Left / Right arrows | stick left / right |
| O | mouse stick on / off (the game's mouse steering key): the cursor's place on screen is the stick, up is forward |
| V | stabiliser on / off (the game's cruise control key) |
| A / D | rudder and nose wheel |
| Space | wheel brakes |
| H | shove a blade by hand |

Nothing drives the rotor. A stopped rotor gets almost no turning torque from air across it, so start it by hand
(H: a shove per press, up to about 120 rpm), then taxi with the stick
back. Air coming up through the tilted disc winds it up, quickly from about 120 rpm; it lifts once
it's fast enough. A panel at the top left shows the controls, airspeed, height and the
engine, prop and rotor speeds while you're in the seat.

The stabiliser (on to start with) is an autopilot servo on the stick: it moves the rotor head, it adds no forces.
In the air, stick forward or back asks for the nose to go down or up and letting go holds the attitude; stick left or
right asks for a bank of up to 30 degrees and centred levels the wings. On the ground pitch goes straight through and
it only damps roll. It trims out the steady roll from the prop's torque and the rotor's sideways tilt (the HUD shows how
much), and in the air it works the rudder on top of your pedals to keep the sideslip out and damp the yaw.
A gyroplane is unstable in pitch on its own (the rotor's force swings further ahead of the centre of mass as the nose
rises), and this one has no tailplane, so flying it by keyboard without the stabiliser is hard.

With nobody in the seat the throttle is shut, the wheel brakes are on and the stabiliser is off, so a parked
gyroplane stays parked. The stabiliser comes back on when you sit down.

## Building aircraft

The gyroplane is a SigmaMod 2.0 Lua asset: `sigma_assets/apocagyro.lua`, built from SigmaMod's parts library and
Apoc-Air's own `sigma_assets/apocair/flight.lua` (`require "apocair.flight"`), which holds the flight controls: keys
and mouse to the stick, pedals and throttle, the hand shove and the stabiliser, taken over while someone is in the
seat. Another aircraft can require it the same way.

Components a SigmaMod template adds and sets (fields are listed at the top of each file in `Components/`):

- `ApocAir.Propeller`: a fixed-pitch prop on the engine's crankshaft, thrust along the object's forward.
- `ApocAir.Rotor`: a free gyroplane rotor; the stick tilts its head.
- `ApocAir.Surface`: a wing, tailplane or fin, with an optional control surface; the part of it in a prop's
  slipstream sees the faster air.
- `ApocAir.Drag`: the drag of everything else.
- `ApocAir.MassCentre`: where the centre of mass is.
- `ApocAir.Cockpit`: the on-screen panel.
- `ApocAir.TyreContactMass`: on the root, gives NWH's tyres the mass they really push against at the ground (it
  allows for the body turning). Without it a light, tall, narrow aircraft rocks itself over on its wheels.

## Requirements

- BepInEx 5
- Engine Sim 1.5.0 or later is optional. With it, the propeller sits on its simulated engine's crankshaft; without
  it, on the game's own engine model
- SigmaMod 2.0.0 or later, which builds the airframes
- Apocaspawner, to spawn one

## Install

Unzip the release into `Apocalypter/BepInEx/plugins`, so that `BepInEx/plugins/ApocAir/ApocAir.dll` exists.

## Build

```
dotnet build -c Release
```

Builds against the game's own assemblies, the Engine Sim repo's build (`../EngineSim`) and the installed SigmaMod, and deploys to
`BepInEx/plugins/ApocAir/`. Set `GameDir` if the repo is not in the game folder.
