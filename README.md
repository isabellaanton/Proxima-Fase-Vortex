
A Unity prototype of a 3D geography guessing game. Players explore a rotating globe where real continents look familiar, but several **fictional countries** have been inserted — invented islands, nations carved into the ocean, or territories embedded inside real continents. The goal is to identify each fictional country using progressive clues.

![Unity](https://img.shields.io/badge/Unity-2022.3%20LTS%2B-000000?logo=unity)
![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP-blue)
![Language](https://img.shields.io/badge/language-C%23-239120)
![Status](https://img.shields.io/badge/status-prototype-yellow)

## Table of contents

- [Overview](#overview)
- [Gameplay](#gameplay)
- [How it works](#how-it-works)
- [Project structure](#project-structure)
- [Getting started](#getting-started)
- [Adding a new country](#adding-a-new-country)
- [Roadmap](#roadmap)
- [Tech stack](#tech-stack)
- [License](#license)

## Overview

Each round highlights one fictional country with a glowing, pulsing outline while the camera smoothly moves in to frame it. The player can request up to three clues — terrain and climate, location and neighbors, then culture and flag — with each clue reducing the maximum score available for that round. The player then answers by picking from four options (Easy mode) or typing the country's name (Hard mode).

A second mode, **Find It**, reverses the flow: the game shows a country's name and flag, and the player must click the correct location on the globe.

## Gameplay

1. A round starts and the target country is highlighted on the globe.
2. The camera focuses on it automatically.
3. The player reveals clues on demand, trading score for information.
4. The player answers via multiple choice or free text.
5. Score is calculated from clues used and time remaining, with immediate correct/incorrect feedback.
6. After 10 rounds, a results screen shows total score, accuracy, and average clues used, with a "Play again" option.

The game ships with 10 ready-to-play fictional countries — including Valdoria, Nerath Isles, Kessandra, Solmara, and Brennhold — each with a unique flag, terrain, and set of clues.

## How it works

- The globe is a sphere rendered with an Earth albedo texture, a normal map for terrain relief, and a **country ID map**: a texture where each fictional country is painted as a single flat, unique color.
- A raycast from the pointer hits the globe's collider, converts the hit point to texture coordinates, and samples the ID map at that pixel to identify which country (if any) was clicked.
- A custom URP shader reads the same ID map per-pixel to render the pulsing highlight fill, the glowing outline around the selected country, a separate hover outline for the Find It mode, and subtle borders around every country — all without any additional geometry.
- Each country's on-globe position and visual size are computed automatically by scanning the ID map at startup, so camera framing requires no manually entered coordinates.

## Project structure

```
Assets/
└── Geoguess/
    ├── Scripts/     Game logic: round/state machine, scoring, globe control,
    │                camera focus, clue system, UI, country picking & highlighting
    ├── Shaders/     Globe, atmosphere rim glow, clouds, and starfield skybox shaders
    ├── Editor/      Editor tooling — sample data generation and UI scaffolding
    └── Generated/   Auto-generated placeholder textures, flags, country data, and materials
```

Key scripts:

| Script | Responsibility |
|---|---|
| `GlobeController` | Orbit-style camera rotation, inertia, zoom, idle auto-rotate |
| `CountryPicker` | Raycast → UV → ID-map lookup → `CountryData` resolution |
| `CountryHighlighter` | Drives highlight/hover shader parameters |
| `GameManager` | Round loop, state machine, scoring, both game modes |
| `ClueSystem` | Progressive clue reveal |
| `UIManager` | HUD, menu, answers, feedback, results (view layer only) |
| `CameraFocus` | Smooth camera fly-to-country |
| `CountryData` / `CountryDatabase` | ScriptableObject data model for countries |

## Getting started

**Requirements:** Unity 2022.3 LTS or newer (tested on Unity 6 LTS), Universal Render Pipeline, TextMeshPro.

1. Clone this repository.
2. Open the project folder with **Unity Hub**, using the **Universal 3D (URP)** template if creating fresh, or simply open it as-is if the project files are already configured for URP.
3. Open the scene under `Assets/Scenes`.
4. If you want to regenerate the sample content from scratch, run **Geoguess ▸ Generate Sample Data** from the Unity menu bar. This creates placeholder Earth textures, a country ID map, procedural flags, and 10 `CountryData` assets.
5. Press **Play**.

## Adding a new country

1. Paint the country's region on the **ID map** with a new, unique flat color, and paint matching terrain onto the Earth albedo texture.
2. Create a `CountryData` asset via **Assets ▸ Create ▸ Geoguess ▸ Country Data**, filling in the name, ID color, flag sprite, three clues, and a few decoy answer names.
3. Add the new asset to the **CountryDatabase** list.

The country's globe position and camera framing distance are derived automatically from the ID map — no manual coordinate entry required.

## Roadmap

- Replace placeholder art with a real Earth texture and hand-painted fictional countries
- Vector or SDF-based country borders for crisper edges at high zoom
- Daily challenge mode with a leaderboard
- Additional clue types (capital, currency, landmarks, languages)
- Audio and juice: ambient music, SFX, camera shake, score animations
- Mobile-optimized touch controls and UI safe areas

## Tech stack

- **Engine:** Unity 2022 LTS+ / Unity 6
- **Rendering:** Universal Render Pipeline (URP), custom HLSL shaders
- **Language:** C#
- **UI:** Unity UI + TextMeshPro

## License

This project currently has no license specified. Add a `LICENSE` file (e.g. MIT) if you intend to make this repository open for reuse or contributions.
