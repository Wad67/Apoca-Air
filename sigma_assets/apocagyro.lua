-- The gyrocopter (a Mad Max 2 style Bensen): an open frame with a pusher engine, a free-spinning rotor on a stick, two
-- fins and a tailplane, three wheels. Apoc-Air's components fly it (rotor, propeller, surfaces, drag); its flight
-- controls are in apocair/flight.lua. Each name is an object in apocagyro.glb.
local parts = require "sigma.parts"
local flight = require "apocair.flight"

local g = parts.chassis {
  mass = 300, drive = 1, speed = true, flip_over = true, clutch_capacity = false, dashboard = "Dashboard",
  add = { "ApocAir.TyreContactMass" },
  controller = {
    enginePosition = { 0, 0.85, -0.18 },
    transmissionPosition = { 0, 0.85, -0.18 },
    ["powertrain.wheelGroups.1.steerCoefficient"] = 0,
    ["powertrain.wheelGroups.2.steerCoefficient"] = 0,
    ["steering.speedSensitiveSteeringCurve"] = { { 0, 1 }, { 0.06, 1 }, { 0.1, 0.4 }, { 0.16, 0.16 }, { 0.24, 0.07 }, { 0.4, 0.03 }, { 1, 0.02 } },
  },
}
parts.colliders(g, "Main Collider", "Secondary Collider")

-- the engine and what it needs
parts.mount(g, "Engine Mount", "engine", { at = "Engine Hinge" })
parts.mount(g, "Radiator Mount", "radiator", { at = "Radiator Hinge" })
parts.engine_sound(g, { at = "Engine Mount" })
parts.fuel_tank(g, { tank = "Fuel Access Collider", filler = "Fuel Drain Siphon Collider", hose = "Fuel Hose End", litres = 10, capacity = 40,
                     hose_at = "Fuel Access Collider", hose_position = { -0.38, -0.25, 0 }, siphon_hose = "Siphon Hose",
                     saved = false, filler_render = false })
g.nodes["Siphon Hose"] = {
  parent = "root",
  at = "Fuel Access Collider",
  rotation = { 0, 0, -60 },
  add = { "MeshFilter", "MeshRenderer" },
  MeshFilter = { sharedMesh = "asset:Mesh/syphon_hose" },
  MeshRenderer = { sharedMaterial = "asset:Material/blackplastic_3", enabled = false },
}
parts.ignition(g, { switch = "Ignition Collider", key = "Ignition Collider", fuel = "Fuel Access Collider", engine = "Engine Mount" })
parts.slot(g, "Dashboard", "gauge", { use = "dashboard_attach", at = "Dashboard Collider", align = true })
parts.slot(g, "Seat Mount", "seat", { use = "wheel_attach", at = "Seat Hinge", align = true, marker = true })

-- the pilot: the seat, the controls, and the seated player model's pose
parts.driver_seat(g, { enter = "Enter Collider", exit = "Exit Collider", input = "INPUT", sit = "head", sit_position = { 0.011, -0.119, 0.177 },
                       exit_position = { -0.3, -0.3, 0 } })
