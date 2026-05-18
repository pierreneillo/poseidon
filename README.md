# Poseidon : water simulation game

This game is in development, check it out in a few weeks !

# Global game concept

You are Poseidon, the all-mighty God of the Sea. You have always been content on planet Earth, as it is composed of 70% of water, but with global warming, water is becoming if not scarce, at least less abundant on planet Earth. A lot of forest fires have started, people are too hot. All this because of one nasty individual : Mr. Global Warming. Being the god of the sea, and thus the god of water, it is your job to stop these fires, to restore the water balance on Earth, and to battle the culprit : Mr. Global Warming.

#Project Setup Guide

This project is built using Unity's **ECS** and **VFX Graph**. Follow these instructions to get the simulation running locally on your machine.

## Prerequisites

1. **Unity Editor**: Ensure you have **Unity 2022.3 LTS** (or later) installed.
2. **Git LFS**: This project tracks binary assets (scenes, profiles) via Git Large File Storage.
3. **Graphics Card**: A dedicated GPU supporting **Compute Shaders** and HLSL is required.

## Quick Start Installation

### 1. Install Git LFS (Linux)
Before cloning, make sure Git LFS is initialized on your system so that textures and scene assets download correctly.
* **Arch Linux:**
  ```bash
  sudo pacman -S git-lfs && git lfs install```
* **Fedora:**
  ```bash
  sudo dnf install git-lfs && git lfs install```
* **Ubuntu/Debian:**
  ```bash
  sudo apt install git-lfs && git lfs install```

### 2. Clone the Repository

### 3. Opening the Project in Unity

1. Open Unity Hub.
2. Click Add > Add project from disk.
3. Select the Poseidon subdirectory from the cloned folder.
4. Open the project. Unity will (normally) download and resolve the required package dependencies (com.unity.entities, com.unity.visualeffectgraph) via the Package Manager.


# TODO

## Art

- Character
- Ennemies
- Environment
- Character animation
- Water rendering
- Sounds
- (Music)
- Storytelling
- Mr. Global Warming (personification of global warming)

## Game

- Moves
- Ball throwing (instead of water)
- Ennemies (HP, attacks)
- Water loss /recuperation over time
- Dodge
- Water handling system (taking water from other planets)

## Simulation

- Water simulation technique choosing
- Basic Water simulation
- Model transition (character mesh <--> water)
- Fire simulation + interaction with the water
- Fire propagation


# Papers used / planned to be used in this game

Position Based Fluids, Miles Macklin & Matthias Müller, ACM Transactions on Graphics (TOG) - SIGGRAPH 2013, Volume 32, Issue 4, July 2013. [https://mmacklin.com/pbf_sig_preprint.pdf]
