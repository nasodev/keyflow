using System.Collections.Generic;
using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class HoldStateMachineTests
    {
        [Test]
        public void Start_WithoutTap_IsSpawned()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(lane: 0, startMs: 1000, endMs: 2000);
            Assert.AreEqual(HoldState.Spawned, sm.GetState(id));
        }

        [Test]
        public void OnStartTapAccepted_TransitionsToHolding()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            Assert.AreEqual(HoldState.Holding, sm.GetState(id));
        }

        [Test]
        public void Holding_ThroughEnd_TransitionsToCompleted()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            var pressed = new HashSet<int> { 0 };

            sm.Tick(songTimeMs: 1500, pressedLanes: pressed);
            Assert.AreEqual(HoldState.Holding, sm.GetState(id));

            sm.Tick(songTimeMs: 2000, pressedLanes: pressed);
            Assert.AreEqual(HoldState.Completed, sm.GetState(id));
        }

        [Test]
        public void Holding_ReleasedEarly_TransitionsToBroken()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);

            sm.Tick(1500, new HashSet<int>());
            Assert.AreEqual(HoldState.Broken, sm.GetState(id));
        }

        [Test]
        public void Spawned_Tick_RemainsSpawned()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.Tick(1500, new HashSet<int> { 0 });
            Assert.AreEqual(HoldState.Spawned, sm.GetState(id));
        }

        [Test]
        public void MultipleConcurrentHolds_IndependentStates()
        {
            var sm = new HoldStateMachine();
            var a = sm.Register(0, 1000, 2000);
            var b = sm.Register(2, 1200, 2500);
            sm.OnStartTapAccepted(a);
            sm.OnStartTapAccepted(b);

            var pressed = new HashSet<int> { 0 };
            sm.Tick(1500, pressed);

            Assert.AreEqual(HoldState.Holding, sm.GetState(a));
            Assert.AreEqual(HoldState.Broken, sm.GetState(b));
        }

        [Test]
        public void Completed_SubsequentTick_StaysCompleted()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            sm.Tick(2000, new HashSet<int> { 0 });
            sm.Tick(2500, new HashSet<int>());
            Assert.AreEqual(HoldState.Completed, sm.GetState(id));
        }

        [Test]
        public void Broken_SubsequentTick_StaysBroken()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            sm.Tick(1500, new HashSet<int>());
            sm.Tick(1800, new HashSet<int> { 0 });
            Assert.AreEqual(HoldState.Broken, sm.GetState(id));
        }

        [Test]
        public void TickReturnsTransitions_ForObservation()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            var transitions = sm.Tick(2000, new HashSet<int> { 0 });

            Assert.AreEqual(1, transitions.Count);
            Assert.AreEqual(id, transitions[0].id);
            Assert.AreEqual(HoldState.Completed, transitions[0].newState);
        }
    }
}
