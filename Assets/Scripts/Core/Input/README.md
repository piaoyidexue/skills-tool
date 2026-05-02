# User Input Control Module

## Overview
This module implements a semantic input system with input buffering and state interception to solve the "swallowed input" problem in ARPG games.

## Features
- **Semantic Mapping**: Abstracts physical inputs (keyboard, gamepad) into semantic intents (Move, Attack, Skill1, etc.)
- **Input Buffering**: Ring buffer implementation that stores input commands for later execution
- **State Interception**: Integrates with GAS system to block inputs based on status effects (stun, silence)
- **0 GC Design**: Uses structs and arrays for optimal performance

## Components
- `InputIntent.cs`: Defines semantic input intents and command structure
- `InputBuffer.cs`: Ring buffer implementation for input command storage
- `InputSystemManager.cs`: Main input system manager with mapping and interception logic
- `InputBufferManager.cs`: Singleton buffer manager for global access

## Integration
- Automatically integrates with `SkillCaster` for skill execution
- Uses `GEHost` for status effect checking
- Provides event system for external subscription