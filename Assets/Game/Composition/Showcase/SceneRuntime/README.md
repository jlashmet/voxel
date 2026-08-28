# Showcase composition runtime

Runtime behaviours moved here retain their original Unity script GUIDs so serialized showcase/lookdev scenes keep resolving the same components. They live under reusable composition ownership because they construct, mutate, or present generated world content; `Assets/Scenes/Showcase` is reserved for scene assets and editor-only tooling.
