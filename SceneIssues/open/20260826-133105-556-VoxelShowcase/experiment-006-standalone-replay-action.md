# Experiment 006 — standalone replay action

## Question
Why did exact green run `33131101780` prove the gate behavior yet its canonical standalone-player frame still show the closed gate?

## Discriminator
The player replay harness restores only the recorded camera transform. It does not restore the character motor or replay the captured E interaction, so canonical evidence necessarily remains pre-action even when the regression opens the gate.

## Result
Behavior and presentation were not contradictory: the behavioral regression opened the gate, while standalone evidence never performed the interaction. The offscreen `Camera.Render()` frames are noncanonical and fallback-gray, so they cannot substitute for the player framebuffer.

## Action / falsifier
Add an opt-in `replayAction: "interact"` to this capture. The non-development replay harness synchronizes the production motor via `VoxelShowcase.TeleportTo`, invokes the same public `TryInteract()` bound to E once, then keeps the exact captured camera pinned while the 0.9 s animation completes. Reject if final CI does not log accepted interaction or the standalone frame fails to show the post-action opening.
