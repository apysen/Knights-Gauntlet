# Animated capture slash

Visual references supplied by the user:

- https://godotshaders.com/shader-tag/sword-slash/
- https://godotshaders.com/shader/procedural-idyllic-slash-2d/
- https://godotshaders.com/shader/procedural-cyclic-slash/

The Unity implementation is `Assets/Resources/CaptureSlash.shader`. It is an original focused implementation for the game's imported manga silhouettes; no Godot shader source or demonstration images were copied into the project.

It uses per-capture progress to reveal the blade, a polar sweep for the crescent variant, a bright moving tip, and a trailing ink breakup. Each pooled renderer gets its own progress and seed through a material property block, while sharing one material. The shader has no global-time animation or external noise texture. The effect remains anchored to the landing square as the board scrolls.

The artwork and exact generation prompts are recorded in `PROMPTS.md`. The downloaded audio and CC0 license details are under `ArtSource/Audio/StarNinjas/`.

The replacement swept-ribbon artwork follows the continuous curved trail and tapered mask principle described in Cyanilux's Sword Slash Shader Breakdown:
https://www.cyanilux.com/tutorials/sword-slash-shader-breakdown/
It replaces the rejected double-ribbon silhouette with one connected blade afterimage. No reference artwork or shader code was copied. Every capture independently chooses a rotation from 0 to 360 degrees, while retaining the localized footprint and per-sprite sweep animation.