flight.controls(g, { input = "INPUT", rotor = "Rotor Hub" })
g.nodes["Seat Frame"] = { parent = "root", at = "Player Skeleton Seated Position", align = "Player Skeleton Seated Position" }
g.nodes["PlayerModel_Sit"] = { parent = "Seat Frame", rotation = { -90, 0, 0 } }
parts.pose(g, {
  -- bone, its parent, position, rotation (quaternion)
  { "Armature", "PlayerModel_Sit", { 0.00024, -0.00034, 0.02148 }, { -0.70711, 0, 0, 0.70711 } },
  { "leg1 Left", "Armature", { -0.06687, 0.00797, -0.01647 }, { -0.70491, 0.23737, 0.66722, -0.03968 } },
  { "leg2 Left", "leg1 Left", { 0, 0.41828, 0 }, { -0.19268, 0.07586, -0.52015, 0.82859 } },
  { "leg3 Left", "leg2 Left", { 0, 0.50506, 0 }, { -0.03806, 0.07708, 0.69203, 0.71673 } },
  { "leg1 Right", "Armature", { 0.06687, 0.00797, -0.01647 }, { -0.76976, -0.26744, -0.57248, -0.09066 } },
  { "leg2 Right", "leg1 Right", { 0, 0.41828, 0 }, { -0.05069, -0.00102, 0.57008, 0.82002 } },
  { "leg3 Right", "leg2 Right", { 0, 0.50506, 0 }, { -0.03806, -0.07708, -0.69203, 0.71673 } },
  { "spine1", "Armature", { 0.00068, 0.01487, 0.00127 }, { 0.72799, -0.0032, 0.00279, 0.68557 } },
  { "spine2", "spine1", { 0, 0.10585, 0 }, { -0.16717, 0.00031, -0.0036, 0.98592 } },
  { "spin3", "spine2", { 0, 0.1814, 0 }, { 0.02758, -0.00021, -0.00272, 0.99962 } },
  { "arm1 Left", "spin3", { -0.092, 0.234, 0.035 }, { -0.08763, -0.11771, 0.72634, 0.67148 } },
  { "arm2Left", "arm1 Left", { -0.033, 0.085, 0.061 }, { 0.72915, 0.08719, 0.35012, 0.58151 } },
  { "arm3 Left", "arm2Left", { 0, 0.24643, 0 }, { 0.20753, -0.13899, 0.03317, 0.96774 } },
  { "hand1 Left", "arm3 Left", { 0, 0.24533, 0 }, { -0.06784, 0.00476, 0.05237, 0.99631 } },
  { "hand2 Left", "hand1 Left", { 0, 0.11356, 0 }, { -0.04152, 0.03621, 0.22536, 0.97272 } },
  { "hand3 Left", "hand2 Left", { 0, 0.04042, 0 }, { -0.10704, 0.14483, 0.47896, 0.85916 } },
  { "hand4 Left", "arm3 Left", { 0, 0.24533, 0 }, { 0.1866, 0.20656, 0.46874, 0.83833 } },
  { "hand5 Left", "hand4 Left", { 0, 0.087, 0 }, { -0.31117, -0.13121, -0.25775, 0.90528 } },
  { "arm1 Right", "spin3", { 0.107, 0.24, 0.05 }, { -0.1807, 0.14311, -0.69715, 0.67885 } },
  { "arm2Right", "arm1 Right", { 0.067, 0.126, 0.036 }, { 0.39075, -0.00633, -0.48387, 0.78304 } },
  { "arm3 Right", "arm2Right", { 0, 0.24643, 0 }, { 0.21053, -0.00204, -0.04048, 0.97675 } },
  { "hand1 Right", "arm3 Right", { 0, 0.24533, 0 }, { -0.06784, -0.00476, -0.05237, 0.99631 } },
  { "hand2 Right", "hand1 Right", { 0, 0.11356, 0 }, { -0.04152, -0.03621, -0.22536, 0.97272 } },
  { "hand3 Right", "hand2 Right", { 0, 0.04042, 0 }, { -0.10704, -0.14483, -0.47896, 0.85917 } },
  { "hand4 Right", "arm3 Right", { 0, 0.24533, 0 }, { 0.1866, -0.20656, -0.46874, 0.83833 } },
  { "hand5 Right", "hand4 Right", { 0, 0.087, 0 }, { -0.31117, 0.13121, 0.25775, 0.90528 } },
  { "neck", "spin3", { -0.00076, 0.2859, 0.01844 }, { 0.39605, 0.00067, 0.00199, 0.91823 } },
  { "head", "neck", { 0, 0.05819, 0 }, { -0.1751, 0, 0, 0.98455 } },
})

-- the wheels
parts.free_wheel(g, "Nose Wheel", { mesh = "ID117", spin = "Nose Spin", position = { 0, 0.1, 0 }, radius = 0.11, width = 0.1, mass = 3,
                                    spring = 0.1, stiffness = 5000, damper = 4000, rolling = 0.99 })
parts.free_wheel(g, "Left Wheel", { mesh = "SideWheel", spin = "Left Spin", position = { 0, 0.1, 0 }, radius = 0.16, width = 0.15, mass = 6,
                                    spring = 0.1, stiffness = 4500, damper = 3000, rolling = 6.0 })
parts.free_wheel(g, "Right Wheel", { mesh = "SideWheel.001", spin = "Right Spin", position = { 0, 0.1, 0 }, radius = 0.16, width = 0.15, mass = 6,
                                     spring = 0.1, stiffness = 4500, damper = 3000, rolling = 6.0 })
