using System;
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using LiquidState.Common;
using LiquidState.Core;

namespace LiquidState.Synchronous.Core
{
    internal static class ExecutionHelper
    {
        internal static void ThrowInTransition()
        {
            throw new InvalidOperationException(
                "State cannot be changed while already in transition. Tip: Use an asynchronous state machine such as QueuedStateMachine that has these parallel semantics for these to work out of the box.");
        }

        internal static void ExecuteAction<TState, TTrigger>(Action<Transition<TState, TTrigger>> action, Transition<TState, TTrigger> transition)
        {
            if (action != null) action.Invoke(transition);
        }

        internal static void MoveToStateCore<TState, TTrigger>(TState state, StateTransitionOption option,
            RawStateMachineBase<TState, TTrigger> machine, bool ignoreInvalidStates = false)
        {
            Contract.Requires(machine != null);

            StateRepresentation<TState, TTrigger> targetRep;
            if (machine.Representations.TryGetValue(state, out targetRep))
            {
                var currentRep = machine.CurrentStateRepresentation;
                machine.RaiseTransitionStarted(targetRep.State);

                var transition = new Transition<TState, TTrigger>(currentRep.State, state);

                if ((option & StateTransitionOption.CurrentStateExitTransition) ==
                    StateTransitionOption.CurrentStateExitTransition)
                {
                    ExecuteAction(currentRep.OnExitAction, transition);
                }
                if ((option & StateTransitionOption.NewStateEntryTransition) ==
                    StateTransitionOption.NewStateEntryTransition)
                {
                    ExecuteAction(targetRep.OnEntryAction, transition);
                }

                var pastState = currentRep.State;
                machine.CurrentStateRepresentation = targetRep;
                machine.RaiseTransitionExecuted(pastState);
            }
            else
            {
                if (!ignoreInvalidStates)
                    machine.RaiseInvalidState(state);
            }
        }

        internal static void FireCore<TState, TTrigger>(TTrigger trigger, RawStateMachineBase<TState, TTrigger> machine,
            bool raiseInvalidStateOrTrigger = true)
        {
            Contract.Requires(machine != null);

            var currentStateRepresentation = machine.CurrentStateRepresentation;
            var triggerRep = DiagnosticsHelper.FindAndEvaluateTriggerRepresentation(trigger, machine,
                raiseInvalidStateOrTrigger);

            if (triggerRep == null)
                return;

            // Catch invalid parameters before execution.

            Action<Transition<TState, TTrigger>> triggerAction = null;
            try
            {
                triggerAction = (Action<Transition<TState, TTrigger>>)triggerRep.OnTriggerAction;
            }
            catch (InvalidCastException)
            {
                if (raiseInvalidStateOrTrigger)
                    machine.RaiseInvalidTrigger(trigger);

                return;
            }

            StateRepresentation<TState, TTrigger> nextStateRep = null;

            if (StateConfigurationHelper.CheckFlag(triggerRep.TransitionFlags,
                TransitionFlag.DynamicState))
            {
                var dynamicState = ((Func<DynamicState<TState>>) triggerRep.NextStateRepresentationWrapper)();
                if (!dynamicState.CanTransition)
                    return;

                var state = dynamicState.ResultingState;
                nextStateRep = StateConfigurationHelper.FindStateRepresentation(state,
                    machine.Representations);

                if (nextStateRep == null)
                {
                    if (raiseInvalidStateOrTrigger)
                        machine.RaiseInvalidState(state);
                    return;
                }
            }
            else
            {
                nextStateRep = (StateRepresentation<TState, TTrigger>) triggerRep.NextStateRepresentationWrapper;
            }

            var transition = new Transition<TState, TTrigger>(currentStateRepresentation.State, nextStateRep.State);

            machine.RaiseTransitionStarted(nextStateRep.State);

            // Current exit
            var currentExit = currentStateRepresentation.OnExitAction;
            ExecuteAction(currentExit, transition);

            // Trigger entry
            ExecuteAction(triggerAction, transition);

            // Next entry
            var nextEntry = nextStateRep.OnEntryAction;
            ExecuteAction(nextEntry, transition);

            var pastState = machine.CurrentState;
            machine.CurrentStateRepresentation = nextStateRep;
            machine.RaiseTransitionExecuted(pastState);
        }

        internal static void FireCore<TState, TTrigger, TArgument>(
            ParameterizedTrigger<TTrigger, TArgument> parameterizedTrigger, TArgument argument,
            RawStateMachineBase<TState, TTrigger> machine, bool raiseInvalidStateOrTrigger = true)
        {
            Contract.Requires(machine != null);

            var currentStateRepresentation = machine.CurrentStateRepresentation;
            var trigger = parameterizedTrigger.Trigger;

            var triggerRep = DiagnosticsHelper.FindAndEvaluateTriggerRepresentation(trigger, machine,
                raiseInvalidStateOrTrigger);
            if (triggerRep == null)
                return;

            // Catch invalid parameters before execution.

            Action<Transition<TState, TTrigger>, TArgument> triggerAction = null;
            try
            {
                triggerAction = (Action<Transition<TState, TTrigger>, TArgument>) triggerRep.OnTriggerAction;
            }
            catch (InvalidCastException)
            {
                if (raiseInvalidStateOrTrigger)
                    machine.RaiseInvalidTrigger(trigger);
                return;
            }

            StateRepresentation<TState, TTrigger> nextStateRep = null;

            if (StateConfigurationHelper.CheckFlag(triggerRep.TransitionFlags,
                TransitionFlag.DynamicState))
            {
                var dynamicState = ((Func<DynamicState<TState>>) triggerRep.NextStateRepresentationWrapper)();
                if (!dynamicState.CanTransition)
                    return;

                var state = dynamicState.ResultingState;
                nextStateRep = StateConfigurationHelper.FindStateRepresentation(state,
                    machine.Representations);

                if (nextStateRep == null)
                {
                    if (raiseInvalidStateOrTrigger)
                        machine.RaiseInvalidState(state);
                    return;
                }
            }
            else
            {
                nextStateRep = (StateRepresentation<TState, TTrigger>) triggerRep.NextStateRepresentationWrapper;
            }

            var transition = new Transition<TState, TTrigger>(currentStateRepresentation.State, nextStateRep.State);

            machine.RaiseTransitionStarted(nextStateRep.State);

            // Current exit
            var currentExit = currentStateRepresentation.OnExitAction;
            ExecuteAction(currentExit, transition);
            
            // Trigger entry
            if (triggerAction != null) triggerAction.Invoke(transition, argument);


            // Next entry
            var nextEntry = nextStateRep.OnEntryAction;
            ExecuteAction(nextEntry, transition);

            var pastState = machine.CurrentState;
            machine.CurrentStateRepresentation = nextStateRep;
            machine.RaiseTransitionExecuted(pastState);
        }
    }
}
