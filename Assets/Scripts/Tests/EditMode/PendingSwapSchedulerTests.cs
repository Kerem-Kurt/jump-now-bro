using System.Collections.Generic;
using NUnit.Framework;
using JumpNowBro.Networking;
using JumpNowBro.Util;

namespace JumpNowBro.Tests
{
    public class PendingSwapSchedulerTests
    {
        static ControlMap JumpToP2 => ControlMap.WithSwap(ControlMap.Default, PlayerAction.Jump);
        static ControlMap DashToP2 => ControlMap.WithSwap(ControlMap.Default, PlayerAction.Dash);

        static List<PendingSwapScheduler.DueSwap> Drain(PendingSwapScheduler s, uint clock)
        {
            // OnTick returns a reused buffer — copy so the test can hold the result across later calls.
            return new List<PendingSwapScheduler.DueSwap>(s.OnTick(clock));
        }

        [Test]
        public void OnTick_BeforeApplyTick_DoesNotApply()
        {
            var s = new PendingSwapScheduler();
            s.Schedule(applyTick: 110, JumpToP2, triggerId: 1);
            Assert.AreEqual(0, Drain(s, 109).Count);
        }

        [Test]
        public void OnTick_AtApplyTick_AppliesOnce()
        {
            var s = new PendingSwapScheduler();
            s.Schedule(110, JumpToP2, 1);
            var first = Drain(s, 110);
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(InputOwner.P2, first[0].Map.jumpOwner);
            Assert.AreEqual(1, first[0].TriggerId);
            Assert.AreEqual(0, Drain(s, 111).Count, "a swap must not re-apply on a later tick");
        }

        [Test]
        public void OnTick_LateEvent_AppliesImmediately()
        {
            // applyTick already passed when the EVENT lands → clamp-to-now (apply on first OnTick).
            var s = new PendingSwapScheduler();
            s.Schedule(100, JumpToP2, 1);
            Assert.AreEqual(1, Drain(s, 130).Count);
        }

        [Test]
        public void OnTick_SameApplyTick_AppliesInInsertionOrder()
        {
            // Two triggers crossed in one host tick: both due at the same applyTick. Insertion order ==
            // host compose order, so the last entry carries the fully-composed (both-swaps) absolute map.
            var s = new PendingSwapScheduler();
            var afterJump = JumpToP2;                                   // {move:P1, jump:P2, dash:P1}
            var afterJumpDash = ControlMap.WithSwap(afterJump, PlayerAction.Dash);  // {move:P1, jump:P2, dash:P2}
            s.Schedule(110, afterJump, 1);
            s.Schedule(110, afterJumpDash, 2);
            var due = Drain(s, 110);
            Assert.AreEqual(2, due.Count);
            Assert.AreEqual(2, due[1].TriggerId);
            Assert.AreEqual(InputOwner.P2, due[1].Map.jumpOwner);
            Assert.AreEqual(InputOwner.P2, due[1].Map.dashOwner, "last-applied map wins; it must include both swaps");
        }

        [Test]
        public void PendingFinalMap_NoEntries_ReturnsCurrent()
        {
            var s = new PendingSwapScheduler();
            var cur = DashToP2;
            Assert.AreEqual(cur.dashOwner, s.PendingFinalMap(cur).dashOwner);
        }

        [Test]
        public void PendingFinalMap_ComposesOntoLastScheduled()
        {
            // Host composes swap B on PendingFinalMap so it doesn't clobber A while A is still pending.
            var s = new PendingSwapScheduler();
            s.Schedule(110, JumpToP2, 1);
            var composed = ControlMap.WithSwap(s.PendingFinalMap(ControlMap.Default), PlayerAction.Dash);
            Assert.AreEqual(InputOwner.P2, composed.jumpOwner);
            Assert.AreEqual(InputOwner.P2, composed.dashOwner);
        }

        [Test]
        public void MapAtTick_ResolvesBoundaryPerTick()
        {
            var s = new PendingSwapScheduler();
            s.Schedule(110, JumpToP2, 1);
            s.Schedule(120, ControlMap.WithSwap(JumpToP2, PlayerAction.Dash), 2);
            Assert.AreEqual(InputOwner.P1, s.MapAtTick(109).jumpOwner, "before any swap → base/Default");
            Assert.AreEqual(InputOwner.P2, s.MapAtTick(110).jumpOwner, "at the first boundary");
            Assert.AreEqual(InputOwner.P1, s.MapAtTick(119).dashOwner, "between boundaries: only the first swap applies");
            Assert.AreEqual(InputOwner.P2, s.MapAtTick(120).dashOwner, "at the second boundary");
        }

        [Test]
        public void ResetTo_CancelsPendingAndSetsBase()
        {
            var s = new PendingSwapScheduler();
            s.Schedule(110, JumpToP2, 1);
            s.ResetTo(DashToP2);
            Assert.AreEqual(0, Drain(s, 200).Count, "a swap scheduled before the reset must never fire");
            Assert.AreEqual(InputOwner.P2, s.MapAtTick(999).dashOwner, "MapAtTick falls back to the new base");
            Assert.AreEqual(InputOwner.P1, s.MapAtTick(999).jumpOwner);
        }

        [Test]
        public void OnTick_WraparoundBoundary_AppliesWhenReached()
        {
            // applyTick just past the u32 wrap; clock crossing it is "newer" by the RFC1982 trick.
            var s = new PendingSwapScheduler();
            s.Schedule(applyTick: 5, JumpToP2, 1);
            Assert.AreEqual(0, Drain(s, uint.MaxValue - 2).Count, "clock before applyTick (across the wrap)");
            Assert.AreEqual(1, Drain(s, 6).Count, "clock has passed applyTick after wrapping");
        }
    }
}