parts.free_wheel(g, "Tail Wheel", { mesh = "Rear Wheel", spin = "Tail Spin", position = { 0, 0.05, 0 }, radius = 0.075, width = 0.03, mass = 2,
                                    spring = 0.05, stiffness = 1200, damper = 1000, rolling = 0.34, extra = { ["sideFriction.grip"] = 0.3 } })

-- what flies it (Apoc-Air's components)
g.nodes["Mass Centre"] = {
  parent = "root",
  position = { 0, 0.6, 0.1 },
  add = { "ApocAir.MassCentre" },
  ["ApocAir.MassCentre"] = { inertia = { 250, 230, 60 } },
}
g.nodes["Rotor Hub"] = {
  parent = "root",
  at = "RotorAxle",
  add = { "ApocAir.Rotor" },
  ["ApocAir.Rotor"] = {
    head = "RotorAxle",
    spin = "Blade Plate",
    radius = 2.7,
    rootCutout = 0.3,
    blades = 2,
    chord = 0.2,
    pitch = 2.5,
    sense = -1,
    bladeMass = 7,
    bearingFriction = 0.5,
    tiltForward = 12,
    tiltBack = 12,
    tiltSide = 8,
    handImpulse = 25,
    handSpeed = 33.9,
    stick = "Control Stick",
    stickAngle = 15,
    rudders = { "Fin L:ApocAir.Surface", "Fin R:ApocAir.Surface" },
  },
}
g.nodes["Prop Shaft"] = {
  parent = "root",
  at = "PusherProp",
  add = { "ApocAir.Propeller" },
  ["ApocAir.Propeller"] = {
    visual = "PusherProp",
    radius = 0.64,
    hubRadius = 0.08,
    blades = 2,
    chord = 0.1,
    taper = 0.7,
    pitch = 0.9,
    ratio = 1,
    inertia = 0.35,
    sense = -1,
  },
}
g.nodes["Body Drag"] = {
  parent = "root",
  position = { 0, 0.6, 0.2 },
  add = { "ApocAir.Drag" },
  ["ApocAir.Drag"] = { dragArea = { 1.2, 1.5, 0.6 } },
}
g.nodes["Fin L"] = {
  parent = "root",
  at = "Tail",
  position = { -0.345, 0.014, 0.19 },
  add = { "ApocAir.Surface" },
  ["ApocAir.Surface"] = {
    vertical = true,
    area = 0.336,
    aspectRatio = 0.77,
    stall = 22,
    controlAngle = -25,
    controlEffect = 0.5,
    propwash = "Prop Shaft:ApocAir.Propeller",
    span = 0.51,
  },
}
g.nodes["Fin R"] = {
  parent = "root",
  at = "Tail",
  position = { 0.345, 0.014, 0.19 },
  add = { "ApocAir.Surface" },
  ["ApocAir.Surface"] = {
    vertical = true,
    area = 0.336,
    aspectRatio = 0.77,
    stall = 22,
    controlAngle = -25,
    controlEffect = 0.5,
    propwash = "Prop Shaft:ApocAir.Propeller",
    span = 0.51,
  },
}
g.nodes["Tailplane"] = {
  parent = "root",
  at = "Tail Plane",
  position = { 0, -0.02, 0.03 },
  add = { "ApocAir.Surface" },
  ["ApocAir.Surface"] = {
    vertical = false,
    area = 0.26,
    aspectRatio = 1.8,
    stall = 18,
    controlAngle = 0,
    propwash = "Prop Shaft:ApocAir.Propeller",
    span = 0.686,
  },
}
g.nodes["Cockpit"] = {
  parent = "root",
  add = { "ApocAir.Cockpit" },
  ["ApocAir.Cockpit"] = {
    controls = "W / S  throttle up / down (stays set)|E / Q  stick forward / back|Left / Right arrows  stick left / right|O  mouse stick on / off (cursor up = forward)|V  stabiliser on / off|A / D  rudder and nose wheel|Space  wheel brakes|H  shove a blade by hand (up to about 120 rpm), then taxi with the stick back|F  (at the side)  get out",
  },
}

parts.toolset(g, "Toolset", { at = "Tool 1 location", mesh = "asset:Mesh/toolset.002", material = "asset:Material/tools_1" })

template "gyro_mad_max" (g)
