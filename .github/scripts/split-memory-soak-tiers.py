#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def replace_once(path: str, old: str, new: str) -> None:
    p = ROOT / path
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one match, found {count}\n--- old ---\n{old}")
    p.write_text(text.replace(old, new, 1))


memory = "Assets/Tests/PlayMode/MemoryStabilityTests.cs"
replace_once(
    memory,
    '''        [Test]\n        [Category("SC_005")]\n        [Category("US4")]\n        public void MemoryStaysWithinTierBudgetOverTwoHours()\n        {\n            // Arrange: set up per-tier budgets from device-matrix.md.\n            var tierConfigs = new (DeviceTier Tier, int BrickPoolCapacityBytes, float MaxWorldMemoryMB)[]\n            {\n                (DeviceTier.PC,      1 << 29, 1800f),   // 1.5 GB pool + overhead\n                (DeviceTier.Console, 1 << 30, 1200f),   // 1.0 GB pool + overhead\n                (DeviceTier.MobileHE, 1 << 22, 464f),   // 384 MB pool + overhead\n            };\n\n            foreach (var (tier, brickPoolCapacityBytes, maxWorldMemoryMB) in tierConfigs)\n            {\n                RunOneTierMemoryTest(tier, brickPoolCapacityBytes, maxWorldMemoryMB);\n            }\n        }\n''',
    '''        // One tier per test/process. Unity's native allocator may retain released pages in\n        // process RSS, so running all three capacities sequentially makes the watchdog measure\n        // allocator history rather than the active tier's world memory.\n        [Test]\n        [Category("SC_005")]\n        [Category("US4")]\n        public void PcMemoryStaysWithinTierBudgetOverTwoHours() =>\n            RunOneTierMemoryTest(DeviceTier.PC, 1 << 29, 1800f);\n\n        [Test]\n        [Category("SC_005")]\n        [Category("US4")]\n        public void ConsoleMemoryStaysWithinTierBudgetOverTwoHours() =>\n            RunOneTierMemoryTest(DeviceTier.Console, 1 << 30, 1200f);\n\n        [Test]\n        [Category("SC_005")]\n        [Category("US4")]\n        public void MobileHeMemoryStaysWithinTierBudgetOverTwoHours() =>\n            RunOneTierMemoryTest(DeviceTier.MobileHE, 1 << 22, 464f);\n''',
)
replace_once(
    memory,
    '            var pool = new BrickPool(1 << 20, Allocator.Persistent);\n',
    '            var pool = new BrickPool(16, Allocator.Persistent);\n',
)

for workflow in (".github/workflows/tests-pr.yml", ".github/workflows/tests-master.yml"):
    replace_once(
        workflow,
        """                '^VoxelEngine\\.Tests\\.PlayMode\\.MemoryStabilityTests\\.MemoryStaysWithinTierBudgetOverTwoHours$'\n                '^VoxelEngine\\.Tests\\.PlayMode\\.MemoryStabilityTests\\.EvictionReturnsBricksToPool$'\n""" if workflow.endswith("tests-pr.yml") else """              '^VoxelEngine\\.Tests\\.PlayMode\\.MemoryStabilityTests\\.MemoryStaysWithinTierBudgetOverTwoHours$'\n              '^VoxelEngine\\.Tests\\.PlayMode\\.MemoryStabilityTests\\.EvictionReturnsBricksToPool$'\n""",
        """                '^VoxelEngine\\.Tests\\.PlayMode\\.MemoryStabilityTests\\.PcMemoryStaysWithinTierBudgetOverTwoHours$'\n                '^VoxelEngine\\.Tests\\.PlayMode\\.MemoryStabilityTests\\.ConsoleMemoryStaysWithinTierBudgetOverTwoHours$'\n                '^VoxelEngine\\.Tests\\.PlayMode\\.MemoryStabilityTests\\.MobileHeMemoryStaysWithinTierBudgetOverTwoHours$'\n                '^VoxelEngine\\.Tests\\.PlayMode\\.MemoryStabilityTests\\.EvictionReturnsBricksToPool$'\n""" if workflow.endswith("tests-pr.yml") else """              '^VoxelEngine\\.Tests\\.PlayMode\\.MemoryStabilityTests\\.PcMemoryStaysWithinTierBudgetOverTwoHours$'\n              '^VoxelEngine\\.Tests\\.PlayMode\\.MemoryStabilityTests\\.ConsoleMemoryStaysWithinTierBudgetOverTwoHours$'\n              '^VoxelEngine\\.Tests\\.PlayMode\\.MemoryStabilityTests\\.MobileHeMemoryStaysWithinTierBudgetOverTwoHours$'\n              '^VoxelEngine\\.Tests\\.PlayMode\\.MemoryStabilityTests\\.EvictionReturnsBricksToPool$'\n""",
    )
    replace_once(
        workflow,
        """                memory-two-hours memory-eviction memory-snapshot n-s\n""" if workflow.endswith("tests-pr.yml") else """              memory-two-hours memory-eviction memory-snapshot n-s\n""",
        """                memory-pc-two-hours memory-console-two-hours memory-mobile-two-hours\n                memory-eviction memory-snapshot n-s\n""" if workflow.endswith("tests-pr.yml") else """              memory-pc-two-hours memory-console-two-hours memory-mobile-two-hours\n              memory-eviction memory-snapshot n-s\n""",
    )

print("per-tier memory soak isolation staged")
