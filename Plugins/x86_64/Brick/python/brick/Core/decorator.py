# from .Logger import logging
import logging

import clr
# clr.AddReference('System')
# clr.AddReference('System.Core')
clr.AddReference('brick')
from Brick import Path, TypePath, Model, ModelRegistry
# from System import Action, Func

_initializers = {}

def triggerOnInit(pathChain, val):
    # print(f'Trigger onInit: {val._ModelValuePath}')
    for path in pathChain:
        fn = _initializers.get(path)
        if fn is not None:
            logging.debug(f'Trigger onInit: {path}')
            fn(val)


# https://realpython.com/primer-on-python-decorators/#decorators-with-arguments
def onInit(path: str):
    '''
    Decorator to register callback for Brick model initialization
    '''
    def wrapFn(fn):
        logging.debug(f'Register brick init function {fn} for model {path}')

        # Would preferrably pass the callback directly to brick using `Action[Brick.Object](fn)`, but this does currently not work
        # Action = getattr(System, "Action`1")

        ModelRegistry.RegisterPythonModelInitializer(Path(path))
        _initializers[path] = fn
        return fn
    return wrapFn

triggerAgxParameterCallback = None
triggerAgxGuiEventCallback = None

try:
    from Brick.AgxBrick import BrickSimulation

    _agxParameterCallbacks = {}

    def triggerAgxParameterCallback(sim, name, val):
        # print(f'triggerAgxParameterCallback: {name} = {val}')
        fn = _agxParameterCallbacks.get(name)
        if fn is not None:
            logging.debug(f'Trigger AGX simulation parameter: {name} = {val}')
            fn(sim, val)

    def registerAgxSimulationParameter(sim: BrickSimulation, name: str, type: str, fn):
        logging.debug(f'Register AGX simulation parameter callback: {name} -> {fn}')
        _agxParameterCallbacks[name] = fn
        return sim.RegisterPythonSimulationParameterCallback(name, type)

    _agxGuiEventCallbacks = {}
    def triggerAgxGuiEventCallback(sim, key, modKeyMask, x, y, keydown):
        # print(f'triggerAgxGuiEventCallback: {key} ({sim})')
        fn = _agxGuiEventCallbacks.get(sim)
        if fn is None:
            return False
        else:
            logging.debug(f'Trigger AGX gui event callback: {key}')
            return fn(sim, key, modKeyMask, x, y, keydown)

    def registerAgxGuiEventListener(sim: BrickSimulation, fn):
        logging.debug(f'Register AGX gui event callback: {sim} -> {fn}')
        _agxGuiEventCallbacks[sim] = fn
except:
    # print('AgxBrick not available in python context')
    pass
