using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseInputSystemTests
    {
        private Keyboard _keyboard;
        private Mouse _mouse;

        [SetUp]
        public void SetUp()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>("Showcase regression keyboard");
            _mouse = InputSystem.AddDevice<Mouse>("Showcase regression mouse");
        }

        [TearDown]
        public void TearDown()
        {
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        }

        [Test]
        public void ReadCurrent_MapsCompleteKeyboardAndMouseSemanticFrame()
        {
            InputSystem.QueueStateEvent(
                _keyboard,
                new KeyboardState(
                    Key.Escape, Key.F, Key.R, Key.E,
                    Key.W, Key.D, Key.LeftShift, Key.Space, Key.LeftCtrl));
            InputSystem.QueueStateEvent(
                _mouse,
                new MouseState
                {
                    delta = new Vector2(25f, -10f),
                    scroll = new Vector2(0f, 120f),
                }.WithButton(MouseButton.Left));
            InputSystem.Update();

            ShowcaseInputFrame frame = ShowcaseInputSystem.ReadCurrent();

            Assert.That(frame.ToggleCursor, Is.True);
            Assert.That(frame.ToggleFly, Is.True);
            Assert.That(frame.Respawn, Is.True);
            Assert.That(frame.Interact, Is.True);
            Assert.That(frame.Forward, Is.True);
            Assert.That(frame.Backward, Is.False);
            Assert.That(frame.Right, Is.True);
            Assert.That(frame.Left, Is.False);
            Assert.That(frame.Sprint, Is.True);
            Assert.That(frame.Jump, Is.True);
            Assert.That(frame.Descend, Is.True);
            Assert.That(frame.PrimaryEdit, Is.True);
            Assert.That(frame.SecondaryEdit, Is.False);
            Assert.That(frame.LookDelta.x, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(frame.LookDelta.y, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(frame.ScrollDirection, Is.EqualTo(1));
        }

        [Test]
        public void ReadCurrent_PressedActionsAreEdgeTriggered_HeldMovementPersists()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.E, Key.W));
            InputSystem.Update();

            ShowcaseInputFrame first = ShowcaseInputSystem.ReadCurrent();
            Assert.That(first.Interact, Is.True);
            Assert.That(first.Forward, Is.True);

            InputSystem.Update();
            ShowcaseInputFrame second = ShowcaseInputSystem.ReadCurrent();
            Assert.That(second.Interact, Is.False);
            Assert.That(second.Forward, Is.True);
        }

        [Test]
        public void ReadCurrent_NormalizesWheelDirectionAndMapsSecondaryEdit()
        {
            InputSystem.QueueStateEvent(
                _mouse,
                new MouseState { scroll = new Vector2(0f, -240f) }
                    .WithButton(MouseButton.Right));
            InputSystem.Update();

            ShowcaseInputFrame frame = ShowcaseInputSystem.ReadCurrent();

            Assert.That(frame.ScrollDirection, Is.EqualTo(-1));
            Assert.That(frame.PrimaryEdit, Is.False);
            Assert.That(frame.SecondaryEdit, Is.True);
        }
    }
}
