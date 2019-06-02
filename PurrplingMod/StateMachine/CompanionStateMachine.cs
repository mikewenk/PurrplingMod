﻿using System;
using System.Collections.Generic;
using PurrplingMod.Loader;
using PurrplingMod.StateMachine.State;
using PurrplingMod.StateMachine.StateFeatures;
using PurrplingMod.Utils;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PurrplingMod.StateMachine
{

    internal class CompanionStateMachine
    {
        public enum StateFlag
        {
            RESET,
            AVAILABLE,
            RECRUITED,
            UNAVAILABLE,
        }
        public CompanionManager CompanionManager { get; private set; }
        public NPC Companion { get; private set; }
        public IMonitor Monitor { get; }
        public Dictionary<StateFlag, ICompanionState> States { get; private set; }
        private ContentLoader.AssetsContent assets;
        private ICompanionState currentState;

        public CompanionStateMachine(CompanionManager manager, NPC companion, ContentLoader.AssetsContent assets, IMonitor monitor = null)
        {
            this.CompanionManager = manager;
            this.Companion = companion;
            this.assets = assets;
            this.Monitor = monitor;
        }

        public string Name
        {
            get
            {
                return this.Companion.Name;
            }
        }

        public StateFlag CurrentStateFlag { get; private set; }
        public Dictionary<int, SchedulePathDescription> BackedupSchedule { get; internal set; }
        public bool RecruitedToday { get; private set; }

        private void ChangeState(StateFlag stateFlag)
        {
            if (this.States == null)
                throw new InvalidStateException("State machine is not ready! Call setup first.");

            if (!this.States.TryGetValue(stateFlag, out ICompanionState newState))
                throw new InvalidStateException($"Invalid state {stateFlag.ToString()}. Is state machine correctly set up?");

            if (this.currentState == newState)
                return;

            if (this.currentState != null)
            {
                this.currentState.Exit();
            }

            newState.Entry();
            this.currentState = newState;
            this.Monitor.Log($"{this.Name} changed state: {this.CurrentStateFlag.ToString()} -> {stateFlag.ToString()}");
            this.CurrentStateFlag = stateFlag;
        }

        public void Setup(Dictionary<StateFlag, ICompanionState> states)
        {
            if (this.States != null)
                throw new InvalidOperationException("State machine is already set up!");

            this.States = states;
            this.ResetStateMachine();
        }

        public void DialogueSpeaked(Dialogue speakedDialogue)
        {
            IDialogueDetector detector = this.currentState as IDialogueDetector;

            if (detector != null)
            {
                detector.OnDialogueSpeaked(speakedDialogue);
            }
        }

        public void NewDaySetup()
        {
            if (this.CurrentStateFlag != StateFlag.RESET)
                throw new InvalidStateException($"State machine {this.Name} must be in reset state!");

            DialogueHelper.SetupDialogues(this.Companion, this.assets.dialogues);
            this.RecruitedToday = false;
            this.MakeAvailable();
        }

        public void MakeAvailable()
        {
            this.ChangeState(StateFlag.AVAILABLE);
        }

        public void MakeUnavailable()
        {
            this.ChangeState(StateFlag.UNAVAILABLE);
        }

        public void ResetStateMachine()
        {
            this.ChangeState(StateFlag.RESET);
        }

        internal void Dismiss(bool keepUnavailableOthers = false)
        {
            this.ResetStateMachine();

            if (this.currentState is ICompanionIntegrator integrator)
                integrator.ReintegrateCompanionNPC();

            this.BackedupSchedule = null;
            this.ChangeState(StateFlag.UNAVAILABLE);
            this.CompanionManager.CompanionDissmised(keepUnavailableOthers);
        }

        public void Recruit()
        {
            this.BackedupSchedule = this.Companion.Schedule;
            this.RecruitedToday = true;

            this.ChangeState(StateFlag.RECRUITED);
            this.CompanionManager.CompanionRecuited(this.Companion.Name);
        }

        public void Dispose()
        {
            if (this.currentState != null)
                this.currentState.Exit();

            this.States.Clear();
            this.States = null;
            this.currentState = null;
            this.Companion = null;
            this.CompanionManager = null;
        }

        public void ResolveDialogueRequest()
        {
            if (!this.CanDialogueRequestResolve())
                return;

            (this.currentState as IRequestedDialogueCreator).CreateRequestedDialogue();
        }

        public bool CanDialogueRequestResolve()
        {
            return this.currentState is IRequestedDialogueCreator dcreator && dcreator.CanCreateDialogue;
        }
    }

    class InvalidStateException : Exception
    {
        public InvalidStateException(string message) : base(message)
        {
        }
    }
}
