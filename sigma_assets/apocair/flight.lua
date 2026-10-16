-- Apoc-Air's flight controls for a gyrocopter: the keys and the mouse to the rotor's stick and pedals and the engine's
-- throttle, with the hand shove that starts the rotor and the stabiliser. They take the controls while someone sits in
-- the driver's seat (the seat's input object hears DriveOn and DriveOff); with nobody aboard the throttle is shut, the
-- wheel brakes are on, and the stabiliser is off.
--   flight.controls(g, { input = "INPUT", rotor = "Rotor Hub" })
local flight = {}

function flight.controls(g, o)
  o = o or {}
  o.rotor = o.rotor or "Rotor Hub"
  actions {
    pedals_raw = {
      { GetAxis = { axisName = "Throttle", multiplier = 1, store = "{up}", everyFrame = true } },
      { GetAxis = { axisName = "Brakes", multiplier = 1, store = "{down}", everyFrame = true } },
    },
    pedals_normalized = {
      { GetAxis = { axisName = "Throttle", multiplier = 1, store = "{up}", everyFrame = true } },
      { GetAxis = { axisName = "Brakes", multiplier = 1, store = "{down}", everyFrame = true } },
      {
        FloatRemap = {
          theFloat = "{up}",
          baseStart = -1,
          baseEnd = 1,
          targetStart = 0,
          targetEnd = 1,
          storeResult = "{up}",
          everyFrame = true,
        },
      },
      {
        FloatRemap = {
          theFloat = "{down}",
          baseStart = -1,
          baseEnd = 1,
          targetStart = 0,
          targetEnd = 1,
          storeResult = "{down}",
          everyFrame = true,
        },
      },
    },
    fly = {
      {
        FloatOperator = {
          float1 = "{up}",
          float2 = "{down}",
          operation = "Subtract",
          storeResult = "{rate}",
          everyFrame = true,
        },
      },
      { FloatMultiply = { floatVariable = "{rate}", multiplyBy = 0.5, everyFrame = true } },
      { FloatAdd = { floatVariable = "{lever}", add = "{rate}", everyFrame = true, perSecond = true } },
      { FloatClamp = { floatVariable = "{lever}", minValue = 0, maxValue = 1, everyFrame = true } },
      { GetButton = { buttonName = "Handbrake", storeResult = "{kbrake}", everyFrame = true } },
      {
        ConvertBoolToFloat = {
          boolVariable = "{kbrake}",
          floatVariable = "{Brakes}",
          falseValue = 0,
          trueValue = 1,
          everyFrame = true,
        },
      },
      { GetAxis = { axisName = "Steering", multiplier = 1, store = "{Steering}", everyFrame = true } },
      { GetButton = { buttonName = "ShiftUp", storeResult = "{kf}", everyFrame = true } },
      { GetButton = { buttonName = "ShiftDown", storeResult = "{kb}", everyFrame = true } },
      { GetKey = { key = "RightArrow", storeResult = "{kr}", everyFrame = true } },
      { GetKey = { key = "LeftArrow", storeResult = "{kl}", everyFrame = true } },
      {
        ConvertBoolToFloat = { boolVariable = "{kf}", floatVariable = "{sf}", falseValue = 0, trueValue = 1, everyFrame = true },
      },
      {
        ConvertBoolToFloat = { boolVariable = "{kb}", floatVariable = "{sb}", falseValue = 0, trueValue = 1, everyFrame = true },
      },
      {
        ConvertBoolToFloat = { boolVariable = "{kr}", floatVariable = "{sr}", falseValue = 0, trueValue = 1, everyFrame = true },
      },
      {
        ConvertBoolToFloat = { boolVariable = "{kl}", floatVariable = "{sl}", falseValue = 0, trueValue = 1, everyFrame = true },
      },
      {
        FloatOperator = {
          float1 = "{sf}",
          float2 = "{sb}",
          operation = "Subtract",
          storeResult = "{pitch}",
          everyFrame = true,
        },
      },
      {
        FloatOperator = { float1 = "{sr}", float2 = "{sl}", operation = "Subtract", storeResult = "{roll}", everyFrame = true },
      },
      {
        SetProperties = {
          on = "root:VehicleController",
          everyFrame = true,
          ["input.Throttle"] = "{lever}",
          ["input.Brakes"] = "{Brakes}",
          ["input.Steering"] = "{Steering}",
        },
      },
      {
        SetProperties = { on = o.rotor .. ":ApocAir.Rotor", everyFrame = true, stickPitch = "{pitch}", stickRoll = "{roll}" },
      },
      { SetProperties = { on = o.rotor .. ":ApocAir.Rotor", pedals = "{Steering}", everyFrame = true } },
    },
  }
  local spec = g.nodes[o.input or "INPUT"] or {}
  g.nodes[o.input or "INPUT"] = spec
  spec.parent = "root"
  spec.playmaker = {
    Input = {
      variables = {
        float = {
          lever = 0,
          up = 0,
          down = 0,
          rate = 0,
          Brakes = 0,
          Steering = 0,
          sf = 0,
          sb = 0,
          sr = 0,
          sl = 0,
          pitch = 0,
          roll = 0,
        },
        bool = { kf = false, kb = false, kr = false, kl = false, kbrake = false },
      },
      globalEvents = { "DriveOn", "DriveOff" },
      global = { DriveOn = "check", DriveOff = "off" },
      start = "off",
      states = {
        off = {
          actions = {
            { SetFloatValue = { floatVariable = "{lever}", floatValue = 0 } },
            {
              -- nobody aboard: throttle shut, wheel brakes on, wheel straight
              SetProperties = { on = "root:VehicleController", ["input.Throttle"] = 0, ["input.Brakes"] = 1, ["input.Steering"] = 0 },
            },
            { SetProperties = { on = o.rotor .. ":ApocAir.Rotor", stickPitch = 0, stickRoll = 0 } },
            { SetProperties = { on = o.rotor .. ":ApocAir.Rotor", pedals = 0 } },
          },
        },
        check = {
          actions = {
            {
              SetProperties = { on = "root:VehicleController", ["steering.degreesPerSecondLimit"] = "{steeringspeed}" },
            },
            { BoolTest = { boolVariable = "{normalizeinput}", isTrue = "normalized", isFalse = "raw" } },
          },
          transitions = { normalized = "normalized", raw = "raw" },
        },
        raw = { actions = { { use = "pedals_raw" }, { use = "fly" } } },
        normalized = { actions = { { use = "pedals_normalized" }, { use = "fly" } } },
      },
    },
    HandSpin = {
      globalEvents = { "DriveOn", "DriveOff" },
      global = { DriveOn = "seated", DriveOff = "off" },
      start = "off",
      states = {
        off = {},
        seated = {
          actions = { { GetButtonDown = { buttonName = "Horn", sendEvent = "push" } } },
          transitions = { push = "push" },
        },
        push = {
          actions = { { SetProperties = { on = o.rotor .. ":ApocAir.Rotor", handPush = true } } },
          transitions = { FINISHED = "seated" },
        },
      },
    },
    MouseStick = {
      variables = { float = { mx = 0, my = 0 }, gameObject = { MouseSteeringUI = null } },
      globalEvents = { "DriveOn", "DriveOff" },
      global = { DriveOn = "keys", DriveOff = "off" },
      start = "off",
      states = {
        off = { actions = { { SetProperties = { on = o.rotor .. ":ApocAir.Rotor", useMouse = false } } } },
        keys = {
          actions = {
            { SetProperties = { on = o.rotor .. ":ApocAir.Rotor", useMouse = false } },
            { SendEvent = { eventTarget = "{Player}/Look", sendEvent = "Deactivate" } },
            {
              FindGameObject = { objectName = "MouseSteeringUI", withTag = "Untagged", store = "{MouseSteeringUI}" },
            },
            { ActivateGameObject = { gameObject = "{MouseSteeringUI}", activate = false } },
            { GetButtonDown = { buttonName = "Mouse Steering", sendEvent = "toggle" } },
          },
          transitions = { toggle = "mouse" },
        },
        mouse = {
          actions = {
            { SetProperties = { on = o.rotor .. ":ApocAir.Rotor", useMouse = true } },
            { SendEvent = { eventTarget = "{Player}/Look", sendEvent = "MouseSteeringLook" } },
            {
              FindGameObject = { objectName = "MouseSteeringUI", withTag = "Untagged", store = "{MouseSteeringUI}" },
            },
            { ActivateGameObject = { gameObject = "{MouseSteeringUI}", activate = true } },
            { GetMouseX = { storeResult = "{mx}", normalize = true, everyFrame = true } },
            { GetMouseY = { storeResult = "{my}", normalize = true, everyFrame = true } },
            {
              FloatRemap = {
                theFloat = "{mx}",
                baseStart = 0,
                baseEnd = 1,
                targetStart = -1,
                targetEnd = 1,
                storeResult = "{mx}",
                everyFrame = true,
              },
            },
            {
              FloatRemap = {
                theFloat = "{my}",
                baseStart = 0,
                baseEnd = 1,
                targetStart = -1,
                targetEnd = 1,
                storeResult = "{my}",
                everyFrame = true,
              },
            },
            {
              SetProperties = { on = o.rotor .. ":ApocAir.Rotor", everyFrame = true, mouseRoll = "{mx}", mousePitch = "{my}" },
            },
            { GetButtonDown = { buttonName = "Mouse Steering", sendEvent = "toggle" } },
          },
          transitions = { toggle = "keys" },
        },
      },
    },
    Stabiliser = {
      globalEvents = { "DriveOn", "DriveOff" },
      global = { DriveOn = "on", DriveOff = "idle" },
      start = "idle",
      states = {
        -- nobody aboard (and when it spawns): the stabiliser off, so nothing flies it
        idle = { actions = { { SetProperties = { on = o.rotor .. ":ApocAir.Rotor", stabilise = false } } } },
        on = {
          actions = {
            { SetProperties = { on = o.rotor .. ":ApocAir.Rotor", stabilise = true } },
            { GetButtonDown = { buttonName = "Cruise Control", sendEvent = "toggle" } },
          },
          transitions = { toggle = "off" },
        },
        off = {
          actions = {
            { SetProperties = { on = o.rotor .. ":ApocAir.Rotor", stabilise = false } },
            { GetButtonDown = { buttonName = "Cruise Control", sendEvent = "toggle" } },
          },
          transitions = { toggle = "on" },
        },
      },
    },
  }
end

return flight
