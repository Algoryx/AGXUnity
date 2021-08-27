# This must be done first to set up the correct environment
# import Environment

import agx
import agxIO
import agxOSG
import agxPython
import agxCollide
import agxSDK
import agxUtil
import agxRender
import agxPowerLine
import sys
import os
import json
import ntpath
import math
import time

from Brick import Path, TypePath, Model
from brick.Core.decorator import registerAgxSimulationParameter, registerAgxGuiEventListener
from Brick.Simulation import Simulation
from Brick.AGXBrick import SimulationApp
from Brick.Physics import ComponentLoader

rcs = Simulation.RemoteCommandServer()
rcs.Enable = True
rcs.AllowExternal = True

angle1 = 20.0
angle2 = 30.0
angle3 = 45.0
deltaAngle1 = 20
deltaAngle2 = -30
deltaAngle3 = -90

def onReInitializeAtNewPosition(brickSimulation):
    global brick_instance, angle1, angle2, angle3, deltaAngle1, deltaAngle2, deltaAngle3

    angle1 = angle1 + deltaAngle1
    angle2 = angle2 + deltaAngle2
    angle3 = angle3 + deltaAngle3

    brick_instance['angle1'] = angle1
    brick_instance['angle2'] = angle2
    brick_instance['angle3'] = angle3

    deltaAngle1 = deltaAngle1*-1
    deltaAngle2 = deltaAngle2*-1
    deltaAngle3 = deltaAngle3*-1

    brick_instance.DefinedConnectorsOrder.Clear()
    ComponentLoader.RepositionComponent(brick_instance)
    brickSimulation.PositionAgxBodies()
    for constraint in brickSimulation.AgxSimulation.getConstraints():
        prismatic = constraint.asPrismatic()
        if prismatic != None:
            prismatic.getMotor1D().setLockedAtZeroSpeed(False)
            prismatic.getLock1D().setEnable(False)
    print(brick_instance['boomAngle'].GetTangentAngle())

def onGuiEvent(brickSimulation, key, modKey, x, y, keyDown):
    global brick_instance
    if keyDown:
        return True

    if key == ord('q'):

        onReInitializeAtNewPosition(brickSimulation)
        print(brick_instance['angle1'])
        print(brick_instance['angle2'])
        print(brick_instance['angle3'])
        return True

    return False


#brick_model = TypePath("Examples.FrictionExperiment.PendulumInWorld")
brick_model = TypePath("Examples.Forwarder.PositionedForwarder")

#brick_model = TypePath("Examples.Panda.Panda")
brick_instance = ComponentLoader.CreateComponentFromPath(brick_model)

simulationConfig = Simulation()
simulationConfig.InteractiveRemoteClient = True
simulationConfig.Scene = brick_instance
simulationConfig.Duration = math.inf
simulationConfig.StartPaused = True
simulationConfig.Rcs = rcs

simulationApp = SimulationApp(simulationConfig)
simulationApp.SetEnableControlChannelTickSignals(False)

registerAgxGuiEventListener(simulationApp.AGXBrickSimulation, onGuiEvent)
#simulationApp.AGXBrickSimulation.SynchronizeWithAgx = False

agx = simulationApp.AGXBrickSimulation.AgxSimulation
#agx.setTimeStep(0.001)
simulationApp.Start()
success = True
t = 0
count = 0
while(success):
    # success = simulationApp.Step(0,None,None,None,True)
    # count = count + 1
    # if (count > 500):
    #     time.sleep(1)
    #     count = 0
    #     ComponentLoader.RepositionComponent(brick_instance)
    #     simulationApp.AGXBrickSimulation.PositionAgxBodies()

    # if (simulationApp.AGXBrickSimulation.AgxSimulation.getTimeStamp() > t):
    #     t = t +10
    #     print(t)

    success = simulationApp.Step(0,None,None,None,True)


simulationApp.End()
