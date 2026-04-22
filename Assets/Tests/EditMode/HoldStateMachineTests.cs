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
            var buffer = new List<HoldTransition>();

            sm.Tick(songTimeMs: 1500, pressedLanes: pressed, outTransitions: buffer);
            Assert.AreEqual(HoldState.Holding, sm.GetState(id));

            sm.Tick(songTimeMs: 2000, pressedLanes: pressed, outTransitions: buffer);
            Assert.AreEqual(HoldState.Completed, sm.GetState(id));
        }

        [Test]
        public void Holding_ReleasedEarly_TransitionsToBroken()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            var buffer = new List<HoldTransition>();

            sm.Tick(1500, new HashSet<int>(), buffer);
            Assert.AreEqual(HoldState.Broken, sm.GetState(id));
        }

        [Test]
        public void Spawned_Tick_RemainsSpawned()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.Tick(1500, new HashSet<int> { 0 }, new List<HoldTransition>());
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
            sm.Tick(1500, pressed, new List<HoldTransition>());

            Assert.AreEqual(HoldState.Holding, sm.GetState(a));
            Assert.AreEqual(HoldState.Broken, sm.GetState(b));
        }

        [Test]
        public void Completed_SubsequentTick_StaysCompleted()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            var buffer = new List<HoldTransition>();
            sm.Tick(2000, new HashSet<int> { 0 }, buffer);
            sm.Tick(2500, new HashSet<int>(), buffer);
            Assert.AreEqual(HoldState.Completed, sm.GetState(id));
        }

        [Test]
        public void Broken_SubsequentTick_StaysBroken()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            var buffer = new List<HoldTransition>();
            sm.Tick(1500, new HashSet<int>(), buffer);
            sm.Tick(1800, new HashSet<int> { 0 }, buffer);
            Assert.AreEqual(HoldState.Broken, sm.GetState(id));
        }

        [Test]
        public void Tick_WritesTransitionsIntoProvidedBuffer()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            var buffer = new List<HoldTransition>();

            sm.Tick(2000, new HashSet<int> { 0 }, buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(id, buffer[0].id);
            Assert.AreEqual(HoldState.Completed, buffer[0].newState);
        }

        [Test]
        public void Tick_ClearsBufferBeforeAdding()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            var buffer = new List<HoldTransition>
            {
                new HoldTransition { id = 999, newState = HoldState.Broken } // sentinel
            };

            sm.Tick(1500, new HashSet<int> { 0 }, buffer); // no transition this tick

            Assert.AreEqual(0, buffer.Count, "Tick should clear caller's buffer before adding");
        }

        [Test]
        public void Tick_PreservesBufferCapacityAcrossCalls()
        {
            var sm = new HoldStateMachine();
            var id = sm.Register(0, 1000, 2000);
            sm.OnStartTapAccepted(id);
            var buffer = new List<HoldTransition>(capacity: 8);
            int initialCapacity = buffer.Capacity;

            for (int i = 0; i < 5; i++)
                sm.Tick(1500, new HashSet<int> { 0 }, buffer);

            Assert.AreEqual(initialCapacity, buffer.Capacity, "Buffer capacity must not grow across steady-state ticks");
        }
    }
}
