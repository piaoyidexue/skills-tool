# Logical Grid & Placement Module

## Overview
This module implements a discrete logical grid system for tower defense and elemental gameplay, providing O(1) coordinate conversion and efficient placement validation.

## Features
- **Mathematical Grid System**: Pure mathematical conversion between world and grid coordinates
- **Discrete Grid Data**: 0 GC structure for efficient grid cell storage
- **Placement Validation**: Comprehensive validation pipeline for tower building
- **Row/Column Resonance**: Optimized queries for elemental row/column effects
- **Path Blocking Detection**: Smart path validation to prevent maze creation

## Components
- `GridMath.cs`: Mathematical foundation for coordinate conversion
- `GridCellData.cs`: Discrete cell data structure with terrain and occupancy info
- `LogicalGridManager.cs`: Main grid manager with data storage and query APIs
- `PlacementValidator.cs`: UI-friendly validation interface with preview status

## Integration
- Provides direct integration with `SkillCaster` for resonance effects
- Works with Unity's NavMesh for dynamic pathfinding updates
- Supports visual feedback through holographic projection rendering