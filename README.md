![Logo](https://user-images.githubusercontent.com/45887963/202493807-38fa0edc-bd89-4d3b-a6ad-31c7a55e0dcb.png)

This is a mod for **Outer Wilds** which predicts and visualizes future trajectories of player, ship, and scout, in map view.

![Screenshot](https://user-images.githubusercontent.com/45887963/202139195-cc38666c-2c16-4875-ad94-7cadea7cc804.jpg)

## Installation
### Easy installation
- [Install the Outer Wilds Mod Manager](https://github.com/Raicuparta/ow-mod-manager#how-do-i-use-this)
- Search for `Trajectory Prediction`  and install it

### Manual Installation
- [Install OWML](https://github.com/amazingalek/owml#installation)
- [Download the latest release](https://github.com/SkutteOleg/TrajectoryPrediction/releases/latest)
- Extract `OlegSkutte.TrajectoryPrediction` directory to `OWML/Mods` directory
- Run `OWML.Launcher.exe` to start the game

## Settings
![Settings](https://user-images.githubusercontent.com/45887963/202139282-0c378a33-0c12-4907-99e4-c98604dcabe1.jpg)
##### Simulation Settings
- **Seconds To Predict** - Determines how far into the future trajectories are predicted in seconds. Higher values take longer to compute.
- **High Precision Mode** - Toggles simulation time step between 1 second and `Time.fixedDeltaTime`. High Precision Mode takes 60 times longer to compute.
- **Predict GravityVolume Intersections** - Future trajectories may escape or enter gravity volumes of different celestial bodies. This setting toggles detection of which gravity volumes will be active at any given future time step. Takes longer to compute and allocates more memory.
##### Customization Settings
- **Player Trajectory Color** - Hex RGBA color of trajectory line of the player.
- **Ship Trajectory Color** - Hex RGBA color of trajectory line of the ship.
- **Scout Trajectory Color** - Hex RGBA color of trajectory line of the scout.
##### Performance Settings
- **Multithreading** - Shifts computations to a separate thread.
- **RAM To Allocate (Megabytes)** - Allocates roughly specified amount of RAM to reduce the frequency of GC lag spikes.
##### Experimental Settings
- **Parallelization** - Runs simulation of all celestial bodies in parallel. Speeds up computation but makes results inaccurate.

## Limitations / Things to Improve
- This mod doesn't account for atmospheric drag. I assume it would've been computationally expensive and pretty useless in practice.
- Simulation allocates a lot of memory, causing periodic GC lag spikes. This could be helped by improving the memory footprint or by finding a way to enable Unity's incremental GC.
- To predict future trajectory of a celestial body, this mod first predicts future trajectories of celestial bodies whose gravity affects said body. Because of that, it doesn't work too well with The Hourglass Twins, since they both affect each other causing a mutual recursion.

*Besides occasional bugfixes, I'm not planning to update this mod further. But if you have a pull request with improvements I'll merge it.*
