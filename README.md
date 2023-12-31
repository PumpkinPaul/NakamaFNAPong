# Pong
Pong - ECS and Networked Multiplayer.

## Overview

A short demo project of the Atari classic, Pong - highlighting three core concepts:
- A **pure** ECS for the world / entity management
- A client relayed networked multiplayer element
- Network prediction for smooth gameplay

### ECS 

> Entity component system (ECS) is a software architectural pattern mostly used in video game development for the representation of game world objects. An ECS comprises entities composed from components of data, with systems which operate on entities' components.
> 
> ECS follows the principle of composition over inheritance, meaning that every entity is defined not by a type hierarchy, but by the components that are associated with it. Systems act globally over all entities which have the required components. 

[MoonTools.ECS](https://gitea.moonside.games/MoonsideGames/MoonTools.ECS) is in the author's own words, "A very simple ECS system."

It could be considered a 'Pure' ECS:
- [E]ntity - Nothing more than a number - acts as an 'indexer' into the various component collections.
- [C]omponent - Data, no behaviour - components in MoonTools.ECS are limited to unmanaged values types only, no class references are allowed here.
- [S]ystem - Functions that operate on entities that conform to a certain set of components
  
  > e.g. A system to move entities in the world could query for entities with both Position and Velocity components.

### Network Multiplayer

Implemented using a Client Relayed Multiplayer approach using [Nakama](https://heroiclabs.com/nakama/) server and client libraries.

> Nakama is the leading open source game server framework for building online multiplayer games in Godot, Unity, Unreal Engine, MonoGame, LibGDX, Defold, Cocos2d, Phaser, Macroquad and more

### Network Prediction

> This project shows how to use prediction and smoothing algorithms to compensate for network lag. This makes remotely controlled objects appear to move smoothly even when there is a significant delay in packets being delivered over the network. 

## Getting Started

Install Nakama - 
The easiest way to do this is to follow the [Install](https://heroiclabs.com/docs/nakama/getting-started/install/) documentation on the Nakama webite.

Clone the source code and build the app - all dependencies should be included in the project.

You should then be able to launch two instances of the game to test locally.

## Credits

Inspired by:
- [Pong](https://en.wikipedia.org/wiki/Pong) by Atari

Frameworks:
- [FNA](https://github.com/FNA-XNA/FNA) - _an XNA4 reimplementation that focuses solely on developing a fully accurate XNA4 runtime for the desktop._
- [MoonTools.ECS](https://gitea.moonside.games/MoonsideGames/MoonTools.ECS) _by MoonsideGames_
- [Nakama](https://heroiclabs.com/nakama/) _by Herioc Labs_

Fonts:
- [Squared Display](https://www.dafont.com/squared-display.font) _by Vikas Kumar_
